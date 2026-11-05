# Building labor implementation checklist

Audited against the engine on **2026-11-04**. This is the working backlog for
building employment, worker behavior and related building-operated actors.
Check items off as complete labor workflows land; loading a model or playing
an ambient animation does not complete an item.

At this audit: **5 implemented labor building types**, **37 remaining worker/service
building entries**, and **13 specialist/household/event entries**. Hive support,
the ten punishment devices and passive-structure classification are tracked alongside them.

Coverage combines the native mission building-availability list, installed
building descriptors, the engine's building catalog and the SH2-Reforged
building/worker references. The local map asset inventory alone is insufficient:
some placeable buildings do not occur in its sampled maps. Names in backticks
are engine/native record names where known; UI aliases are given alongside them.

## Implemented baseline

- [x] **Sawpit / woodcutter** — `SawPit`: staffing, cart travel, reserved foliage/log
  piles, felling, sawing, 16-plank loads and incremental stockpile deliveries.
- [x] **Stone quarry** — `StoneQuarry`: three required workers, coordinated work,
  intermediate stone visuals, production and bounded local stone storage.
- [x] **Ox tether / handler** — `OxTether`: staffing, ox/equipment lifecycle,
  exclusive quarry/mine collection, full cargo loads and incremental same-estate
  stockpile delivery.
- [x] **Iron mine** — `IronMine`: two required workers, mining animations, local
  iron storage and collection by ox handlers.
- [x] **Pitch rig** — `PitchRig`: one required worker, swamp extraction, five
  synchronized scoop loops per two-pitch load, full-pot carrying, incremental
  same-estate stockpile deliveries and return to work.

These are the five types currently registered with concrete labor definitions in
`LaborBuildingCatalog`. Revisit their checkboxes only if a regression or missing
workflow is identified; do not count their rendered map placeholders as extra staff.

## Remaining worker and service buildings

### Granary food

- [ ] **Apple farm** — `AppleFarm`: orchard worker, tree/fruit growth and harvest,
  carrying visuals and apple delivery to the granary.
- [ ] **Dairy farm** — `DairyFarm`: dairy worker, cow lifecycle/interactions,
  cheese-making and granary delivery.
- [ ] **Hunter's post** — `HuntersPost`: hunter, huntable animal selection/claims,
  pursuit, kill/processing and meat delivery.
- [ ] **Wheat farm** — `WheatFarm`: farmer, crop planting/growth/harvest, field
  occupancy and wheat delivery to the stockpile.
- [ ] **Mill / windmill** — `Mill`: miller workflow, wheat collection, flour
  production and stockpile delivery. Existing spinning machinery is ambient
  presentation, not a completed labor implementation.
- [ ] **Bakery** — `Bakery`: baker, flour collection, baking and bread delivery
  to the granary.

### Ale and inn

- [ ] **Hops farm** — `HopFarm`: farmer, hop growing/harvesting and stockpile delivery.
- [ ] **Brewery** — `Brewery`: brewer, hops collection, brewing and ale delivery.
- [ ] **Inn** — `Inn`: innkeeper, ale collection, serving/consumption and associated
  visitor/popularity behavior.

### Wool, cloth and candles

- [ ] **Sheep farm** — `SheepFarm`: shepherd, sheep lifecycle/shearing and wool delivery.
- [ ] **Weaver's workshop** — `WeaverWorkshop`: weaver, wool collection, cloth
  production and stockpile delivery.
- [ ] **Chandler's workshop** — `ChandlerWorkshop`: chandler, hive selection,
  honey collection/carrying, candle-making and candle delivery. Preserve the
  original hive-local input rather than inventing an estate goods slot for it.
  - [ ] **Beehive dependency** — `BeeHive`: hive replenishment/availability,
    collection claims and honey visual/state changes. The reference has the
    chandler visiting the hive; do not assume a separate beekeeper employee.

### Lord's pantry and kitchen

