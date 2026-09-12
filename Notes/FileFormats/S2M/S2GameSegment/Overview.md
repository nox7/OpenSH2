# S2Game segment

This is an object stream containing terrain systems, placed entities, buildings, effects, script actions, and navigation data. It begins with the common 8-byte segment prefix, uses a segment-local type registry, and ends with `AD DE FF FF`.

Validated object counts:

| Map | Inflated bytes | Objects | Registered types |
| --- | ---: | ---: | ---: |
| `war_chapter1.s2m` | 10,397,138 | 788 | 121 |
| `war_chapter8.s2m` | 10,709,160 | 2,661 | 157 |

The marker-based object walk reaches the end marker exactly in both files. This provides a useful safe baseline: known types can be decoded, while unknown types retain their raw payload and do not stop the rest of the map from loading.

## Confirmed terrain/system object types

Both samples include `S2Game`, `Simulation`, `Landscape`, `Map`, `BuildingLayer`, `ChannelLayer`, `ContextLayer`, `DesirabilityLayer`, `EstateLayer`, `HeightLayer`, `LogicLayer`, `ProximityLayer`, `SearchLayer`, `SpreadLayer`, `DangerLayer`, and `TempLayer`. Some empty layers are represented by short 28-byte payloads. Chapter 1 and chapter 8 do not serialize these types in the same order, so readers must follow object headers rather than fixed offsets.

## Confirmed placed-object types

Examples include troops (`Archer`, `Crossbowman`, `Pikeman`, `Spearman`, `Swordsman`, `Knight`, `Lord`), economy objects (`AppleFarm`, `Bakery`, `Hovel`, `HorseCart`, `Oxen`), fortifications (`TowerLookout`, `TowerSquare`, `GateTeleport`, `KillingPit`), and civic buildings (`CourtHouse`, `Dungeon`). Their payload schemas remain under investigation, but each payload is now retained as a `RawS2Object` for repeatable comparison.

In chapter 1 there are 67 `Archer` objects; chapter 8 has 278. Chapter 8 also has 11 `TowerLookout`, 11 `HorseCart`, 3 `Oxen`, 4 `CourtHouse`, and 4 `Dungeon` objects. These are good controlled populations for field-correlation work.

## Reader validation

The object-stream reader was exercised against all 36 `.s2m` files in the supplied Maps directory. Every file reached the end of all three decompressed segments. Every tested RadarMap segment contained two objects. S2Game object counts ranged from 150 to 3,675. A few backup or partial test maps contain no decodable `HeightLayer`; the reader leaves `HeightLayer` null rather than rejecting the entire map.

The confirmed primary height plane can be converted to a runtime Unity Terrain as described in [Rendering HeightLayer with Unity Terrain](./UnityTerrain.md). Controlled water, material, and flow-direction fields are documented in [Landscape water data](./LandscapeWater.md).

The current terrain-texture selector and tint evidence is documented in [Terrain texture selection and tinting](./TerrainTextures.md).

Placed landscape records and the demand-loaded GR2 rendering path are documented in
[Forest vegetation and Unity rendering](./Vegetation.md).
