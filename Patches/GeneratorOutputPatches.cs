using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using DeadlyElectricity.Switching;
using HarmonyLib;
using Objects;

namespace DeadlyElectricity.Patches
{
    /// <summary>
    /// Makes a switched-off generator deliver no power ("generator circuit breaker open").
    ///
    /// <para>
    /// A generator that returns 0 from <c>GetGeneratedPower</c> is not added to the list of power providers of its
    /// cable network. As a consequence a switched-off generator no longer counts as potential power, and, since
    /// <see cref="CableNetworkOnPowerTickPatch"/> only picks its victims from that list, it can no longer be
    /// damaged by over-supply. That is the whole point of switching a generator off.
    /// </para>
    ///
    /// <para>
    /// Each generator type is patched at the single place that best reflects reality for that machine, and
    /// the machine itself keeps behaving physically as it would with an open breaker:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Solar panel</b> - patched at <c>GenerationRate</c>, which is used by the power tick, the logic
    /// readings and the tooltip, so all of them report 0 W. The panel is otherwise untouched (no visual change,
    /// the arms keep tracking the sun), exactly like a real panel behind an open isolator.</item>
    /// <item><b>Wind turbine</b> (small and large) - patched at <c>CalculateGenerationRate</c>. The turbine uses the
    /// same value to drive its blades and its sound, so a switched-off turbine stops spinning, like a real one
    /// that is feathered and braked.</item>
    /// <item><b>Gas (pressure) turbine</b> - only the electrical output is cut. The turbine keeps spinning and
    /// letting gas through, because that flow is part of the atmosphere of the base and a disconnected
    /// real turbine also keeps turning without load. Changing the gas flow would silently break existing builds.</item>
    /// <item><b>RTG</b> - a real RTG cannot be stopped, so only its output is disconnected from the network.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Cost.</b> The power tick runs on its own thread, and these patches are read-only lock-free lookups
    /// (<see cref="GeneratorSwitch.IsDisabled"/>). When no generator is switched off the cost is one integer comparison.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class GeneratorOutputPatches
    {
        /// <summary>Skips applying these patches when the feature is disabled in the configuration.</summary>
        [HarmonyPrepare]
        private static bool Prepare() => GeneratorSwitch.Enabled;

        /// <summary>Solar panel: output 0 W while switched off. Skipping the original also saves its computation.</summary>
        [HarmonyPatch(typeof(SolarPanel), nameof(SolarPanel.GenerationRate), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool SolarGenerationRatePrefix(SolarPanel __instance, ref float __result)
        {
            if (!GeneratorSwitch.IsDisabled(__instance)) return true;

            __result = 0f;
            return false;
        }

        /// <summary>Wind turbines: no generation, no blade movement and no sound while switched off.</summary>
        [HarmonyPatch(typeof(WindTurbineGenerator), nameof(WindTurbineGenerator.CalculateGenerationRate))]
        [HarmonyPrefix]
        private static bool WindCalculateGenerationRatePrefix(WindTurbineGenerator __instance, ref float __result)
        {
            if (!GeneratorSwitch.IsDisabled(__instance)) return true;

            __result = 0f;
            return false;
        }

        /// <summary>Gas turbine: electrical output is cut, the turbine itself keeps working (see class summary).</summary>
        [HarmonyPatch(typeof(TurbineGenerator), nameof(TurbineGenerator.GetGeneratedPower))]
        [HarmonyPostfix]
        private static void TurbineGetGeneratedPowerPostfix(TurbineGenerator __instance, ref float __result)
        {
            if (GeneratorSwitch.IsDisabled(__instance))
            {
                __result = 0f;
            }
        }

        /// <summary>
        /// Gas turbine: the <c>PowerGeneration</c> logic reading reports the value that was cached from the
        /// pressure difference, so make it agree with the (zero) power actually delivered to the network.
        /// </summary>
        [HarmonyPatch(typeof(TurbineGenerator), nameof(TurbineGenerator.GetLogicValue), new[] { typeof(LogicType) })]
        [HarmonyPostfix]
        private static void TurbineGetLogicValuePostfix(TurbineGenerator __instance, LogicType logicType, ref double __result)
        {
            if (logicType == LogicType.PowerGeneration && GeneratorSwitch.IsDisabled(__instance))
            {
                __result = 0d;
            }
        }

        /// <summary>RTG: output disconnected while switched off.</summary>
        [HarmonyPatch(typeof(RadioscopicThermalGenerator), nameof(RadioscopicThermalGenerator.GetGeneratedPower))]
        [HarmonyPostfix]
        private static void RtgGetGeneratedPowerPostfix(RadioscopicThermalGenerator __instance, ref float __result)
        {
            if (GeneratorSwitch.IsDisabled(__instance))
            {
                __result = 0f;
            }
        }
    }
}
