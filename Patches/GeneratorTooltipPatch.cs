using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using DeadlyElectricity.Switching;
using HarmonyLib;
using Objects;
using UnityEngine;

namespace DeadlyElectricity.Patches
{
    /// <summary>
    /// Adds a "disconnected" line to the hover tooltip of a switched-off generator, so the player can tell why it
    /// produces nothing.
    ///
    /// <para>
    /// Wind turbines already show it visually (the blades stop), but gas turbines and RTGs have no visual
    /// feedback, so the tooltip is the indicator. Solar panels are intentionally excluded: they keep their normal
    /// look and their tooltip already reports 0 W of generated power.
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> The tooltip is only built while the player looks at the generator, so there is no per-frame or
    /// per-generator cost. In multiplayer, a client that has the mod learns the state from the host (see
    /// <c>GeneratorSwitchSync</c>) and shows the line too; a client without the mod does not.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class GeneratorTooltipPatch
    {
        private const string DisconnectedLine = "<color=orange>Output disconnected by logic (On = 0)</color>";

        /// <summary>Skips applying this patch when the feature is disabled in the configuration.</summary>
        [HarmonyPrepare]
        private static bool Prepare() => GeneratorSwitch.Enabled;

        [HarmonyPatch(typeof(Device), nameof(Device.GetPassiveTooltip))]
        [HarmonyPostfix]
        private static void GetPassiveTooltipPostfix(Device __instance, Collider hitCollider, ref PassiveTooltip __result)
        {
            if (__instance is not (WindTurbineGenerator or TurbineGenerator or RadioscopicThermalGenerator)) return;
            if (!GeneratorSwitch.IsDisabled(__instance)) return;

            // When the player points at a pipe/cable connection the base game shows the connection's tooltip
            // instead of the device's; leave that one untouched.
            if (hitCollider != null && IsConnectionCollider(__instance, hitCollider)) return;

            __result.State = string.IsNullOrEmpty(__result.State)
                ? DisconnectedLine
                : __result.State + "\n" + DisconnectedLine;
        }

        private static bool IsConnectionCollider(Device device, Collider collider)
        {
            foreach (Connection openEnd in device.OpenEnds)
            {
                if (openEnd.Collider == collider)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
