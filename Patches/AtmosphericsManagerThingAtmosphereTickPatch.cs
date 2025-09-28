using Assets.Scripts.Atmospherics;
using DeadlyElectricity.Util;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DeadlyElectricity.Patches
{
    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.ThingAtmosphereTick))]
    internal class AtmosphericsManagerThingAtmosphereTickPatch
    {
        internal static ConcurrentBag<CableHeatData> cableHeats = [];

        [HarmonyPostfix]
        private static void Postfix()
        {
            List<CableHeatData> snapshot = cableHeats.ToList();
            cableHeats = [];

            foreach (CableHeatData heatData in snapshot)
            {
                AtmosphericsManager.Find(heatData.worldGrid)?.GasMixture.AddEnergy(new(heatData.heatEnergy));
            }
        }
    }
}
