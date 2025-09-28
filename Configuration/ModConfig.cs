using BepInEx.Configuration;

namespace DeadlyElectricity.Configuration
{
    internal static class ModConfig
    {
        internal static void LoadConfiguration(this ConfigFile config)
        {
            config.AddConfig("General", "DebugEnabled", false, "Enable debug output in the bepinex console");
            config.AddConfig("General", "ExcludedPowerSuppliers", "StructureBattery,StructureBatterySmall,StructureBatteryMedium,StructureBatteryLarge,StructureAreaPowerControl,StructureAreaPowerControlReversed,StructureTransformer,StructureTransformerSmall,StructureTransformerSmallReversed,StructureTransformerMedium,StructureTransformerMedium(Reversed),StructureRocketTransformerSmall", "Power suppliers excluded from taking damage from oversupply of power");
            config.AddConfig("Power", "powerShareConvertedPerCable", 0.001f, "The share of energy in the network that is converted to heat per cable in the network");
            config.AddConfig("Power", "MaximumSafeWatts", 100, "The watts threshold at which a live cable is always safe to cut");
            config.AddConfig("Power", "WattsThresholdForHumanDamage", 250, "The watts threshold at which humans without a suit will be damaged by cutting a live cable");
            config.AddConfig("Power", "WattsThresholdForSuitDamage", 500, "The watts threshold at which a suit will be damaged if worn while cutting a live cable");
            config.AddConfig("Power", "WattsThresholdForCableDestroyed", 1000, "The watts threshold at which a live cable will blow when cut");
            config.AddConfig("Damage", "WattsThresholdForGeneratorDamage", 100, "The watts threshold the potential power of a cable network has to be above the required load in order to damage power generating structures on the network");
            config.AddConfig("Damage", "DamageRate", 0.1f, "How much damage a generator will take if it is selected to take damage due to overproduction of power on a network");
            config.AddConfig("Damage", "ShortCircuitThreshold", 0.25f, "If a generators health is below this percentage of its total health it will have a chance to catch fire next time it takes damage");
        }
    }
}
