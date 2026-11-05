# Apple farm labor

An `AppleFarm` has exactly one required `AppleFarmerJob`. Recruitment, estate labor
demand, actor ownership and game time use the shared labor infrastructure. The
job remains employed by its original farm while it can harvest other farms in
the same estate. Allegiance alone does not combine neighboring estates' trees
or food stores.

`S2MAppleOrchardPlan` matches serialized apple tree farm references to the saved
building references, not the nearest building. The final 20-byte AppleTree body
contains the farm reference at +0, stage at +4 and timer at +8. Campaign maps have
25 farms with 200 owned trees; some maps also contain stale tree records whose
farms no longer exist. These do not become harvest targets. A farm without saved
trees receives the native eight-tree layout. Planted state and timers are read
without modifying the source map records.

Facts checked against the SH2 Reforged farm/worker/tree references and installed
assets:

| State | Appearance | Behavior |
| --- | --- | --- |
| -1 | Hidden | Farmer must plant it |
| 0 | `apple_tree_sapling` | Five simulation seconds |
| 1 | `apple_tree_bare` | 12.5 seconds, with a stable stagger after regrowth |
| 2 | `apple_tree_blossom` | 12.5 seconds |
| 3 | `apple_tree` | 12.5 seconds |
| 4 | `apple_tree_apples` | Ripe; one farmer may reserve it |
| 5 | `apple_tree_bare` | Picked; waits for the farm's seasonal rest |

The farm begins a 120-second harvest window when all eight trees mature. The
window ends early when all are picked. Trees then rest for 12.5 seconds and
regrow; the farm's season clock has a 17.5-second cooldown. Growth runs through
one shared `AppleOrchards` environment even when no farmer is employed. It is
advanced once by `LaborManager`, independently of job or worker count. Timers
advance at boundaries without a per-tree Unity component or fixed 40 Hz loop.
The native 40-tick timings are converted to simulation seconds.

A farmer first plants its own unplanted trees, using the installed planting
clip. It then searches for the nearest reachable ripe, unclaimed tree in its
estate. Selection uses an incremental spatial bucket query, with a configurable
work budget per call. The chosen tree is reserved before requesting a route,
remains reserved during travel/picking, and is released on path failure, removal,
season expiry or successful harvest. Reservation races retry the remaining
trees instead of treating the orchard as empty.

Picking two trees yields exactly five apples. Picking/planting durations come
from the loaded animation clips (3.333 and 4.333 seconds respectively). Basket
attachments use the native unit definition and animation attachment slots.
`StorageLocator` generalizes the existing stockpile lookup to a requested storage
kind. Food deliveries discover a reachable, non-full same-estate granary through
the authoritative estate building registry and logical entrance geometry;
farmers keep no private list of stores. Newly placed granaries are discovered
automatically. Apples unload one at a time, at the native four-tick (0.1-second)
default interval, then the farmer searches from the delivery location. If no
tree is available it returns to its own farm centre and idles; it can select a
tree that ripens while returning. No granary means waiting with the existing
cargo. Removing the worker releases reservations and clears its carried load.

`Main.appleFarmSettings` controls growth/season times, movement/work speeds,
granary deposit/retry/idle timings and search budget. Worker count, two trees per
load and five apples per load follow the native requirements. Timers, movement,
growth and animations use the simulation clock, including speed and pause.

`S2MAppleOrchardView` uses the five original tree model/texture sets in shared
GPU instancing batches (at most 1023 instances per submission). It has no
GameObject per tree. Stage changes mark the shared matrices dirty; stable stages
reuse the existing arrays. The original static vegetation renderer excludes
only trees owned by these farms when dynamic orchard rendering is enabled,
avoiding duplicate trees while retaining independent decorative plant records.
Farm removal deactivates its trees and invalidates their reservations.
Apple trunk groups use `wood_walls.DDS` as an opaque surface; its alpha is not a
leaf opacity mask. The other tree groups retain their alpha-tested cards.
Apple Tree Model Scale on Main defaults to 1 and combines with Crop Model Scale,
independently of the forest Tree Model Scale (default 0.5). This restores apple
trees to twice their previous size without moving their logical/render anchors.

Validation: `Notes/tools/AppleFarmValidation.cs`, executed as
`AppleFarmValidation.Run` in an isolated Unity project. It checks ordinary and
budgeted navigation, reservation competition, cross-farm selection after
delivery, estate/storage restrictions, incremental deposits, planting and all
growth stages, seasonal regrowth, centre idling, removal, pause/speed, a shared
unstaffed orchard clock, campaign ownership and native models/animations/baskets.
