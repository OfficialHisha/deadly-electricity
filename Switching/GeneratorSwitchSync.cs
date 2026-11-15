using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using BepInEx.Logging;

namespace DeadlyElectricity.Switching
{
    /// <summary>
    /// Multiplayer synchronisation of the generator switch state (host to clients).
    ///
    /// <para>
    /// <b>Who needs it.</b> The host decides everything (power, damage, IC10, saving). Clients only need to know
    /// which generators are switched off so that they <i>look</i> right: wind turbine blades and sound stop, the
    /// tooltip line shows, and reading <c>On</c> returns the real value. Without this, a client would see a
    /// switched-off wind turbine spinning.
    /// </para>
    ///
    /// <para>
    /// <b>Why separate messages and not the game's own update stream.</b> The game's per-thing network updates are
    /// a positional binary protocol: appending bytes for a client that does not run the mod would corrupt the
    /// stream and disconnect it. Mod messages travel in their own length-delimited packets (through
    /// LaunchPadBooster), so a message that is not understood is simply skipped.
    /// </para>
    ///
    /// <para>
    /// <b>This is opt-in</b> (<c>GeneratorSwitch.EnableNetworkSync</c>, default off). Using the LaunchPadBooster
    /// networking changes the join handshake of the whole game session, which means every player must run this mod
    /// with the setting enabled to be able to join. It must therefore be a deliberate choice of the server owner.
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> Nothing is sent while nobody toggles a generator. A toggle is queued and, once per frame at most,
    /// all queued changes are sent as one small message (about 9 bytes per generator). A joining player gets the list
    /// of switched-off generators once, as part of the join data. If the option is off, every entry point returns
    /// after reading a single boolean.
    /// </para>
    /// </summary>
    internal static class GeneratorSwitchSync
    {
        /// <summary>One queued switch change.</summary>
        internal readonly struct Change
        {
            internal readonly long ReferenceId;
            internal readonly bool Disabled;

            internal Change(long referenceId, bool disabled)
            {
                ReferenceId = referenceId;
                Disabled = disabled;
            }
        }

        // True only after the LaunchPadBooster link has been set up successfully.
        private static volatile bool _active;

        // Changes waiting to be sent. Written from whichever thread runs the IC10 program, read on the main thread.
        private static readonly List<Change> Pending = new();
        private static readonly object PendingLock = new();
        private static int _pendingCount;

        // Reused for the outgoing batch so that sending does not allocate per frame.
        private static readonly List<Change> Batch = new();

        // Set by StartLink. Calling the link through a delegate keeps every method of this class free of references
        // to LaunchPadBooster types, so none of them can fail to load when that library is not installed.
        private static Action<List<Change>> _sender;

        /// <summary>True when the multiplayer sync is running on this machine.</summary>
        internal static bool Active => _active;

        /// <summary>
        /// Sets the sync up if the configuration asks for it. Called once from the plugin's <c>Awake</c>.
        /// Never throws: if LaunchPadBooster is missing or anything fails, the feature keeps working without sync.
        /// </summary>
        internal static void Initialize(string modVersion)
        {
            if (!Configuration.ConfigurationHelper.GetBool("EnableNetworkSync")) return;
            if (!GeneratorSwitch.Enabled) return;

            try
            {
                StartLink(modVersion);
            }
            catch (Exception exception)
            {
                // Typically LaunchPadBooster.dll is not present, which surfaces as a load/type exception here.
                _active = false;
                _sender = null;
                DeadlyElectricity.LogModMessage(LogLevel.Warning,
                    $"Generator switch network sync is enabled but could not start ({exception.GetType().Name}: {exception.Message}). The switch still works, but clients will not see its state.");
            }
        }

        /// <summary>
        /// The only method of this class that references <see cref="LaunchPadLink"/>. It must not be inlined, so that
        /// a missing LaunchPadBooster fails here, inside the caller's try/catch, and nowhere else.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartLink(string modVersion)
        {
            if (!LaunchPadLink.TryInitialize(modVersion)) return;

            _sender = LaunchPadLink.Send;
            _active = true;
        }

        /// <summary>
        /// Queues a change made on this machine so that it is announced to the clients.
        /// Only the host announces; anywhere else (or with the sync off) this returns immediately.
        /// </summary>
        internal static void NotifyChanged(Thing thing, bool disabled)
        {
            if (!_active || !NetworkManager.IsServer) return;

            lock (PendingLock)
            {
                Pending.Add(new Change(thing.ReferenceId, disabled));
                Volatile.Write(ref _pendingCount, Pending.Count);
            }
        }

        /// <summary>
        /// Sends everything queued since the last call as a single message. Called once per frame from the plugin's
        /// <c>Update</c>; when nothing is queued this is one integer read.
        /// </summary>
        internal static void Flush()
        {
            if (Volatile.Read(ref _pendingCount) == 0) return;

            Batch.Clear();
            lock (PendingLock)
            {
                Batch.AddRange(Pending);
                Pending.Clear();
                Volatile.Write(ref _pendingCount, 0);
            }

            // Nobody to tell (single player, or a host that is alone): drop the batch. Players that join later get
            // the full list from the join data instead.
            if (!NetworkServer.HasClients()) return;

            try
            {
                _sender?.Invoke(Batch);
            }
            catch (Exception exception)
            {
                DeadlyElectricity.LogModMessage(LogLevel.Warning,
                    $"Could not send generator switch changes to the clients: {exception.Message}");
            }
        }

        /// <summary>
        /// Applies a batch received from the host. Unknown or already destroyed generators are skipped.
        /// </summary>
        internal static void ApplyFromHost(List<Change> changes)
        {
            foreach (Change change in changes)
            {
                Thing thing = Referencable.Find<Thing>(change.ReferenceId);
                if (thing != null && GeneratorSwitch.IsSwitchable(thing))
                {
                    GeneratorSwitch.SetDisabled(thing, change.Disabled, notifyNetwork: false);
                }
            }
        }
    }
}
