# Estate storage

`Assets.Code.Simulation.Economy.EstateInventory` is the simulation authority.
Main exposes the loaded instance as `main.Inventory`; `Game.Inventory` refers to
that same instance. It is created per loaded map by `S2MStoragePlan`, independently
of rendering. Every represented estate can query every known good, returning zero
when nothing is stored. Goods preserve the zero-based indices in `GoodsList.md`;
UNKNOWN entries have no enum members and are rejected by the catalog. This is a
separate namespace from the legacy file-reader `GoodsEnum`.

| Responsible store | Goods |
| --- | --- |
| Stockpile | Wood, Stone, Iron, Wheat, Flour, Hops, Ale, Grapes, Pitch, Candles, Wool, Cloth |
| Lord's Pantry | Eels, Geese, Pigs, Vegetables, Wine |
| Granary | Apples, Bread, Cheese, Meat |
| Armory | Bows, Crossbows, Swords, Maces, Pikes, Spears, MetalArmor, LeatherArmor |

The native `ServantsQuarters`/`servants_quarters_pantry` placement is the Lord's
Pantry, `Armoury` is the armory. Other recognized stores are `StockPile` and
`Granary`. Building estate comes from the saved estate layer, using the same
footprint-based lookup as labor and livery. Resources belong to an estate, with
their physical responsibility retained in a specific building.

## Queries and transactions

```csharp
using Assets.Code.Simulation.Economy;

var inventory = main.Inventory; // Available after the map has loaded.
long wood = inventory.Estate(11)[Goods.Wood];
long apples = inventory.Amount(11, Goods.Apples);
var storageKind = inventory.Catalog[Goods.Wood].Storage; // Stockpile

if (inventory.TryGetBuilding(buildingId, out var building))
{
    long storedHere = building.Amount(Goods.Wood);
    long availableRoom = building.FreeCapacity(Goods.Wood);
    Pallet first = building.GetPallet(0); // Stockpiles only; immutable value.
}

bool accepted = inventory.TryDeposit(buildingId, 11, Goods.Wood, 16);
bool taken = inventory.TryWithdraw(buildingId, Goods.Wood, 8);
bool moved = inventory.TryTransfer(sourceBuilding, targetBuilding, Goods.Wood, 8);
```

Deposits verify the exact estate and the responsible building category. They are
all-or-nothing: insufficient space, a wrong estate or a wrong store changes
nothing. Partial pallets of the same resource fill before empty pallets are
claimed; two different resources never share a pallet. Withdrawals empty the last
matching pallets first and release their type when emptied. Explicit transfers
preflight both sides, including aggregate overflow checks, then commit together.
Cross-estate transfers are explicit economy operations; worker deliveries cannot
use them to bypass the same-estate restriction.

Per-building amounts and per-estate totals are cached in 64-bit counters, so
ordinary queries are O(1). Pallet counts remain bounded integers. Mutations update
slots and totals before raising `Changed(BuildingStorage)`; subscribers see a
consistent result. Rendering receives only change notifications and coalesces
multiple deliveries into one layout rebuild. All mutation runs on the simulation
thread; this service is not a concurrently writable dictionary. Building removal
is explicit via `TryRemoveEmptyBuilding`, which refuses to discard resources.

Pantry/granary/armory quantities are per-building bins. They currently have no
invented original-game capacity limits; only stockpile pallets enforce recovered
limits. Map registration starts empty: original save/mission starting-stock
import is not implemented here. Such an importer can seed the same transaction
API, rather than giving rendering or mission code a second inventory authority.

## Native stockpile pallets

Stockpiles have 30 pallets: two 3-column by 5-row banks, with the middle column
kept clear for walking. Their building-local tile positions turn with the
stockpile; models retain their authored upright orientation. Each occupied slot
has exactly one resource and one `palette.GR2` base. Empty slots do not draw a
pallet. The saved walking grid already describes the blocked goods banks and
open aisle; models do not add collision or actor clearance.

| Resource | Units per pallet |
| --- | ---: |
| Wood, Iron | 36 |
| Stone | 96 |
| Wheat, Flour | 32 |
| Grapes | 15 |
| Cloth | 25 |
| Hops, Ale, Pitch, Candles, Wool | 16 |

These capacities and the 30-slot layout were checked against the reference
project as factual hints; the storage/renderer implementation is independent.
Native assets were inspected separately from the installed game. The wood partial
variants represent one through five units, with cumulative layers for multiples
of six. A 16-unit pile uses the 12-unit layer plus the 4-unit partial at the next
height. Iron similarly uses cumulative 12-unit layers and partials, and cloth has
native per-count variants. Other stockpile goods use their native item models in
compact per-pallet grids; those packing arrangements are configurable, rather
than a claim to reproduce every original game's partial-pile arrangement.