- [ ] **Vineyard** — native `Vineyard`, asset `farm_grapes.bdg`: vineyard worker,
  grape growth/harvest and stockpile delivery. Asset-catalog coverage also needs review.
- [ ] **Vintner's workshop** — native `VintnerWorkshop`, asset `vintner.bdg`:
  vintner, grape collection, wine-making and Lord's Pantry delivery.
- [ ] **Eel pond / fish pond** — `FishPond`: worker, eel **and goose** production,
  animal/net states, collection and pantry delivery. These are one building's
  workflows, not a separate inferred goose-farm building.
- [ ] **Pig farm** — `PigFarm`: farmer, pigs/breeding/collection and pantry supply.
- [ ] **Vegetable garden / gardener's hut** — `GardenersHut`: gardener, its garden
  plots, vegetable growth/harvest and pantry delivery. Keep the hut and associated
  garden patches together rather than creating two independent staffing goals.
- [ ] **Lord's kitchen** — `Kitchen`: cooking staff, pantry inputs, cooking,
  prepared food and banquet interactions.
- [ ] **Servants' quarters / Lord's Pantry** — `ServantsQuarters`: servants/pages,
  pantry collection/service and the linked kitchen workflow. Inventory storage
  exists; serving and actor behavior are still pending. Verify the composite
  kitchen/pantry ownership and staff split before assigning worker requirements.

### Weapons and armor

- [ ] **Fletcher's workshop** — `FletcherWorkshop`: fletcher, wood collection,
  bow/crossbow selection, crafting and armory delivery.
- [ ] **Poleturner's workshop** — `PoleturnerWorkshop`: poleturner, wood collection,
  spear/pike selection, crafting and armory delivery.
- [ ] **Blacksmith's workshop** — `BlacksmithWorkshop`: blacksmith, iron collection,
  sword/mace selection, forging and armory delivery.
- [ ] **Armourer's workshop** — `ArmourerWorkshop`: armorer, iron collection,
  metal armor production and armory delivery.
- [ ] **Tanner's workshop** — `TannerWorkshop`: tanner, original animal/hide sourcing,
  processing, leather armor production and armory delivery. Confirm the native
  source workflow before introducing any new inventory resource.

### Trade and transport

- [ ] **Market** — `Market`: merchant/trade actor workflow, orders and goods/money
  transactions; confirm its staffing model rather than assuming one peasant.
- [ ] **Carter's post** — `CarterPost`: carter and cart, load reservations, route
  orders, pickup/drop-off, unloading and removal/replacement. Its explicitly
  ordered inter-estate transfers need their own transport rules; ordinary
  production deliveries must retain the existing same-estate restriction.

### Fire, sanitation, pests and health

- [ ] **Well** — `Well`: well worker, water collection, fire dispatch and extinguishing.
- [ ] **Water pot** — `WaterPot`: larger water-supply/firefighting workflow and its
  own verified staffing requirement; do not assume it matches the well.
- [ ] **Gong pit** — `GongPit`: gong farmer, waste discovery/reservation, collection,
  disposal and associated cart/tool visuals.
- [ ] **Falconer's post** — `FalconersPost`: falconer, bird lifecycle, rat targets,
  pest-control dispatch and return behavior.
- [ ] **Apothecary** — `Apothecary`: apothecary, illness/affected-person dispatch,
  treatment and associated work/equipment animations.

### Crime and punishment

- [ ] **Guard/watch post** — `WatchPost`: watchman, crime detection, pursuit,
  arrest and prisoner handoff.
- [ ] **Courthouse** — `CourtHouse`: courthouse staff, criminal processing,
  sentencing and linked dungeon interaction.
- [ ] **Dungeon** — `Dungeon`: jailer/prisoner workflows, detention capacity,
  transfer and release/removal. Verify which actors are staff versus prisoners.
