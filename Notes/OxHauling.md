# Ox tether labor and stone hauling

Every `OxTether` has exactly one worker slot. Recruitment uses the ordinary
same-estate peasant pool and ground route checks. A map-authored ox at that tether
is suppressed from the static actor list. The working ox becomes visible only
when the slot is assigned, stays at its post while its handler walks there, then
follows once the handler is within `HandlerPickupRadius`. No peasant means no ox.
Removal/death via `S2MPopulationController.RemoveActor` clears employment, quarry
claim and cargo, hides the entire ox assembly immediately and recruits a new
handler. The new ox starts at the original tether, never at its predecessor's
last delivery location. Tethers expose the shared assigned/arrived/max staffing API.

## Production and inventory

`OxHaulingNetwork` is shared by all tethers on the map. It indexes quarry and iron-mine pickup
points and walkable stockpile aisles, with explicit estate IDs. Handlers search
for the nearest reachable, unclaimed quarry or iron mine in their own estate, including one
whose output buffer is temporarily empty.
`QuarryProduction.TryReserveHauler` makes assignment exclusive; the claim lasts
through travel and collection and is released on departure or worker removal.
Unreachable producers are skipped. No same-estate stockpile with space means no
new collection. All pathfinding uses ordinary ground navigation and logical point
positions, with no clearance from the ox, frame or cargo bounds.

A handler collects one actual stone from the quarry every 0.5 simulation seconds,
up to **16 stone**. `CargoStone` counts actual goods; every pickup subtracts one
from `QuarryProduction.StoredStone`. There is no conversion or multiplier at
delivery: 16 removed from the quarry becomes exactly 16 added to inventory.
`VisibleLoadBlocks` is purely presentation: `(CargoStone + 1) / 2`, capped at 8.
Cargo 1 shows the first native stone model; cargo 15/16 shows the eighth model.
The native eight-model limit does not reduce logical carrying capacity.

A depleted quarry keeps its assigned handler waiting in place, retaining cargo
and its exclusive claim until production fills the ox. Other handlers skip that
quarry during the wait. Only a full ox leaves for storage, releasing the claim
after its departure route has been established. Inventory is credited only after
arrival, **one stone per deposit interval**, with the same shared setting used by
woodcutters. Full stores retain all undelivered cargo; even a single free slot
can accept a stone. Handlers retry reachable same-estate stockpiles as needed.
After unloading they find another producer and repeat.

Main / **Stockpile Delivery Settings / Seconds Per Item** defaults to 0.15 game
seconds and applies live to both wood planks and stone. `EstateInventory` owns
the shared settings reference so roles cannot silently use different defaults.
All waiting, loading, following and deposit timing uses simulation seconds;
pause freezes it and 2x game speed halves the real time between deposits.

The handler leads; the ox reuses its route and follows at a path-distance offset.
The initial join is pathfound once per trip; corners follow the stored waypoints,
not a straight line through walls. Handler arrival waits for the ox to be nearby
before loading or unloading. Navigation revisions trigger a replan only if either
route is no longer traversable. Work, movement, retry and transfer intervals use
simulation seconds, so pause and game speed apply consistently.

## Native assets and rendering

`ox_handler.uni` selects the original handler walk/idle models and clips. Its
version-4 definition has no team-group trailer; the catalog now handles that
format instead of reading past EOF. Versions 5 and 6 retain their trailer parsing.

The ox uses `oxen.GR2`, `oxen_walk.GR2` and `oxen_grazing.GR2`, with a 180-degree
local forward correction. Its original placed heading and position are retained; no angle is calculated
from the post origin. Tethers without a placed ox use the building heading. `oxen_stone_prop.GR2` supplies the carrying frame and
`oxen_stone_01.GR2` through `_08.GR2` supply the visible loads. The animal's
`oxen_stone.aml` identifies attachment point 1, direct skeleton bone 1, which is
`Bip01 Pelvis`. Both frame and cargo use that bone's animated world palette. They
are not placed independently at the animal's ground origin.

Idle/walk and load-count variants are preloaded and share native model/motion
caches. They render through GPU instancing and bone attachments: no Animator,
per-frame skeleton or GameObject per ox/frame/block. Asset preparation happens
at map load. Runtime changes select a preloaded variant. The ox, frame and load
all use a 1.25 multiplier on Main's existing unit scaling.

Main's **Ox Hauling Settings** exposes handler/ox speed, pickup/follow distance,
retry and loading intervals, actual stone capacity (1..16), and ox scale. The
one-worker requirement is fixed. Reload the map to apply those settings; the
shared stockpile deposit interval applies live. The old eight-block capacity
and per-ox unloading fields were removed so serialized values do not preserve
the obsolete doubling behavior.

## Validation

`Notes/tools/OxHaulingValidation.cs` tests staffing-gated spawn, waiting for the
handler, route following, quarry claim exclusivity across production waits, nearest
empty-quarry selection, full-load-only departure, sixteen-stone pickup and delivery conservation, one-at-a-time deposits,
shared interval changes, pause/2x timing, single-free-slot acceptance, foreign estate rejection, removal/replacement and pause. Its chapter-8
run recruits three handlers, loads all native variants, checks pelvis-bound
frames, delivers into same-estate stockpiles, and verifies immediate hiding and
replacement at the tether. The estate without a stockpile waits without taking
stone. Ignored `Temp/OxHaulingValidation` contains results and rendered previews.


Iron mines use the same exclusive producer contract, with a separate eight-unit
iron capacity and native iron cargo art. Handlers choose the nearest source of
either kind while empty and retain the resource type until unloaded. See
[IronMine.md](IronMine.md). Stone remains sixteen actual units per full load.
