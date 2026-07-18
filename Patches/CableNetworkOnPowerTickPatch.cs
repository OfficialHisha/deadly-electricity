using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using BepInEx.Logging;
using DeadlyElectricity.Configuration;
using DeadlyElectricity.Extensions;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace DeadlyElectricity.Patches
{
    [HarmonyPatch(typeof(CableNetwork), nameof(CableNetwork.OnPowerTick))]
    internal class CableNetworkOnPowerTickPatch
    {
        private static readonly string[] _excludedGenerators = ConfigurationHelper.GetString("ExcludedPowerSuppliers").Split(',');

        [HarmonyPrefix]
        private static bool Prefix(CableNetwork __instance)
        {
            float energyToBurn = __instance.CurrentLoad * 0.01f * ConfigurationHelper.GetFloat("PowerShareConvertedPerCable");
            DeadlyElectricity.LogModMessage(LogLevel.Debug, $"Emitting heat at {energyToBurn} per cable");

            foreach (Cable cable in __instance.CableList)
            {
                AtmosphericsManagerThingAtmosphereTickPatch.cableHeats.Add(new(cable.WorldGrid, energyToBurn));
            }

            __instance.PotentialLoad -= energyToBurn;

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(CableNetwork __instance)
        {
            DeadlyElectricity.LogModMessage(LogLevel.Debug, $"Power: {__instance.PotentialLoad}:{__instance.RequiredLoad}");
            int damageRoll = Random.Range(0, (int)__instance.PotentialLoad);
            DeadlyElectricity.LogModMessage(LogLevel.Debug, $"Damage roll: {damageRoll} passed? {damageRoll > __instance.RequiredLoad + ConfigurationHelper.GetInt("WattsThresholdForGeneratorDamage")}");
            if (damageRoll > __instance.RequiredLoad + ConfigurationHelper.GetInt("WattsThresholdForGeneratorDamage"))
            {
                PowerProvider powerProvider = __instance.PowerTick.Providers.Where(static provider => !_excludedGenerators.Contains(provider.Device.PrefabName)).ToList().Pick();

                if (powerProvider == null) return;

                Device generatorDevice = powerProvider.Device;
                DeadlyElectricity.LogModMessage(LogLevel.Debug, $"Device.PrefabName: {generatorDevice.PrefabName}");

                generatorDevice.DamageState.Damage(ChangeDamageType.Increment, ConfigurationHelper.GetFloat("DamageRate"), DamageUpdateType.Brute);
                DeadlyElectricity.LogModMessage(LogLevel.Debug, "Applied power damage");

                if (generatorDevice.DamageState.Brute > generatorDevice.DamageState.MaxDamage * ConfigurationHelper.GetFloat("ShortCircuitThreshold"))
                {
                    float fireRoll = Random.Range(0, generatorDevice.DamageState.MaxDamage);
                    DeadlyElectricity.LogModMessage(LogLevel.Debug, $"Damage roll: {fireRoll} passed? {fireRoll < generatorDevice.DamageState.Brute}");
                    if (fireRoll < generatorDevice.DamageState.Brute)
                    {
                        DeadlyElectricity.LogModMessage(LogLevel.Debug, "Catching fire");
                        generatorDevice.CatchFire();
                    }
                }
            }
        }
    }
}