- [ ] **Torturer's guild** — `TorturersGuild`: torturer recruitment/staffing,
  prisoner assignment, station selection, escort, punishment and return.
  Implement the following endpoints as part of this system; do **not** assume
  every device independently recruits another peasant:
  - [ ] Executioner's block — `TortureBlock`.
  - [ ] Branding chair — `TortureBrandingChair`.
  - [ ] Burning post — `TortureBurningPost`.
  - [ ] Flogging post — `TortureFloggingPost`.
  - [ ] Gallows — `TortureGallows`.
  - [ ] Gibbet — `TortureGibbet`.
  - [ ] Humiliation mask — `TortureMask`.
  - [ ] Stretching rack — `TortureRack`.
  - [ ] Stocks — `TortureStocks`.
  - [ ] Torture wheel — `TortureWheel`.

### Religion

- [ ] **Church** — `Church`: priest, religious service/blessing and related visitors.
- [ ] **Monastery** — `Monastery`: monks, religious/work activities and any related
  output/recruitment. Verify how monastery actors enter the population system.

## Specialist, household and event actor workflows

These known placeable buildings also lack their operating actor systems. They
belong in the backlog, but are not automatically ordinary peasant jobs. Verify
the original recruitment/ownership model before adding labor slots.

- [ ] **Barracks** — `StoneBarracks`: soldier recruitment, population/equipment
  consumption, muster/exit locations and removal; soldiers are not resident staff.
- [ ] **Mercenary post** — `MercenaryPost`, `mercenary_post.bdg`: hired-unit
  recruitment, costs and exit/muster behavior. Asset mapping also needs review.
- [ ] **Engineers' guild** — `EngineersGuild`: engineer recruitment and assignment
  to engine construction/operation and siege tasks.
- [ ] **Siege camp** — `SiegeCamp`: siege construction crews/engineers, requested
  equipment, material costs and completion/ownership handoff.
- [ ] **Oil smelter / oil cauldron** — `oil_cauldron.bdg`: operator assignment,
  input/boiling/carrying and wall-use workflow; confirm native record naming.
- [ ] **Tunnel entrance / tunneler deployment** — `tunneler.bdg`: tunneler
  assignment, digging, target structures and emergence/completion.
- [ ] **Stables** — `Stables`: horse production, capacity, horse actors and mounted
  unit supply; verify staff requirements before introducing a stable-worker job.
- [ ] **War hound cage** — `WarHoundCage`: hound production/capacity, release,
  targets and lifecycle; verify whether there is any resident employee.
- [ ] **Treasury** — `Treasury`: tax/treasury actor and money-management integration;
  verify the original staffing model rather than inventing a tax-collector slot.
- [ ] **Lady's bedchamber / solar** — `Solar`: household/lady/servant activities
  and their gameplay effects; distinguish generated household actors from labor.
- [ ] **Musicians' guild** — `MusiciansGuild`: minstrel creation, dispatch,
  performance and return/lifecycle.
- [ ] **Jousting arena** — `Jousting`: event participants, scheduling, mounted
  animation/interaction and rewards.
- [ ] **Traveling fair / fairground** — native `Fairground`, `travelling_fair.bdg`:
  visiting performers, event lifecycle and effects; review asset mapping too.

## Passive structures and placeable equipment: classify without inventing labor

The following are tracked for completeness, not as unimplemented peasant jobs.
Their gameplay can still be incomplete even when their labor requirement is zero.

- Already explicitly zero-worker in the labor catalog: **hovel**, **stockpile**,
  **granary**, **estate flagpole**. Their implemented population/storage behavior
  does not create a resident storekeeper or hovel worker.
- [ ] **Armory** — `Armoury`: storage already exists; explicitly review zero-worker
  classification and connect delivery/recruitment consumers as they are implemented.
- [ ] **Keep variants** — `Keep1`–`Keep4` (manor/wooden, small, strong/medium,
  royal/large): campfire/population already exists; review explicit staffing
  classification. Keep household/banquet actors belong to their own systems.
