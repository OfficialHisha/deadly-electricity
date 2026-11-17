using Assets.Scripts.Objects;
using DeadlyElectricity.Switching;
using HarmonyLib;

namespace DeadlyElectricity.Patches
{
    /// <summary>
    /// Keeps the switched-off state of generators across save and load, without any per-generator cost.
    ///
    /// <para>
    /// <b>How.</b> The base game stores a list of <c>States</c> (name/value pairs) for every thing in the save
    /// file. When loading, it applies the names it knows and ignores the rest. A switched-off generator simply
    /// adds one extra entry, <see cref="GeneratorSwitch.SaveStateName"/>, and the load patch reads it back.
    /// Generators that are switched on write nothing, so existing saves and farms of running generators are
    /// not changed at all, and a save made with this mod still loads without it (the unknown entry is ignored).
    /// </para>
    ///
    /// <para>
    /// <b>Why patch <c>Thing</c>.</b> Generators either override the save methods and call <c>base</c>, or do not
    /// override them, so patching the <c>Thing</c> implementation sees every generator with one patch.
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> Only incurred while saving or loading. Saving: one integer check per thing, and work only for
    /// switched-off generators. Loading: a cheap type check per thing, and a scan of a very short list only for
    /// switchable generators.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class GeneratorPersistencePatches
    {
        /// <summary>Skips applying these patches when the feature is disabled in the configuration.</summary>
        [HarmonyPrepare]
        private static bool Prepare() => GeneratorSwitch.Enabled;

        /// <summary>Save: write the marker for a switched-off generator.</summary>
        // InitialiseSaveData is protected in the game, so it is addressed by name.
        [HarmonyPatch(typeof(Thing), "InitialiseSaveData")]
        [HarmonyPostfix]
        private static void SavePostfix(Thing __instance, ref ThingSaveData savedData)
        {
            if (!GeneratorSwitch.AnyDisabled || savedData?.States == null) return;

            if (GeneratorSwitch.IsDisabled(__instance))
            {
                savedData.States.Add(new InteractableState { StateName = GeneratorSwitch.SaveStateName, State = 1 });
            }
        }

        /// <summary>Load: restore the switched-off state if the marker is present.</summary>
        [HarmonyPatch(typeof(Thing), nameof(Thing.DeserializeSave))]
        [HarmonyPrefix]
        private static void LoadPrefix(Thing __instance, ThingSaveData saveData)
        {
            if (!GeneratorSwitch.IsSwitchable(__instance) || saveData?.States == null) return;

            foreach (InteractableState state in saveData.States)
            {
                if (state.StateName == GeneratorSwitch.SaveStateName)
                {
                    GeneratorSwitch.SetDisabled(__instance, state.State == 1, notifyNetwork: false);
                    return;
                }
            }
        }
    }
}
