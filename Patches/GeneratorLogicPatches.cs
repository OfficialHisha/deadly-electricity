using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using DeadlyElectricity.Switching;
using HarmonyLib;
using System;

namespace DeadlyElectricity.Patches
{
    /// <summary>
    /// Exposes the standard <see cref="LogicType.On"/> logic type on generators that do not have one natively
    /// (see <see cref="GeneratorSwitch.IsSwitchable"/>).
    ///
    /// <para>
    /// <b>Why patch <see cref="Device"/> and not each generator class?</b>
    /// None of these generators handle <c>On</c> themselves; every override ends with a call to
    /// <c>base.XXX(logicType)</c>, which lands in <see cref="Device"/>. Patching the four base methods once
    /// therefore covers every generator and every size variant (small/large wind turbine, all solar panels...).
    /// </para>
    ///
    /// <para>
    /// <b>Why this also fixes the Stationpedia.</b> The Stationpedia builds the "Logic" table of each page by calling
    /// <c>CanLogicRead</c>/<c>CanLogicWrite</c> on the prefab for every logic type. Because those calls are patched,
    /// <c>On</c> (Read/Write) is listed on the page of each switchable generator exactly like for any other device,
    /// and the generator also shows up on the Stationpedia page of the <c>On</c> logic type.
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> Each patch starts with <c>logicType != LogicType.On</c>, a single integer comparison, so the
    /// logic reads and writes of every other device and logic type are practically unaffected.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class GeneratorLogicPatches
    {
        /// <summary>Skips applying these patches when the feature is disabled in the configuration.</summary>
        [HarmonyPrepare]
        private static bool Prepare() => GeneratorSwitch.Enabled;

        /// <summary>The generator can be READ for <c>On</c> (1 = connected, 0 = disconnected).</summary>
        [HarmonyPatch(typeof(Device), nameof(Device.CanLogicRead), new[] { typeof(LogicType) })]
        [HarmonyPostfix]
        private static void CanLogicReadPostfix(Device __instance, LogicType logicType, ref bool __result)
        {
            if (logicType != LogicType.On || __result) return;

            if (GeneratorSwitch.IsSwitchable(__instance))
            {
                __result = true;
            }
        }

        /// <summary>The generator can be WRITTEN for <c>On</c>.</summary>
        [HarmonyPatch(typeof(Device), nameof(Device.CanLogicWrite), new[] { typeof(LogicType) })]
        [HarmonyPostfix]
        private static void CanLogicWritePostfix(Device __instance, LogicType logicType, ref bool __result)
        {
            if (logicType != LogicType.On || __result) return;
            if (!GeneratorSwitch.IsSwitchable(__instance)) return;

            // Same guard the base game uses: a structure that is still being built cannot be written to while the
            // game is running. (Outside of a running game, e.g. while the Stationpedia is generated from the
            // prefabs, the guard does not apply, which is how the base game lists its own logic types.)
            if (!__instance.IsStructureCompleted && GameManager.GameState == GameState.Running) return;

            __result = true;
        }

        /// <summary>Reports the current <c>On</c> value: 1 when connected, 0 when switched off.</summary>
        [HarmonyPatch(typeof(Device), nameof(Device.GetLogicValue), new[] { typeof(LogicType) })]
        [HarmonyPostfix]
        private static void GetLogicValuePostfix(Device __instance, LogicType logicType, ref double __result)
        {
            if (logicType != LogicType.On) return;

            if (GeneratorSwitch.IsSwitchable(__instance))
            {
                __result = GeneratorSwitch.IsDisabled(__instance) ? 0d : 1d;
            }
        }

        /// <summary>
        /// Applies a written <c>On</c> value.
        /// This is a prefix that skips the original: the base implementation would try to interact with the
        /// generator's (non-existent) On/Off interactable and fail.
        /// </summary>
        [HarmonyPatch(typeof(Device), nameof(Device.SetLogicValue), new[] { typeof(LogicType), typeof(double) })]
        [HarmonyPrefix]
        private static bool SetLogicValuePrefix(Device __instance, LogicType logicType, double value)
        {
            if (logicType != LogicType.On || !GeneratorSwitch.IsSwitchable(__instance)) return true;

            // The base game ignores writes to unfinished structures; do the same.
            if (__instance.IsStructureCompleted)
            {
                // Same value handling as the base game: the value is clamped to 0..1 and truncated, so only a
                // value of 1 (or more) means "on".
                bool turnOn = (int)Math.Clamp(value, 0d, 1d) == 1;
                GeneratorSwitch.SetDisabled(__instance, !turnOn);
            }

            return false;
        }
    }
}
