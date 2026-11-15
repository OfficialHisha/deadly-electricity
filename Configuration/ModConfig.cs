using BepInEx.Configuration;

namespace DeadlyElectricity.Configuration
{
    internal static class ModConfig
    {
        internal static void LoadConfiguration(this ConfigFile config)
        {
            config.AddConfig("General", "DebugEnabled", false, "Enable debug output in the bepinex console");
            config.AddConfig("General", "ExcludedPowerSuppliers", "StructurePowerConnector,StructureBattery,StructureBatterySmall,StructureBatteryMedium,StructureBatteryLarge,StructureAreaPowerControl,StructureAreaPowerControlReversed,StructureTransformer,StructureTransformerSmall,StructureTransformerSmallReversed,StructureTransformerMedium,StructureTransformerMedium(Reversed),StructureRocketTransformerSmall,StructureCableFuse1k,StructureCableFuse5k,StructureCableFuse50k,StructureCableFuse100k,StructurePowerUmbilicalFemaleSide,StructurePowerUmbilicalFemale,SmartCircuitBreaker,SmartCircuitBreakerReversed,CircuitBreakerHeavy,SmallCircuitBreaker,SmallCircuitBreakerReversed", "Power suppliers excluded from taking damage from oversupply of power");
            config.AddConfig("Power", "PowerShareConvertedPerCable", 0.02f, "The share of energy in the network that is converted to heat per cable in the network");
            config.AddConfig("Power", "MaximumSafeWatts", 100, "The watts threshold at which a live cable is always safe to cut");
            config.AddConfig("Power", "WattsThresholdForHumanDamage", 250, "The watts threshold at which humans without a suit will be damaged by cutting a live cable");
            config.AddConfig("Power", "WattsThresholdForSuitDamage", 500, "The watts threshold at which a suit will be damaged if worn while cutting a live cable");
            config.AddConfig("Power", "WattsThresholdForCableDestroyed", 1000, "The watts threshold at which a live cable will blow when cut");
            config.AddConfig("Damage", "PercentageOfPowerConvertedToDamage", 0.001f, "The percentage rate of how much damage a shock will give based on the potential power of the network when cutting a live wire");
            config.AddConfig("Damage", "DamageMultiplerForShortCircuit", 0.5f, "The amount of damage per watt in a short circuit based on the PercentageOfPowerConvertedToDamage value");
            config.AddConfig("Damage", "WattsThresholdForGeneratorDamage", 100, "The watts threshold the potential power of a cable network has to be above the required load in order to damage power generating structures on the network");
            config.AddConfig("Damage", "DamageRate", 0.1f, "How much damage a generator will take if it is selected to take damage due to overproduction of power on a network");
            config.AddConfig("GeneratorSwitch", "EnableGeneratorSwitch", true, "Adds the standard 'On' logic type (readable and writable from IC10 and logic devices) to generators that cannot be switched off in the base game: solar panels, wind turbines, gas turbines and RTGs. Writing On = 0 disconnects the generator from the power network (it stops producing and can no longer be damaged by over-supply), On = 1 reconnects it. The state is saved with the game. Requires a restart to take effect.");
            config.AddConfig("GeneratorSwitch", "RestrictBasicGenerators", true, "When true, the basic generators listed in RestrictedPrefabs (the upright wind turbine and the basic solar panels) can NOT be switched off: they keep behaving exactly as in the base game. Advanced models (which have a data port) are then the ones worth building when you want a controllable generator. Set to false to allow every supported generator to be switched. Requires a restart to take effect.");
            config.AddConfig("GeneratorSwitch", "RestrictedPrefabs", "StructureUprightWindTurbine,StructureSolarPanelFlat,StructureSolarPanel45,StructureSolarPanelFlatReinforced,StructureSolarPanel45Reinforced", "Comma separated prefab names of the generators that cannot be switched off while RestrictBasicGenerators is true. Defaults to the upright wind turbine and the basic solar panels (flat and angled, normal and heavy). Dual and other solar panels, the other wind turbines, the gas turbine and the RTG are not restricted. Requires a restart to take effect.");
            config.AddConfig("GeneratorSwitch", "EnableNetworkSync", false, "Multiplayer only. Sends the switched-off state of generators from the host to the clients, so that wind turbines stop spinning and tooltips are correct on every client. Requires LaunchPadBooster (StationeersLaunchPad). WARNING: this changes the join handshake of the session, so the host AND every player must have this mod with this option enabled, otherwise they will not be able to join. Leave it off if your players do not all run the mod. Requires a restart to take effect.");
            config.AddConfig("Damage", "ShortCircuitThreshold", 0.25f, "If a generators health is below this percentage of its total health it will have a chance to catch fire next time it takes damage");
        }
    }
}
