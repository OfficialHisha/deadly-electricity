using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using DeadlyElectricity.Configuration;
using Objects;

namespace DeadlyElectricity.Switching
{
    /// <summary>
    /// Central state and helpers for the "generator switch" feature.
    ///
    /// <para>
    /// <b>What the feature does.</b> Several power generators in Stationeers have no On/Off state of their own:
    /// solar panels, wind turbines (both sizes), the gas (pressure) turbine and the RTG. They always push
    /// whatever they produce into the cable network, so a circuit cannot protect them from the damage
    /// this mod applies when a network is over-supplied.
    /// This feature gives those generators the standard <c>On</c> logic type, so an IC10 program (or any
    /// logic device) can write <c>s device On 0</c> to disconnect the generator from the grid and
    /// <c>s device On 1</c> to connect it again. Think of it as the generator circuit breaker of a real power plant.
    /// </para>
    ///
    /// <para>
    /// <b>Generators that already have a native On/Off</b> (Stirling engine, gas fuel generator, solid fuel
    /// generator) are deliberately left alone: for them "off" really stops the process (fuel is no longer burned),
    /// which is more realistic than a breaker, and the base game already handles logic, saving and the Stationpedia.
    /// </para>
    ///
    /// <para>
    /// <b>Performance design.</b>
    /// <list type="bullet">
    /// <item>No component, no <c>Update</c> loop and no field is added to any generator instance.</item>
    /// <item>Only generators that are currently OFF are stored (in a <see cref="ConditionalWeakTable{TKey,TValue}"/>),
    /// so a farm with thousands of running generators costs no memory at all.</item>
    /// <item>The table is keyed by the generator object itself, so entries disappear automatically when the
    /// generator is destroyed (deconstructed, world unloaded) and a reference id of a previous save can never leak
    /// into a new one.</item>
    /// <item>Reads are lock-free and are safe from the power-tick thread. A volatile counter lets the common
    /// "nothing is switched off" case return after a single integer comparison.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class GeneratorSwitch
    {
        /// <summary>
        /// Name of the entry written into the save file (<c>ThingSaveData.States</c>) for a switched-off generator.
        /// The base game silently ignores state names it does not know, so saves stay loadable if the mod is removed.
        /// </summary>
        internal const string SaveStateName = "DeadlyElectricity.GeneratorDisabled";

        /// <summary>
        /// Master switch from the BepInEx configuration (<c>GeneratorSwitch.EnableGeneratorSwitch</c>).
        /// It is read once, when the patches are applied; changing it requires a game restart.
        /// </summary>
        internal static readonly bool Enabled = ConfigurationHelper.GetBool("EnableGeneratorSwitch");

        // Generators that are currently switched OFF. The value is irrelevant, only the presence of the key matters.
        private static readonly ConditionalWeakTable<Thing, object> Disabled = new();
        private static readonly object Marker = new();

        // Serialises writers only (toggling is rare). Readers never take this lock.
        private static readonly object WriteLock = new();

        // Number of entries added minus entries removed explicitly. It only drives the fast path in IsDisabled:
        // it can stay above the real count if a switched-off generator is destroyed, which merely means the fast
        // path is skipped and the (still cheap) table lookup is used. It never makes a result incorrect.
        private static int _disabledCount;

        /// <summary>
        /// Prefab names of the basic generators that are deliberately NOT given a switch (see
        /// <see cref="BuildRestrictedPrefabs"/>). Empty when the restriction is turned off. Built once.
        /// </summary>
        private static readonly HashSet<string> RestrictedPrefabs = BuildRestrictedPrefabs();

        /// <summary>
        /// Reads the "basic generators cannot be switched" rule from the configuration.
        ///
        /// <para>
        /// <b>Why it exists.</b> Basic generators (the upright wind turbine and the basic solar panels) have no data
        /// port, so a circuit could not reach them anyway. Leaving them out on purpose makes the advanced models,
        /// which do have a data port, the ones worth building when you want a generator that can be controlled.
        /// The rule is on by default; <c>GeneratorSwitch.RestrictBasicGenerators</c> turns it off and
        /// <c>GeneratorSwitch.RestrictedPrefabs</c> is the editable list of prefab names it applies to.
        /// </para>
        /// </summary>
        private static HashSet<string> BuildRestrictedPrefabs()
        {
            var restricted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!ConfigurationHelper.GetBool("RestrictBasicGenerators")) return restricted;

            foreach (string name in ConfigurationHelper.GetString("RestrictedPrefabs").Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0)
                {
                    restricted.Add(trimmed);
                }
            }

