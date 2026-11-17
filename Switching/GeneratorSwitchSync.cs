using System;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using UnityEngine;

namespace DeadlyElectricity.Switching
{
    /// <summary>
    /// Multiplayer synchronisation of the generator switch state (host to clients), using only what the base game
    /// already sends.
    ///
    /// <para>
    /// <b>What it is for.</b> The host decides everything (power, damage, IC10, saving). Clients only need to know
    /// which generators are switched off so that they <i>look</i> right: wind turbine blades and sound stop, the
    /// tooltip line shows, and reading <c>On</c> returns the real value. Without it a client simply keeps seeing a
    /// switched-off wind turbine spinning; nothing else is affected.
    /// </para>
    ///
    /// <para>
    /// <b>How it works.</b> For every object that changed, the game sends a 16-bit "what changed" mask followed by the
    /// data for each bit that is set. Each class only reads the bits it knows and ignores the others. This feature
    /// uses two bits that none of the generator classes use, <see cref="OffBit"/> and <see cref="OnBit"/>, and
    /// <b>no data at all</b> behind them: exactly one of them being set means "this generator is now off" or "now on".
    /// </para>
    ///
    /// <para>
    /// <b>Why this is safe for everybody.</b>
    /// <list type="bullet">
    /// <item>A player without the mod ignores the two bits, and since no bytes follow them the rest of the update is
    /// read exactly as before. They can join any server and the server can have the mod without requiring anything
    /// from them; they only miss the visual feedback.</item>
    /// <item>A player with the mod on a server without the mod simply never receives the bits.</item>
    /// <item>There is no setting, no handshake and no extra library: the host always writes the bits and a client with
    /// the mod always reads them.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>When the bits are sent.</b>
    /// <list type="bullet">
    /// <item>When a generator is switched, the host marks it as changed, so it is in the next update.</item>
    /// <item>When a player joins, the game marks every object as fully changed, which sets all 16 bits. Just before
    /// writing, <see cref="WithState"/> replaces the two bits of a generator by the single bit of its real state, so
    /// the joining client learns it. A client ignores an update where both or none of the bits are set.</item>
    /// <item>Every <see cref="RefreshSeconds"/> seconds, while at least one generator is switched off, the host marks
    /// the switched-off generators again, so a client that missed an update catches up. This only visits the
    /// generators that are off, which are the only ones the mod tracks.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> No bytes beyond the object id and mask the game sends for any changed object, and only for a
    /// generator that was switched. On the host every update does one mask test; on a client every received update
    /// does one mask test. Nothing runs while nobody uses the feature.
    /// </para>
    /// </summary>
    internal static class GeneratorSwitchSync
    {
        /// <summary>Set (alone) when the generator is switched off. Unused by every generator class of the game.</summary>
        internal const ushort OffBit = 0x4000;

        /// <summary>Set (alone) when the generator is switched on. Unused by every generator class of the game.</summary>
        internal const ushort OnBit = 0x8000;

        /// <summary>Both bits together.</summary>
        internal const ushort Mask = OffBit | OnBit;

        /// <summary>How often the switched-off generators are announced again to the clients.</summary>
        internal const float RefreshSeconds = 30f;

        // Next time (Time.realtimeSinceStartup) the periodic refresh is due.
        private static float _nextRefresh;

        // Cached so that the refresh does not allocate a delegate every time.
        private static readonly Action<Thing> MarkOff = static thing => thing.NetworkUpdateFlags |= OffBit;

        /// <summary>
        /// Returns <paramref name="updateType"/> with the two sync bits replaced by the single bit that describes the
        /// current state. Every other bit is left untouched.
        /// </summary>
        internal static ushort WithState(ushort updateType, bool disabled)
        {
            return (ushort)((updateType & ~Mask) | (disabled ? OffBit : OnBit));
        }

        /// <summary>
        /// Reads the state carried by <paramref name="updateType"/>. Returns false when it carries none, which is the
        /// case when neither bit is set (the generator did not change) or both are (an update that was not normalised).
        /// </summary>
        internal static bool TryReadState(ushort updateType, out bool disabled)
        {
            bool off = (updateType & OffBit) != 0;
            bool on = (updateType & OnBit) != 0;
            disabled = off;
            return off != on;
        }

        /// <summary>True when this machine is a host that currently has players connected.</summary>
        private static bool HostWithClients()
        {
            return NetworkManager.IsServer && NetworkServer.HasClients();
        }

        /// <summary>
        /// Called on the host right after a generator was switched: marks it as changed so the next update carries its
        /// state. Does nothing in single player or when nobody is connected (a player who joins later learns the state
        /// from the update the game sends for everything at that moment).
        /// </summary>
        internal static void NotifyChanged(Thing thing, bool disabled)
        {
            if (!HostWithClients()) return;

            thing.NetworkUpdateFlags |= disabled ? OffBit : OnBit;
        }

        /// <summary>
        /// Called on the host every time the game builds a batch of updates. Once every
        /// <see cref="RefreshSeconds"/> seconds it announces the switched-off generators again; the rest of the time it
        /// is a single integer test.
        /// </summary>
        internal static void RefreshIfDue()
        {
            if (!GeneratorSwitch.AnyDisabled) return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextRefresh) return;

            _nextRefresh = now + RefreshSeconds;
            if (!HostWithClients()) return;

            GeneratorSwitch.ForEachDisabled(MarkOff);
        }

        /// <summary>
        /// Called on a client for every update it receives that has one of the sync bits. Applies the state if the
        /// object is a switchable generator and the update carries exactly one state bit.
        /// </summary>
        internal static void ApplyReceivedState(Thing thing, ushort updateType)
        {
            // Only a client applies received state. The host is the source of truth and never obeys an update.
            if (NetworkManager.IsServer) return;
            if (!GeneratorSwitch.IsSwitchable(thing)) return;
            if (!TryReadState(updateType, out bool disabled)) return;

            GeneratorSwitch.SetDisabled(thing, disabled, notifyNetwork: false);
        }
    }
}
