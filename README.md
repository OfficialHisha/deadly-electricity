# Deadly Electricity

What if electricity wasn't that simple? Over-supplied power networks damage (and eventually set on fire) the
generators feeding them, live cables are dangerous to cut, and power turns into heat in the cables.

## Generator switch (logic On/Off for every generator)

Real power plants protect their generators by disconnecting them when the grid cannot use their power. In the
base game many generators cannot be switched off at all, so a circuit could never protect them from the damage
this mod applies. The generator switch fixes that.

### What it does

The standard `On` logic type is added to the generators that do not have one:

| Generator | Writing `On = 0` | Visual feedback |
|---|---|---|
| Solar panel (dual and other advanced models) | Output drops to 0 W, the panel is otherwise untouched | none (unchanged, like a real panel behind an open isolator) |
| Wind turbine (all except the upright one) | Output drops to 0 W, the blades stop and the sound stops | the blades stop, plus a tooltip line |
| Gas (pressure) turbine | Electrical output is disconnected. The turbine keeps spinning and letting gas through, so base atmospherics are not affected | tooltip line |
| RTG | Output is disconnected (a real RTG cannot be stopped) | tooltip line |

`On = 1` connects the generator again. Reading `On` returns `1` when connected and `0` when switched off.

A switched-off generator delivers no power, so it no longer counts as potential power on its network and can no
longer be damaged by over-supply.

The **Stirling engine**, the **gas fuel generator** and the **solid fuel generator** already have a native On/Off in
the base game (where "off" really stops burning fuel), so they are left untouched and work with the same `On`
logic type.

### Basic generators cannot be switched (on by default)

Basic generators have no data port, so a circuit cannot reach them anyway. To make the advanced models, which do have
a data port, worth building, the basic ones are deliberately left as in the base game: no `On` logic type, no
switching, nothing saved. By default this applies to:

* **Upright Wind Turbine** (`StructureUprightWindTurbine`)
* **Basic solar panels**, normal and heavy (`StructureSolarPanelFlat`, `StructureSolarPanel45`,
  `StructureSolarPanelFlatReinforced` and `StructureSolarPanel45Reinforced`)

Dual and other solar panels, the other wind turbines, the gas turbine and the RTG are **not** restricted. Set
`RestrictBasicGenerators = false` to allow every supported generator, or edit `RestrictedPrefabs` (comma separated
prefab names) to choose exactly which ones are restricted. Restricted generators do not list `On` in the
Stationpedia either.

### Using it from IC10

```
alias panel d0

# Disconnect the panel
s panel On 0

# Connect it again
s panel On 1

# Read the current state (1 = connected, 0 = switched off)
l r0 panel On
```

The `On` logic type is also listed (Read/Write) on the Stationpedia page of every supported generator, like any
other logic type, and those generators appear on the Stationpedia page of the `On` logic type.

### Saving

The switched-off state is saved with the game. Only generators that are switched off write anything to the
save, and a save made with this mod still loads without it.

### Performance

* No component, no per-frame update and no extra field is added to any generator.
* Only generators that are currently switched off are tracked, so large generator farms cost nothing while running.
* The power tick does a lock-free lookup only; when nothing is switched off it is a single integer comparison.
* Saving and loading only do work for switched-off generators.

### Multiplayer

The host decides everything: it runs the power simulation and the IC10 programs, so power, damage and saving are
correct in multiplayer without any extra setup, and **clients do not need the mod for that**.

What clients cannot know by themselves is *which* generators are switched off, so by default, on a client:
the tooltip line is not shown, a switched-off wind turbine keeps animating (its blades are computed locally), and
reading `On` from a client-side device reports `1`. Gas turbines are not affected visually, as the game
synchronises their animation.

#### Optional: synchronise the switch state to the clients

Set `GeneratorSwitch / EnableNetworkSync = true` to send the state from the host to the clients. Then wind turbines
stop on every screen, tooltips are right and `On` reads correctly everywhere.

> **Read this before enabling it.** The sync uses the LaunchPadBooster networking, which changes the join
> handshake of the whole session. The host **and every player** must run this mod with `EnableNetworkSync` enabled,
> otherwise they will not be able to join. That is why it is off by default: turn it on only if all your players
> use the mod. It requires StationeersLaunchPad (LaunchPadBooster); if that library is missing the mod logs a warning
> and keeps working without the sync.

How it stays light and safe:

* Nothing is sent while nobody toggles a generator. Changes made in the same frame are sent as one message
  (about 9 bytes per generator). A joining player receives the list of switched-off generators once, in the join data.
* The messages use their own length-delimited packets, not the game's positional update stream, so a payload that is
  not understood is skipped instead of corrupting the connection. Every payload starts with a protocol byte.
* Only the host sends, and a host never obeys a message it receives, so a client cannot switch generators on the host.
* The mod is not marked as required and only the major version has to match, so players on different minor
  releases can still join each other.
* Anything that goes wrong on the sync path is caught and results in "no sync", never in a failed join.

Known gap: a toggle that happens in the short moment between the host preparing the join data and the new player
becoming ready is not delivered to that player until the generator is toggled again.

### Configuration

| Setting | Default | Purpose |
|---|---|---|
| `GeneratorSwitch / EnableGeneratorSwitch` | `true` | Turns the whole generator switch off. Restart required. |
| `GeneratorSwitch / RestrictBasicGenerators` | `true` | Basic generators listed in `RestrictedPrefabs` cannot be switched (see above). Restart required. |
| `GeneratorSwitch / RestrictedPrefabs` | basic generators | Comma separated prefab names affected by the restriction. Restart required. |
| `GeneratorSwitch / EnableNetworkSync` | `false` | Sends the switch state to the clients (see above). Must be identical for the host and all players. Restart required. |

### How it is built

Everything lives in two places, each heavily commented:

* [`Switching/`](Switching) - `GeneratorSwitch` (state and performance design), `GeneratorSwitchSync` (when and what
  is sent) and `LaunchPadLink` (the only code that touches LaunchPadBooster).
* [`Patches/`](Patches) - `GeneratorLogicPatches` (logic type and Stationpedia), `GeneratorOutputPatches`
  (cutting the output), `GeneratorPersistencePatches` (save/load) and `GeneratorTooltipPatch` (tooltip).

### Generators that never take damage

The power connector (`StructurePowerConnector`) can never be damaged by over-supply. This is hard-coded, so it holds
even for configuration files created by older versions. Other suppliers can be excluded with
`ExcludedPowerSuppliers`.
