using Assets.Scripts.Objects;
using DeadlyElectricity.Switching;
using HarmonyLib;

namespace DeadlyElectricity.Patches
{
    /// <summary>
    /// The three hooks of the multiplayer sync of the generator switch state. See <see cref="GeneratorSwitchSync"/>
    /// for the whole design; in short, the host puts a state bit in the "what changed" mask of a generator's update and
    /// a client with the mod reads it. Nothing is sent behind the bit, so players without the mod are unaffected.
    ///
    /// <para>
    /// All three patch <c>Thing</c>, because the generator classes either call <c>base</c> in these methods or do
    /// not override them, so every generator goes through the <c>Thing</c> implementation.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class GeneratorSyncPatches
    {
        /// <summary>Skips applying these patches when the feature is disabled in the configuration.</summary>
        [HarmonyPrepare]
        private static bool Prepare() => GeneratorSwitch.Enabled;

        /// <summary>
        /// Host: the game calls this for each changed object right before writing its mask. When the mask carries our
        /// bits (because the generator was switched, was refreshed, or the game marked everything for a joining player)
        /// they are replaced by the single bit of its real state. The other bits are never touched, and other classes
        /// that use these two bits for something of their own are skipped by the generator check.
        /// </summary>
        [HarmonyPatch(typeof(Thing), nameof(Thing.VerifyUpdateType))]
        [HarmonyPostfix]
        private static void VerifyUpdateTypePostfix(Thing thing, ref ushort networkUpdateType)
        {
            if ((networkUpdateType & GeneratorSwitchSync.Mask) == 0) return;
            if (!GeneratorSwitch.IsSwitchable(thing)) return;

            networkUpdateType = GeneratorSwitchSync.WithState(networkUpdateType, GeneratorSwitch.IsDisabled(thing));
        }

        /// <summary>
        /// Host: the game builds its batch of updates through this method. Used as a cheap clock for the periodic
        /// refresh of the switched-off generators, so no extra update loop is needed.
        /// </summary>
        [HarmonyPatch(typeof(Thing), nameof(Thing.SerializeDeltaState))]
        [HarmonyPrefix]
        private static void SerializeDeltaStatePrefix()
        {
            GeneratorSwitchSync.RefreshIfDue();
        }

        /// <summary>
        /// Client: reads the state bit of an update it received. A reader is never touched, as there are no bytes
        /// behind the bits. The first test makes this one mask comparison for every update of every other object.
        /// </summary>
        [HarmonyPatch(typeof(Thing), nameof(Thing.ProcessUpdate))]
        [HarmonyPostfix]
        private static void ProcessUpdatePostfix(Thing __instance, ushort networkUpdateType)
        {
            if ((networkUpdateType & GeneratorSwitchSync.Mask) == 0) return;

            GeneratorSwitchSync.ApplyReceivedState(__instance, networkUpdateType);
        }
    }
}
