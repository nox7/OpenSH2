# Building asset inventory

Audited 65 `.s2m` files in `C:/Users/garet/Documents/Stronghold 2/Maps` against the installed Stronghold 2 assets. Counts describe this local corpus, including backup/variant maps, not every possible game building. The current test map is `war_chapter8.s2m`.

The common Building payload identifies 1,332 placements in 87 concrete types. The generic loader accepts 1,282 ordinary placements; the existing keep loader handles all 28 keeps. The remaining 22 common placements are listed in [BuildingAssetReview.md](BuildingAssetReview.md). Completed modular bridges use their separate landscape/span decoder and are additional to these counts.

Chapter 8 contains 246 common placements: 239 ordinary buildings and five keeps are supported; one unlinked servants' quarters marker and one construction bridge remain unresolved. Two completed bridge spans bring the map-level render count to 246.

The ordinary-building audit converted 203 distinct GR2 files (exteriors, interiors, collision, and selected separate components). Texture resolution finds 125 unique installed DDS files; all 125 decode successfully. Six obsolete material references remain, detailed in the review list. Collision-only materials are invisible.

## Mapping

Paths below are relative to `meshes/buildings`. The BDG supplies the active exterior/interior/collision references; filenames are not generated from the object name. An empty interior column means the descriptor does not enable one.