            return restricted;
        }

        /// <summary>
        /// True when <paramref name="thing"/> is a generator that this mod adds an On/Off switch to.
        ///
        /// <list type="bullet">
        /// <item>It must be one of the generator types without a native On/Off. The <c>HasOnOffState</c> check also
        /// makes the mod step aside if the game (or another mod) gives the generator one in the future.</item>
        /// <item>Its prefab must not be in the restricted list (the basic generators, by default). Restricted
        /// generators behave exactly as in the base game: no <c>On</c> logic type, nothing to save, no tooltip line.</item>
        /// </list>
        /// </summary>
        internal static bool IsSwitchable(Thing thing)
        {
            return thing is SolarPanel or WindTurbineGenerator or TurbineGenerator or RadioscopicThermalGenerator
                   && !thing.HasOnOffState
                   && (RestrictedPrefabs.Count == 0 || !RestrictedPrefabs.Contains(thing.PrefabName));
        }

        /// <summary>
        /// True when the generator has been switched off. Safe to call from any thread, allocation free.
        /// </summary>
        internal static bool IsDisabled(Thing thing)
        {
            // Fast path: no generator is switched off anywhere, which is the normal case.
            if (Volatile.Read(ref _disabledCount) == 0)
            {
                return false;
            }

            return Disabled.TryGetValue(thing, out _);
        }

        /// <summary>
        /// Switches the generator off (<paramref name="disabled"/> = true) or on.
        /// Does nothing if the generator is already in the requested state.
        /// </summary>
        /// <param name="notifyNetwork">
        /// True when the change originates on this machine (a logic write) and must be announced to the clients.
        /// False when it is being applied from a save file or received from the host, so it is never echoed back.
        /// </param>
        internal static void SetDisabled(Thing thing, bool disabled, bool notifyNetwork = true)
        {
            lock (WriteLock)
            {
                bool isDisabled = Disabled.TryGetValue(thing, out _);
                if (isDisabled == disabled)
                {
                    return;
                }

                if (disabled)
                {
                    Disabled.Add(thing, Marker);
                    Interlocked.Increment(ref _disabledCount);
                }
                else
                {
                    Disabled.Remove(thing);
                    Interlocked.Decrement(ref _disabledCount);
                }
            }

            // Outside the lock: the sync layer only queues the change, it never calls back into this class.
            if (notifyNetwork)
            {
                GeneratorSwitchSync.NotifyChanged(thing, disabled);
            }
        }

        /// <summary>
        /// Adds the reference id of every generator that is currently switched off to <paramref name="ids"/>.
        /// Only used when a player joins, never on a hot path.
        /// </summary>
        internal static void CollectDisabledIds(List<long> ids)
        {
            if (!AnyDisabled) return;

            lock (WriteLock)
            {
                foreach (KeyValuePair<Thing, object> entry in Disabled)
                {
                    Thing thing = entry.Key;
                    if (thing != null && thing.ReferenceId != 0L)
                    {
                        ids.Add(thing.ReferenceId);
                    }
                }
            }
        }

        /// <summary>
        /// True when at least one generator may be switched off. Used by the save patch to skip all work in the
        /// common case.
        /// </summary>
        internal static bool AnyDisabled => Volatile.Read(ref _disabledCount) > 0;
    }
}