- [ ] **Fortifications** — wooden walls; single/double/triple stone walls; wooden,
  small, medium and large gatehouses; wall stairs/stairwell; wooden platform;
  lookout, short round/bastion, tall round, square and great towers; hoardings;
  sally port; wooden and stone bridges. Track unit access, gates, garrisons and
  construction separately from permanent labor slots.
- [ ] **Defensive equipment** — tower mangonel/ballista, oil equipment, stone
  tippers/rock baskets, rolling logs, braziers, siege spikes, ladders, mantraps,
  killing pits, pitch ditches and moats. Operators can be soldiers/engineers or
  construction crews; do not model every item as another employed peasant.
- [ ] **Honor/decorative structures** — statues (lord/horse variants), dovecote,
  and any placeable garden/decorative props. A dovecote is not the eel pond's
  goose production building. Confirm original placeability and zero-worker
  behavior for assets absent from the current map corpus/menu decoder.
- [ ] **Scenario/editor structures** — outlaw camp, signpost, beacon and ruins,
  plus siege tents/wood/barrel props where editor-placeable. Verify per asset
  whether it is a placeable gameplay building, generated attachment or scenery;
  outlaw spawning and scripted actors are separate from estate employment.

Construction/ruined versions, six hovel appearances, alternative statue meshes,
linked sub-buildings and generated crop/garden patches do not each create a new
independent labor-building checkbox.

## Completion criteria for each worker building

Before checking a main item off:

1. Verify the required/max worker count from original data/reference behavior;
   record it in the building definition. Do not guess or make a required minimum
   an arbitrary tuning value. Record a verified zero where appropriate.
2. Implement staffing, travel, arrival, work start/stop, worker removal/death and
   replacement, using the shared labor manager and bounded path queue.
3. Bind the correct worker models, tools, carried goods, work animations and
   building-relative attachment positions. Ambient building animation alone is
   insufficient.
4. Implement inputs, target claims, production/action timing and finite local
   buffers; stop correctly when unstaffed, blocked or full. Use simulation time.
5. Implement pickup/delivery/service completion, resource accounting and visual
   state changes. Reuse estate storage queries and shared incremental deposit
   timing; honor each resource's stockpile/granary/pantry/armory destination.
6. Validate multiple competing workers, same-estate rules where applicable,
   removal/capture, pause/speed changes and scale. Keep per-frame work bounded
   and reuse shared render/animation resources.
7. Update this checklist with the implemented code and validation notes.

Suggested progression: pitch rig and simple food farms; wheat → mill → bakery;
hops → brewery → inn; wool/cloth and hive/candles; pantry/cooking chains; weapons
and armor; trade/transport; health/sanitation/fire; justice/religion; specialist
and household/event systems. The order can change with current map needs.

## Audit sources

- Engine status: `Assets/Code/Stronghold2/MapLoading/Labor/LaborBuildingDefinition.cs`
  (`LaborBuildingCatalog`), its five concrete definitions, `S2MLaborPlan`, and
  `Assets/Code/Simulation/Labor`.
- Placeable menu coverage:
  `Assets/Code/Stronghold2/S2MReader/Resources/MissionBuildingAvailability.cs`.
- Native types/assets: `S2MBuildingAssetCatalog`, installed
  `meshes/buildings/*.bdg`, and
  [building asset inventory](../FileFormats/S2M/S2GameSegment/Buildings/BuildingAssetInventory.md).
- Storage destinations:
  [GoodsList.md](../FileFormats/S2M/MapHeaderSegment/Lists/GoodsList.md) and the
  engine economy catalog. Existing storage support does not imply labor support.
- Reference behavior: local SH2-Reforged `Source/OpenSH2.Simulation/Kingdom`
  building/worker modules. In particular, FishPond covers eels/geese,
  GardenersHut covers vegetable plots, BeeHive is visited by chandlers, and
  specialist/event actors require classification instead of assumed peasant slots.

When a new building type is decoded or a previously unknown asset becomes
placeable, add it to the appropriate section rather than silently excluding it.