Main's Storage Settings adjusts pallet count (1..30 on native stockpiles) and
per-resource pallet limits. Stockpile Visual Settings overrides native model
paths, columns, rows and item scale. Reload the map for these changes. Native
assets are loaded/converted once through the existing cache pipeline; no installed
files are modified. Meshes, materials and textures are shared across all pallets.
The GPU view uses instanced batches of up to 256 pieces, with no per-pallet
GameObjects or per-pallet scripts. Unchanged frames allocate no managed memory.
Unit model scale does not change pallet size; map scale, axes, building rotations
and vertical terrain scaling are handled by the map adapter.

## Woodcutter delivery

A processed cart starts with `WoodcutterJob.PlanksPerDelivery == 16` units of
`Goods.Wood`. Main / Woodcutting Settings / **Plank Unload Seconds** controls the
interval between individual deposits (default **0.15 simulation seconds**).
The first plank is credited after one full interval; all 16 take 2.4 game seconds.
The shared game clock scales this timer just like movement and sawing.

Arrival enters `Unloading`, with `RemainingPlanks` tracking the actual cargo.
Every interval commits exactly one unit to the selected same-estate stockpile,
decrements remaining cargo, and increments `DeliveredPlanks`. `CompletedDeliveries`
increments once the cart is empty. Inventory change events make the stockpile's
visible pile grow incrementally; changes in the same frame are coalesced into one
render-layout rebuild. The cart retains the native loaded visual until emptied.

Destination selection needs room for one plank, rather than an entire cart. If
another worker fills a stockpile partway through unloading, the worker retains
the undelivered count and retries reachable same-estate stores. Withdrawing stock
can allow unloading to resume. There is no partial-loss or whole-cart credit.

After the final plank the cart becomes empty and automatically resumes work from
its actual stockpile position. The foliage registry sends the worker back to its
assigned log pile while logs remain, skipping felling for an existing pile.
Once that pile is exhausted, the worker reserves new available foliage. Each
collected log still returns to the original saw pit, is sawn for the configured
production time, and becomes another 16-plank delivery. Missing resources or
storage retain the job and retry; worker removal releases its resource claim.

Large simulation steps consume multiple unload intervals and repeated work
cycles without discarding elapsed work time at an arbitrary transition cap.
`AtStockpile` and `ResumeAfterUnloading` remain compatibility hooks for legacy
jobs without a storage service. The hook cannot discard remaining planks in a
storage-backed job; the normal production loop no longer needs an external
resume call.

## Validation

`Notes/tools/StorageValidation.cs` validates all 29 goods, category ownership,
UNKNOWN filtering, packing/caps, release/reuse, atomic failure, withdrawals,
transfers and cached totals across 1,000 stores. It then runs
`Notes/tools/IncrementalDeliveryValidation.cs` for timed unloading and automatic
work cycles: early-credit rejection, interval changes, pause/speed, competing
partial unloads, capacity recovery, removal, same-pile reuse and new foliage.
Twelve complete synthetic cycles produce 192 planks with both large and small
ticks. The chapter-8 run verifies six repeating workers and two missing-store
waits, recording 352 individual deposits before the new-foliage check completes.

`Notes/tools/StockpileVisualValidation.cs` loads original pallet and goods
models, exercises both banks and all stockpile goods, and checks unchanged-frame
allocations. Its 21-pallet sample draws 288 pieces in 13 instanced calls with
zero managed bytes over 30 unchanged frames. Reports and previews are under
ignored `Temp/StorageValidation`.


Quarry output remains a local work-in-progress buffer (`QuarryJob.Production`),
separate from this spendable inventory. See [Quarry.md](Quarry.md) and [OxHauling.md](OxHauling.md). Ox handlers collect
16 actual stone and credit the same 16 units individually on delivery.


## Shared stockpile deposit timing

Main / Stockpile Delivery Settings / Seconds Per Item controls both woodcutters
and ox handlers, defaulting to one item per 0.15 simulation seconds. The active
`EstateInventory.DeliverySettings` retains that shared settings object, so changes
apply live to all deliveries. The old woodcutter-only `PlankUnloadSeconds` and
per-ox unload interval are replaced by this setting. Pause and game speed affect
the simulation time consumed, not the resource quantities. Each labor delivery transaction
adds one deposited plank or one deposited stone.