| S2M type | All maps | Chapter 8 | BDG | Exterior | Enabled interior | Collision | Status |
|---|---:|---:|---|---|---|---|---|
| Apothecary | 1 | 1 | `apothecary.bdg` | `apothecary_closed.GR2` | `apothecary_open.GR2` | `apothecary_collision.GR2` | Loaded |
| AppleFarm | 168 | 11 | `farm_apples.bdg` | `farm_apple.GR2` | â€” | `farm_apple_collision.GR2` | Loaded |
| ArmourerWorkshop | 3 | 1 | `armourer.bdg` | `armourer_closed.GR2` | `armourer_open.GR2` | `armourer_collision.GR2` | Loaded |
| Armoury | 7 | 3 | `armoury.bdg` | `armoury_closed.GR2` | `armoury_open.GR2` | `armoury_closed_collision.GR2` | Loaded |
| Bakery | 72 | 5 | `bakery_workshop.bdg` | `workshop_bakery_closed.GR2` | `workshop_bakery_open.GR2` | `workshop_bakery_collision.GR2` | Loaded |
| BeeHive | 3 | 3 | `beehive.bdg` | `beehive.GR2` | â€” | `beehive_collision.GR2` | Loaded |
| BlacksmithWorkshop | 7 | 2 | `blacksmith_workshop.bdg` | `blacksmith_workshop_closed.GR2` | `blacksmith_workshop_open.GR2` | `blacksmith_shop_collision.GR2` | Loaded |
| Brewery | 7 | 0 | `brewery.bdg` | `brewery_closed.GR2` | `brewery_open.GR2` | `brewery_collision.GR2` | Loaded |
| CarterPost | 23 | 0 | `cart_post.bdg` | `cart_post.GR2` | â€” | — | Loaded |
| ChandlerWorkshop | 1 | 1 | `chandler_workshop.bdg` | `chandler_workshop.GR2` | `chandler_workshop_open.GR2` | `chandler_workshop_collision.GR2` | Loaded |
| Church | 2 | 1 | `church.bdg` | `church.GR2` | `church_open.GR2` | `church_collision.GR2` | Loaded |
| ConstructingBeacon | 1 | 0 | â€” | â€” | â€” | — | Deferred: construction state |
| ConstructingBridge | 2 | 1 | â€” | â€” | â€” | — | Deferred: construction state |
| ConstructingKeep | 1 | 0 | â€” | â€” | â€” | — | Deferred: construction state |
| ConstructingMonastery | 2 | 0 | â€” | â€” | â€” | — | Deferred: construction state |
| CourtHouse | 5 | 4 | `court_house.bdg` | `court_house_closed.GR2` | `court_house_open.GR2` | `court_collision.GR2` | Loaded |
| DairyFarm | 55 | 4 | `farm_dairy.bdg` | `farm_dairy.GR2` | â€” | `farm_dairy_collision.GR2` | Loaded |
| Dungeon | 5 | 4 | `dungeon.bdg` | `dungeon_closed.GR2` | `dungeon_open.GR2` | `dungeon_collision.GR2` | Loaded |
| EngineersGuild | 5 | 4 | `engineers_guild.bdg` | `engineers_guild_closed.GR2` | â€” | `engineers_guild_collision.GR2` | 4 loaded; one 3x4 placement skipped |
| FalconersPost | 7 | 7 | `falcon_post.bdg` | `falcon_post.GR2` | â€” | `falcon_post_collision.GR2` | Loaded |
| FishPond | 1 | 1 | `fishpond.bdg` | `fishpond.GR2` | â€” | — | Loaded |
| FletcherWorkshop | 10 | 4 | `fletcher_workshop.bdg` | `fletcher_closed.GR2` | `fletcher_open.GR2` | `fletcher_collision.GR2` | Loaded |
| GardenersHut | 1 | 1 | `gardeners_hut.bdg` | `gardeners_hut.GR2` | â€” | `gardeners_hut_collision.GR2` | Loaded |
| GongPit | 14 | 6 | `gong_farm.bdg` | `gong_farm.GR2` | â€” | `gong_farm_collision.GR2` | Loaded |
| Granary | 36 | 4 | `granary.bdg` | `granary_closed.GR2` | `granary_open.GR2` | `granary_collision.GR2` | Loaded |
| HopFarm | 7 | 1 | `farm_hops.bdg` | `farm_hops.GR2` | â€” | `farm_hops_collision.GR2` | Loaded |
| Hovel | 155 | 20 | `hovel3.bdg`<br>`hovel6.bdg`<br>`hovel1.bdg`<br>`hovel2.bdg`<br>`hovel4.bdg`<br>`hovel5.bdg` | `hovel3_closed.GR2`<br>`hovel6_closed.GR2`<br>`hovel1_closed.GR2`<br>`hovel2_closed.GR2`<br>`hovel4_closed.GR2`<br>`hovel5_closed.GR2` | `hovel3_open.GR2`<br>`hovel6_open.GR2`<br>`hovel1_open.GR2`<br>`hovel2_open.GR2`<br>`hovel4_open.GR2`<br>`hovel5_open.GR2` | `hovel_small_collision.GR2`<br>`hovel_small_collision.GR2`<br>`hovel_small_collision.GR2`<br>`hovel_small_collision.GR2`<br>`hovel_small_collision.GR2`<br>`hovel_small_collision.GR2` | Loaded: selector 0..5; visual ordering inferred |
| HuntersPost | 21 | 3 | `hunters_post.bdg` | `hunter_post.GR2` | â€” | `hunter_post_collision.GR2` | Loaded |
| Inn | 6 | 2 | `inn.bdg` | `inn_closed.GR2` | `inn_open.GR2` | `inn_collision.GR2` | Loaded |
| IronMine | 23 | 0 | `iron_mine.bdg` | `iron_mine.GR2` | â€” | `iron_mine_collision.GR2` | Loaded |
| Jousting | 2 | 1 | `jousting.bdg` | `jousting.GR2` | â€” | — | Loaded |
| Keep1 | 19 | 1 | `keep_wooden.bdg` | `Keep_wooden_closed.GR2` | `Keep_wooden_open.GR2` | `Keep_wooden_collision.GR2` | Specialized keep loader |
| Keep2 | 2 | 1 | `keep_small.bdg` | `Keep_small_closed.GR2` | `Keep_small_open.GR2` | `Keep_small_collision.GR2` | Specialized keep loader |
| Keep3 | 5 | 3 | `keep_medium.bdg` | `keep_medium_closed.GR2` | `keep_medium_open.GR2` | `keep_medium_collision.GR2` | Specialized keep loader |
| Keep4 | 2 | 0 | `keep_large.bdg` | `Keep_large_closed.GR2` | `Keep_large_open.GR2` | `Keep_large_collision.GR2` | Specialized keep loader |
| KillingPit | 43 | 28 | `killing_pits.bdg` | `killing_pits.GR2` | â€” | — | Loaded |
| Kitchen | 4 | 3 | `kitchen.bdg` | `kitchen_closed.GR2` | `kitchen_open.GR2` | `kitchen_collision.GR2` | Loaded |
| LargeGateHouse | 1 | 0 | `gatehouse_large.bdg` | `gatehouse_large.GR2` | â€” | `gatehouse_large_collision.GR2` | Loaded |
| ManTrap | 128 | 0 | `mantrap.bdg` | `man_trap.GR2` | â€” | — | Loaded |
| Market | 5 | 2 | `market.bdg` | `market_closed.GR2` | â€” | `market_collision.GR2` | Loaded |
| MediumGateHouse | 8 | 4 | `gatehouse_medium.bdg` | `gatehouse_medium.GR2` | â€” | `gatehouse_medium_collision.GR2` | Loaded |
| Mill | 14 | 1 | `windmill.bdg` | `windmill_closed.GR2` | `windmill_open.GR2` | `windmill_collision.GR2` | Loaded |
| Monastery | 2 | 0 | `monastery.bdg` | `monastery.GR2` | `monastery_inner.GR2` | `monastery_collision.GR2` | Loaded |
| MusiciansGuild | 1 | 0 | `minstrels_guild.bdg` | `minstrels_guild.GR2` | `minstrels_guild_open.GR2` | `minstrels_guild_collision.GR2` | Loaded |
| OutlawCamp | 7 | 0 | `bandit_camp.bdg` | `bandit_camp.GR2` | â€” | `bandit_camp_collision.GR2` | Loaded |
| OxTether | 42 | 3 | `ox_tether.bdg` | `ox_tether.GR2` | â€” | `ox_tether_collision.GR2` | Loaded |
| PigFarm | 5 | 4 | `farm_pigs.bdg` | `farm_pigs.GR2` | â€” | `farm_pigs_collision.GR2` | Loaded |
| PitchRig | 5 | 3 | `pitch_rig.bdg` | `pitch_rig_poles.GR2` | â€” | `pitch_rig_collision.GR2` | Loaded |
| PoleturnerWorkshop | 11 | 4 | `poleturner_hut.bdg` | `poleturner_hut_closed.GR2` | `poleturner_hut_open.GR2` | `poleturner_hut_collision.GR2` | Loaded |
| SawPit | 69 | 8 | `saw_pit.bdg` | `saw_pit.GR2` | â€” | `saw_pit_collision.GR2` | Loaded |
| ServantsQuarters | 10 | 4 | `servants_quarters_pantry.bdg` | `servants_pantry.GR2` | `servants_pantry_open.GR2` | `servants_pantry_collision.GR2` | 4 linked pantries loaded; 6 unlinked markers skipped |
| SheepFarm | 2 | 0 | `farm_sheep.bdg` | `farm_sheep.GR2` | â€” | `farm_sheep_collision.GR2` | Loaded |
| Signpost | 5 | 0 | `signpost.bdg` | `signpost.GR2` | â€” | — | Loaded |
| SmallGateHouse | 9 | 3 | `gatehouse_small.bdg` | `gatehouse_small.GR2` | â€” | `gatehouse_small_collision.GR2` | Loaded |
| Solar | 2 | 2 | `queens_solar.bdg` | `queens_solar_closed.GR2` | `queens_solar_open.GR2` | `queens_solar_collision.GR2` | Loaded |
| Stables | 2 | 0 | `stable.bdg` | `stable.GR2` | â€” | `stable_collision.GR2` | Loaded |
| Statue | 8 | 0 | `statue_horse.bdg`<br>`statue_lord.bdg` | `statue_horse.GR2`<br>`statue_lord.GR2` | â€” | — | Skipped: variant unknown |
| StockPile | 57 | 3 | `stockpile.bdg` | `stockpile_closed.GR2` | `stockpile_open.GR2` | `stockpile_collision.GR2` | 56 loaded; one 3x5 placement skipped |
| StoneBarracks | 7 | 3 | `barrack_stone.bdg` | `barrack_stone.GR2` | â€” | `barrack_stone_collision.GR2` | Loaded |
| StoneQuarry | 12 | 3 | `quarry.bdg` | `quarry.GR2` | â€” | `quarry_collision.GR2` | Loaded |
| TannerWorkshop | 4 | 2 | `tanner_workshop.bdg` | `tanner_workshop_closed.GR2` | `tanner_workshop_open.GR2` | `tanner_workshop_collision.GR2` | Loaded |
| TortureBlock | 4 | 2 | `torture_block.bdg` | `torture_block_building.GR2` | â€” | `torture_block_collision.GR2` | Loaded |
| TortureBrandingChair | 1 | 0 | `torture_branding.bdg` | `torture_branding_building.GR2` | â€” | `torture_branding_collision.GR2` | Loaded |
| TortureBurningPost | 1 | 0 | `torture_burning.bdg` | `torture_burning.GR2` | â€” | `torture_burning_collision.GR2` | Loaded |
| TortureFloggingPost | 1 | 1 | `torture_flogging.bdg` | `torture_flogging_building.GR2` | â€” | `torture_flogging_collision.GR2` | Loaded |
| TortureGallows | 1 | 1 | `torture_gallows.bdg` | `torture_gallows_building.GR2` | â€” | `torture_gallows_collision.GR2` | Loaded |
| TortureGibbet | 1 | 0 | `torture_gibbet.bdg` | `torture_gibbet_building.GR2` | â€” | `torture_gibbet_collision.GR2` | Loaded |
| TortureMask | 1 | 0 | `torture_mask.bdg` | `torture_mask_building.GR2` | â€” | `torture_mask_collision.GR2` | Loaded |
| TortureRack | 2 | 1 | `torture_streching.bdg` | `torture_streching.GR2` | â€” | `torture_streching_collision.GR2` | Loaded |
| TorturersGuild | 4 | 3 | `torturer_guild.bdg` | `torturers_guild.gr2` | `torturer_guild_open.GR2` | `torturers_guild_collision.gr2` | Loaded |
| TortureStocks | 1 | 0 | `torture_stocks.bdg` | `torture_stocks_building.GR2` | â€” | `torture_stocks_collision.GR2` | Loaded |
| TortureWheel | 3 | 1 | `torture_wheel.bdg` | `torture_wheel.GR2` | â€” | `torture_wheel_collision.GR2` | Loaded |
| TowerGreat | 3 | 0 | `tower_great.bdg` | `tower_great_closed.GR2` | `tower_great_open.GR2` | `tower_great_collision.GR2` | Loaded |
| TowerLookout | 17 | 11 | `tower_lookout.bdg` | `tower_lookout_closed.GR2` | `tower_lookout_open.GR2` | `tower_lookout_collision.GR2` | Loaded |
| TowerRoundShort | 3 | 1 | `tower_round_short.bdg` | `tower_round_short_closed.GR2` | `tower_round_short_open.GR2` | `tower_round_short_collision.GR2` | Loaded |
| TowerRoundTall | 7 | 4 | `tower_round_tall.bdg` | `tower_round_tall_closed.GR2` | `tower_round_tall_open.GR2` | `tower_round_tall_collision.GR2` | Loaded |
| TowerSquare | 23 | 4 | `tower_square.bdg` | `tower_square_closed.GR2` | `tower_square_open.GR2` | `tower_square_collision.GR2` | Loaded |
| TowerStairwell | 11 | 3 | `tower_stairwell.bdg` | `tower_stairwell_closed.GR2` | `tower_stairwell_open.GR2` | `tower_stairwell_collision.GR2` | Loaded |
| Treasury | 4 | 3 | `treasury.bdg` | `treasury_closed.GR2` | `treasury_open.GR2` | `treasury_collision.GR2` | Loaded |
| WarHoundCage | 8 | 0 | `wardog_cage.bdg` | `wardog_cage.GR2` | â€” | `wardog_cage_collision.GR2` | Loaded |
| WatchPost | 7 | 5 | `watchpost.bdg` | `watchpost.GR2` | â€” | `watchpost_collision.GR2` | Loaded |
| WaterPot | 2 | 0 | `water_pot.bdg` | `water_pot.GR2` | â€” | — | Loaded |
| WeaverWorkshop | 1 | 0 | `workshop_weaver.bdg` | `workshop_weaver.GR2` | â€” | `workshop_weaver_collision.GR2` | Loaded |
| Well | 4 | 0 | `well.bdg` | `well.GR2` | â€” | `well_collision.GR2` | Loaded |
| WheatFarm | 48 | 3 | `farm_wheat.bdg` | `farm_wheat.GR2` | â€” | `farm_wheat_collision.GR2` | Loaded |
| WoodenGateHouse | 8 | 5 | `gatehouse_wooden.bdg` | `gatehouse_wooden.GR2` | â€” | `gatehouse_wooden_collision.GR2` | Loaded |
| WoodenPlatform | 22 | 18 | `tower_wooden.bdg` | `tower_wooden.GR2` | â€” | — | Loaded |

