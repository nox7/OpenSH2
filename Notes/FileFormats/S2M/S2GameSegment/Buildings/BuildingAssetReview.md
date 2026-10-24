# Building assets needing review

These are the remaining common Building placements skipped by the generic loader. The main building meshes for the other mapped families were found and converted.

| Case | All maps | Chapter 8 | Candidate / unresolved detail |
|---|---:|---:|---|
| Unlinked ServantsQuarters markers | 6 | 1 | Four reciprocal kitchen/pantry pairs are now decoded and rendered. The remaining 1x1 records have no kitchen link; their standalone purpose remains unresolved. See [CompositeBuildings.md](CompositeBuildings.md). |
| Statue | 8 | 0 | Both `statue_horse.bdg` and `statue_lord.bdg` exist; variant selector is not mapped. |
| EngineersGuild (one placement) | 1 | 0 | `Peace Campaign.s2m`, object 575210552: map 3x4 versus `engineers_guild.bdg` 5x5. Four other placements load normally. |
| StockPile (one placement) | 1 | 0 | `war_chapter1.s2m`, object 339973272: map 3x5 versus `stockpile.bdg` 7x5. Other 56 placements load normally. Need smaller/state-specific asset rule. |
| ConstructingBeacon / Keep / Monastery / Bridge | 6 | 1 | Construction progress/state selection remains undecoded. Bridge construction remains deferred as requested. |

## Exact skipped objects

| Map | Object type | Object ID |
|---|---|---:|
| 3v2 - Highlands v2.s2m | Statue | 265208360 |
| 3v2 - Highlands v2.s2m | Statue | 265206624 |
| 3v2 - Highlands.s2m | Statue | 902070080 |
| 3v2 - Highlands.s2m | Statue | 902073800 |
| Axon (backup).s2m | ConstructingMonastery | 901422776 |
| Axon.s2m | ConstructingMonastery | 902846440 |
| Chiller Map ;).s2m | ServantsQuarters | 843253664 |
| Peace Campaign.s2m | ConstructingBeacon | 357016992 |
| Peace Campaign.s2m | ConstructingKeep | 356344048 |
| Peace Campaign.s2m | ServantsQuarters | 356343720 |
| Peace Campaign.s2m | ServantsQuarters | 575937328 |
| Peace Campaign.s2m | ServantsQuarters | 354794464 |
| Peace Campaign.s2m | ServantsQuarters | 357676720 |
| Peace Campaign.s2m | EngineersGuild | 575210552 |
| war_chapter1.s2m | Statue | 339990824 |
| war_chapter1.s2m | Statue | 339990584 |
| war_chapter1.s2m | Statue | 339985096 |
| war_chapter1.s2m | Statue | 339984856 |
| war_chapter1.s2m | StockPile | 339973272 |
| war_chapter2.s2m | ConstructingBridge | 484097416 |
| war_chapter8.s2m | ConstructingBridge | 217300008 |
| war_chapter8.s2m | ServantsQuarters | 217296368 |

## Unresolved texture references

All 125 DDS files resolved by the current importer decode. The following exporter references have no direct installed DDS match; the renderer currently uses its untextured material for these slots.

| Model | Exporter texture basename | Detail |
|---|---|---|
| `water_pot.GR2` | `grubby_planks.bmp` | No corresponding DDS found in the installation. Need a verified replacement. |
| `gatehouse_small_foundation.GR2`, `gatehouse_medium_foundation.GR2`, `gatehouse_large_foundation.GR2` | `weapons_short.BMP` | Exporter says `meshes/buildings/weapons_short`; installed candidates are `meshes/buildings/stockpile/weapons_short.dds` and `meshes/units/models/weapons_short.DDS`. Correct source not verified. |
| `poleturner_hut_collision.GR2` | `poleturner_workshop_open.bmp`, `poleturner_workshop_closed.bmp` | Collision-only geometry; invisible, so no visible texture loss. |

## Other placed devices

These objects are present outside the common Building placement decoder and are not included in the 1,332-building count. Candidate geometry was found under `meshes/siege_equipment`, not `meshes/buildings`. Their placement/state blocks still need decoding.

| Object family | Candidate GR2 files |
|---|---|
| Brazier | `brazier.GR2` |
| BurningLog / BurningLogTipper | `burning_logs_body.GR2`, `burning_logs_log.GR2` |
| StoneTipper | `stone_tipper_body.GR2`, `stone_tipper_rocks.GR2` |

Hovel selector ordering and separate moving-component poses are inferred/static and need visual confirmation; they do not prevent their base meshes from loading. Quarry rock variants, stored resources, crops, workers, animations and production effects are separate follow-up work.
