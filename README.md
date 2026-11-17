# Deadly Electricity

What if electricity wasn't that simple? Over-supplied power networks damage (and eventually set on fire) the
generators feeding them, live cables are dangerous to cut, and power turns into heat in the cables.

## Generator switch

Real power plants protect their generators by disconnecting them when the grid cannot use their power. In the base game
many generators cannot be switched off at all, so a circuit could never protect them from the damage this mod applies.
The generator switch adds the standard `On` logic type to them: write `On = 0` to disconnect a generator from the power
network and `On = 1` to connect it again. A switched-off generator delivers no power, so it no longer counts as potential
power on its network and can no longer be damaged by over-supply.

| Generator | Writing `On = 0` | Visual feedback |
|---|---|---|
| Solar panel (dual and other advanced models) | Output drops to 0 W, the panel is otherwise untouched | none |
| Wind turbine (all except the upright one) | Output drops to 0 W, the blades and the sound stop | blades stop, tooltip line |
| Gas (pressure) turbine | Electrical output is disconnected; it keeps spinning and letting gas through, so base atmospherics are not affected | tooltip line |
| RTG | Output is disconnected (a real RTG cannot be stopped) | tooltip line |

Reading `On` returns `1` when connected and `0` when switched off. The `On` logic type is listed (Read/Write) on the
Stationpedia page of every supported generator. The Stirling engine, the gas fuel generator and the solid fuel generator
already have a native On/Off in the base game and are left untouched.

```
alias panel d0
s panel On 0        # disconnect the panel
s panel On 1        # connect it again
l r0 panel On       # read the state: 1 = connected, 0 = switched off
```

### Basic generators cannot be switched

Basic generators have no data port, so a circuit could not reach them anyway. To make the advanced models, which do have
a data port, worth building, the basic ones are deliberately left as in the base game (no `On` logic type, no switching):
the **Upright Wind Turbine** and the **basic solar panels**, normal and heavy (flat and angled). This is on by default and
can be changed in the configuration (`RestrictBasicGenerators`, `RestrictedPrefabs`).

### Saving and multiplayer

The switched-off state is saved with the game; only switched-off generators write anything to the save, and a save made
with this mod still loads without it.

The host runs the power simulation and the IC10 programs, so everything works with no setup and nobody needs the mod to
join a server that has it. Players who have the mod also see the right state (wind turbine blades stop, the tooltip line
shows, `On` reads correctly); players without it just keep seeing the turbine spinning.

## Configuration

The settings are in `BepInEx/config/DeadlyElectricity.cfg` (StationeersLaunchPad also lists them in its configuration
panel). The settings marked with * need a game restart to take effect.

### General

| Setting | Default | Description |
|---|---|---|
| `DebugEnabled` | `false` | Enable debug output in the BepInEx console. |
| `ExcludedPowerSuppliers` * | batteries, area power controls, transformers, cable fuses, power umbilicals and circuit breakers | Comma separated prefab names of the power suppliers excluded from taking damage from oversupply of power (spaces around a name are ignored). `StructurePowerConnector` is always excluded, whatever this list says. |

### Power

| Setting | Default | Description |
|---|---|---|
| `PowerShareConvertedPerCable` | `0.02` | The share of energy in the network that is converted to heat per cable in the network. |
| `MaximumSafeWatts` | `100` | The watts threshold at which a live cable is always safe to cut. |
| `WattsThresholdForHumanDamage` | `250` | The watts threshold at which humans without a suit will be damaged by cutting a live cable. |
| `WattsThresholdForSuitDamage` | `500` | The watts threshold at which a suit will be damaged if worn while cutting a live cable. |
| `WattsThresholdForCableDestroyed` | `1000` | The watts threshold at which a live cable will blow when cut. |

### Damage

| Setting | Default | Description |
|---|---|---|
| `PercentageOfPowerConvertedToDamage` | `0.001` | The percentage rate of how much damage a shock will give based on the potential power of the network when cutting a live wire. |
| `DamageMultiplerForShortCircuit` | `0.5` | The amount of damage per watt in a short circuit based on the `PercentageOfPowerConvertedToDamage` value. |
| `WattsThresholdForGeneratorDamage` | `100` | The watts threshold the potential power of a cable network has to be above the required load in order to damage power generating structures on the network. |
| `DamageRate` | `0.1` | How much damage a generator will take if it is selected to take damage due to overproduction of power on a network. |
| `ShortCircuitThreshold` | `0.25` | If a generator's health is below this percentage of its total health it will have a chance to catch fire next time it takes damage. |

### GeneratorSwitch

| Setting | Default | Description |
|---|---|---|
| `EnableGeneratorSwitch` * | `true` | Adds the `On` logic type to the generators that cannot be switched off in the base game (see above). |
| `RestrictBasicGenerators` * | `true` | When true, the generators listed in `RestrictedPrefabs` can NOT be switched off and keep behaving exactly as in the base game. Set to false to allow every supported generator to be switched. |
| `RestrictedPrefabs` * | `StructureUprightWindTurbine`, `StructureSolarPanelFlat`, `StructureSolarPanel45`, `StructureSolarPanelFlatReinforced`, `StructureSolarPanel45Reinforced` | Comma separated prefab names of the generators that cannot be switched off while `RestrictBasicGenerators` is true. |