## Descriptor prefix and assembly

Verified version-13 BDG prefix: uint32 version at 0; uint16 width/height at 8/10; 32-byte exterior name at 32; enabled-open flag at 64 and 32-byte interior name at 65; optional 32-byte collision name at 163. Inactive filename slots can contain stale exporter strings and must not be scanned as active models. This is a partial schema, not a complete BDG decoder.

Selected descriptor-referenced components are loaded in their static exported poses: gatehouse foundations/doors/portcullises, large gatehouse bridge/mechanism/chain, windmill wings/shaft, pitch rig scoop, and dairy parts. Animation, worker interactions, stored goods, crop actors, production effects, lights and destruction stages are not implemented. Other animated/interior attachments (for example quarry rock poses and the apothecary book/liquid) need their component/state records decoded.

Map footprints can include reserved placement clearance larger than the mesh (some saw pits 1x4 vs 1x2). The kitchen is a composite exception: its 7x4 footprint contains a 4x4 kitchen and a linked 3x4 pantry; it is not evidence of empty clearance. See [CompositeBuildings.md](CompositeBuildings.md). The original dimensions remain stored on every `BuildingPlacement` for future placement-overlap checks and debugging; models retain their physical scale. This likely explains the original game's colored placement squares, but does not establish unit collision or walkability rules. Smaller-than-descriptor footprints are reported rather than stretched. All rendering uses the stored position/yaw, shared map scale and axis flips.

Hovel appearance is the int32 at body offset 4 in its final counted 28-byte concrete body. All 155 records contain selectors 0..5; these select `hovel1.bdg` through `hovel6.bdg`. Confirm the ordering visually against the original game.

Unity exposes shared **Building Render Settings** for automatic/forced exterior/interior views. The serialized former keep-settings field is migrated. Mesh/material data is shared per asset batch and DDS textures are reference-counted across batches. Primary ground rendering and original walking corner heights remain separate; asset loading does not modify the navigation data.

## Validation

All 65 maps parsed and ran through the actual asset loaders and native converter. All supported primary meshes exist and convert, as do their enabled interiors/collision/static parts. Empty non-drawable Granny helper meshes are skipped instead of failing on their zero-byte index format. Additional checks cover view decisions/hysteresis, scaled transforms, texture resolution, DDS decoding, shared texture lifetimes, and terrain-height preservation. The Unity C# assembly builds; in-editor placement, inferred hovel appearance order and attachment poses still need visual verification.

