using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Items;
using DeadlyElectricity.Configuration;
using HarmonyLib;

namespace DeadlyElectricity.Patches
{
    [HarmonyPatch(typeof(Item), nameof(Item.OnUseItem))]
    internal class ItemOnUseItemPatch
    {
        [HarmonyPostfix]
        private static void PostFix(Item __instance, ref bool __result, Thing useOnThing)
        {
            if (__instance is not WireCutter) return;

            Cable cable = useOnThing as Cable;
            if (cable != null && cable.CableNetwork.PotentialLoad > ConfigurationHelper.GetInt("MaximumSafeWatts"))
            {
                float loadDamage = cable.CableNetwork.PotentialLoad / 1000;

                if (cable.CableNetwork.PotentialLoad > ConfigurationHelper.GetInt("WattsThresholdForCableDestroyed"))
                {
                    cable.Break();
                } else
                {
                    WorldManager.Spark(__instance.ThingTransformPosition, 20, __instance.GridController.RoomController.GetRoom(__instance.WorldGrid) != null);
                }

                if (__instance.RootParentHuman.Suit != null && cable.CableNetwork.PotentialLoad > ConfigurationHelper.GetInt("WattsThresholdForSuitDamage"))
                {
                    __instance.RootParentHuman.Suit.AsThing.DamageState.Damage(ChangeDamageType.Increment, loadDamage, DamageUpdateType.Brute);
                }
                else if (cable.CableNetwork.PotentialLoad > ConfigurationHelper.GetInt("WattsThresholdForHumanDamage"))
                {
                    __instance.RootParentHuman.DamageState.Damage(ChangeDamageType.Increment, loadDamage, DamageUpdateType.Brute);
                }

                __instance.DamageState.Damage(ChangeDamageType.Increment, loadDamage, DamageUpdateType.Brute);
                OnServer.MoveToWorld(__instance, loadDamage);

                __result = false;
            }
        }
    }
}
