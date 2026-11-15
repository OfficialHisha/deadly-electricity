using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Assets.Scripts.Networking;
using LaunchPadBooster;
using LaunchPadBooster.Networking;

namespace DeadlyElectricity.Switching
{
    /// <summary>
    /// The only place that touches LaunchPadBooster types.
    ///
    /// <para>
    /// It is kept apart (and only entered through <see cref="TryInitialize"/>, which is marked
    /// <c>NoInlining</c>) so that LaunchPadBooster.dll is not loaded at all unless the multiplayer sync is
    /// enabled. If the library is missing, the exception is caught by the caller and the rest of the mod is
    /// unaffected.
    /// </para>
    ///
    /// <para>
    /// <b>Compatibility rules this class follows</b>
    /// <list type="bullet">
    /// <item>The mod is <i>not</i> marked as required, and its version check only compares the major version, so
    /// players on slightly different releases can still join each other.</item>
    /// <item>Every payload starts with a protocol byte. A payload with a protocol this version does not know is
    /// ignored (the packet is length-delimited, so the unread bytes cannot affect anything else).</item>
    /// <item>Only the host sends changes, and a host never applies a message it receives, so a client cannot
    /// switch generators on the host.</item>
    /// <item>Nothing here is allowed to throw into the game's networking code: serialisation and processing are
    /// wrapped, and a failure results in "no sync", never in a failed join.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class LaunchPadLink
    {
        /// <summary>Layout version of the payloads below. Bump when the layout changes.</summary>
        internal const byte Protocol = 1;

        // Safety limit on the number of entries accepted from the network.
        private const int MaxEntries = 1 << 20;

        private static Mod _mod;

        /// <summary>Registers the mod, its message and its join data with LaunchPadBooster.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryInitialize(string modVersion)
        {
            _mod = new Mod("DeadlyElectricity", modVersion);

            IModNetworking networking = _mod.Networking;
            networking.Required = false;
            networking.VersionValidator = new MajorVersionValidator();
            networking.RegisterMessage<GeneratorStateMessage>();
            networking.JoinSuffixSerializer = new JoinSerializer();

            return true;
        }

        /// <summary>Sends a batch of changes to every connected client.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Send(List<GeneratorSwitchSync.Change> changes)
        {
            new GeneratorStateMessage { Changes = new List<GeneratorSwitchSync.Change>(changes) }.SendAll(-1L);
        }

        /// <summary>Writes a list of changes. Shared by the update message and the join data.</summary>
        private static void Write(RocketBinaryWriter writer, List<GeneratorSwitchSync.Change> changes)
        {
            writer.WriteByte(Protocol);
            writer.WriteInt32(changes.Count);
            foreach (GeneratorSwitchSync.Change change in changes)
            {
                writer.WriteInt64(change.ReferenceId);
                writer.WriteBoolean(change.Disabled);
            }
        }

        /// <summary>Reads a list of changes; returns false (and an empty list) for an unknown or damaged payload.</summary>
        private static bool TryRead(RocketBinaryReader reader, List<GeneratorSwitchSync.Change> changes)
        {
            try
            {
                if (reader.ReadByte() != Protocol) return false;

                int count = reader.ReadInt32();
                if (count < 0 || count > MaxEntries) return false;

                for (int i = 0; i < count; i++)
                {
                    long referenceId = reader.ReadInt64();
                    bool disabled = reader.ReadBoolean();
                    changes.Add(new GeneratorSwitchSync.Change(referenceId, disabled));
                }

                return true;
            }
            catch (Exception)
            {
                changes.Clear();
                return false;
            }
        }

        /// <summary>
        /// "These generators changed state" - host to clients, sent when a generator is toggled.
        /// </summary>
        internal sealed class GeneratorStateMessage : INetworkMessage
        {
            internal List<GeneratorSwitchSync.Change> Changes = new();

            public void Serialize(RocketBinaryWriter writer)
            {
                Write(writer, Changes);
            }

            public void Deserialize(RocketBinaryReader reader)
            {
                Changes = new List<GeneratorSwitchSync.Change>();
                TryRead(reader, Changes);
            }

            public void Process(long clientId)
            {
                // A host never obeys a remote switch request: it is the single source of truth.
                if (NetworkManager.IsServer || Changes.Count == 0) return;

                GeneratorSwitchSync.ApplyFromHost(Changes);
            }
        }

        /// <summary>
        /// Join data: the full list of switched-off generators, written once for a joining player.
        /// The game has already created every thing when this section is read, so the ids can be resolved.
        /// </summary>
        private sealed class JoinSerializer : IJoinSuffixSerializer
        {
            public void SerializeJoinSuffix(RocketBinaryWriter writer)
            {
                var changes = new List<GeneratorSwitchSync.Change>();
                try
                {
                    var ids = new List<long>();
                    GeneratorSwitch.CollectDisabledIds(ids);
                    foreach (long id in ids)
                    {
                        changes.Add(new GeneratorSwitchSync.Change(id, true));
                    }
                }
                catch (Exception)
                {
                    // Never break a join because of this optional data: send an empty list.
                    changes.Clear();
                }

                Write(writer, changes);
            }

            public void DeserializeJoinSuffix(RocketBinaryReader reader)
            {
                var changes = new List<GeneratorSwitchSync.Change>();
                if (TryRead(reader, changes) && changes.Count > 0 && !NetworkManager.IsServer)
                {
                    GeneratorSwitchSync.ApplyFromHost(changes);
                }
            }
        }

        /// <summary>
        /// Accepts any peer running the same major version, instead of the exact version LaunchPadBooster requires
        /// by default. The payloads carry their own protocol byte, so minor releases stay compatible.
        /// </summary>
        private sealed class MajorVersionValidator : IVersionValidator
        {
            public bool ValidateVersion(string version)
            {
                return Major(version) == Major(_mod.ID.Version);
            }

            private static string Major(string version)
            {
                if (string.IsNullOrEmpty(version)) return string.Empty;

                int dot = version.IndexOf('.');
                return dot < 0 ? version : version.Substring(0, dot);
            }
        }
    }
}
