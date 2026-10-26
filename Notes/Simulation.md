# Simulation foundation

Gameplay rules live in `Assets/Code/Simulation` under `Assets.Code.Simulation`.
These classes do not depend on Unity. Map adapters stay under
`Stronghold2/MapLoading`, asset conversion under `AssetLoading`, and GPU/scene
objects under `ModelRendering`. Future workers, recruitment, jobs, economy, and
daily updates should extend simulation state rather than use renderer objects
as the authoritative population.

`Game.Simulation` owns a `SimulationClock`; `Game.IsPaused` forwards to it.
`Main.Update` toggles pause on P using the configured Input System and advances
the clock with unscaled frame time. Actor, building ambient, and campfire
animations consume its delta. Paused frames advance by zero and resume without
catching up elapsed wall time. Unity global time scale is untouched, allowing
camera and interface interaction. Main's existing map-test startup remains the
active entry point.

Each keep gets a `CampfirePopulation`: eight seats plus eight per Hovel whose
footprint center belongs to the keep's estate in `RadarEstateLayer`. Estate IDs
remain distinct even when two estates have the same lord. Buildings in other
estates do not contribute. Unknown estate membership contributes no hovels.
Population starts empty. Each kingdom recruits one peasant every five simulation
seconds by default. The first eight start at the keep's stair base and run to
their seats. Subsequent recruits are distributed across that estate's hovels,
begin seated inside a hovel, stand up, leave it, then run to the campfire.
`PeasantRecruitment` owns recruitment timing and campfire lifecycle; each actor
has separate movement and worker components. Popularity, migration, building construction/destruction and
ownership changes remain future simulation work.

Main exposes **Population Settings**. Defaults are:

| Setting | Default | Units |
| --- | ---: | --- |
| Spawn Interval Seconds | 5 | simulation seconds per kingdom |
| Hovel Seated Seconds | 2 | simulation seconds before standing |
| Run Speed Cells Per Second | 2 | map cells per simulation second |
| Walk Speed Cells Per Second | 0.85 | worker travel in map cells per simulation second |
| First Ring Radius | 0.9 | map cells |
| Ring Spacing | 0.55 | map cells between rings |
| Seat Spacing | 0.65 | map cells along a ring |
| Keep Stair Base Offset | 0.35 | map cells outside the local entrance side |
| Hovel Door Offset | 0.25 | map cells outside the building |
| Maximum Step Height | 0.4 | map height units (1024 raw units = 1) |
| Job Assignment Interval Seconds | 3 | simulation seconds between vacancy polls |

Ring capacities follow circumference divided by seat spacing (initially 8, 14,
19, ...), with staggered alternate rings and inward headings. Obstructed or
disconnected seats are skipped; additional rings supply remaining capacity.
These are adjustable OpenSH2 values, not recovered original executable constants.
Reload the map after changing layout/entrance/navigation settings.

Peasants use `peasant.GR2`, three `peasant_campfire*.GR2` clips, `hovel_peasant`,
`church_standup` (a four-second stand transition), `worker_run` and `worker_idle`.
The renderer preloads clip variants for each simulation ID, hides unspawned
actors, and selects one variant per active actor. Visible matrices and playback
parameters are compacted together for instanced draws. Transitions are governed
by simulation time; pausing freezes recruitment, transition timers and movement.
Synthetic actors remain separate from saved map actors and honor map flips,
terrain heights, Game Scale and Unit Model Scale.

The inspected wooden/medium keep models contain only a single root bone, with
no separate doorway/stair marker exposed. Keep spawns therefore use the rotated
entrance side plus the adjustable stair offset, snapped to nearby walkable
ground. Hovel interior/exit points likewise derive from the building transform.
When the preferred entrance opens into an isolated pocket, the closest reachable
side of that hovel is used. This is an initial placement policy; original BDG
door helpers have not been decoded.

## Ground navigation

`Assets.Code.Simulation.Navigation.NavigationGrid` is independent of Unity and
S2M parsing. It provides A* with eight neighbours, an octile heuristic, a binary
heap, cached connectivity regions, maximum step-height checks, no diagonal
corner cutting, and conservative line-of-sight smoothing. Disconnected routes
are rejected without searching the entire map. No route means the peasant waits
and retries every five simulation seconds, rather than teleporting to a seat.

`S2MNavigationBuilder` builds quarter-cell nodes from the loaded map. The decoded
`WalkingLayer` preserves all three packed ContextLayer grids and their original
backing stride. Its low byte identifies the walking surface: observed classes
0 (ground), 4 (building/bridge floor), and 10 (gate passage) are traversable by
this ground module. Stockpile goods cells (class 1) remain blocked while class-0
aisles remain open, including underneath projected roof/building geometry. Upper
bytes and the other two walking layers remain preserved for later interpretation.
Terrain height comes from interpolated walking corners; gate passages sample
primary ground rather than the roof surface. Saved classes identify candidate
surfaces; they cannot reopen solid map walls or building walls. A separate
`S2MBuildingObstacleRasterizer` samples visible walls and parts at character-body
heights with 0.18-cell clearance. Roofs over passages, walking ramps and short
stair risers are not wall barriers. Visible geometry is used because some
collision proxies seal genuine openings. Fire centers and steep steps still
block travel. Dynamic changes to goods or walkability are not yet simulated.

`S2MGatePartState` keeps initial presentation and navigation consistent for saved
class-10 passages. Such gates start open: grilles retract above their openings;
shut door leaves are omitted until hinge animation is decoded. Non-passage gate
parts remain barriers. This is an OpenSH2 initial policy based on saved portal
walkability, not a decoded gate-control state or a live opening/closing system.

Without a saved walking grid, solid building model surfaces are projected into
obstacles, leaving empty reserved placement margins available; unsupported
buildings fall back to footprints. Water, pits/cutouts and walls block travel;
completed bridges retain their walking surface and gatehouses receive a central
ground passage. Farmland and ground-only markers are not solid buildings.
Hovel egress is a short, explicit segment from inside its source building to
its exit, followed by a regular ground route to the seat.

This first module covers static ground navigation. Live gate opening/closing,
wall-walk layers, ladders/teleports, dynamic obstacle updates and avoidance
between moving actors are not implemented. Grid changes require rebuilding
navigation and plans.

## Woodcutter labor

Labor is componentized into reusable simulation contracts and separate game-file
adapters/presenters:

| Component | Responsibility |
| --- | --- |
| `MovementComponent` | Position, heading, following paths, and speed-based movement; shared by recruits and workers |
| `WorkerComponent` | Assignment, travel, exact-step arrival, and advancing the assigned job's work cycle |
| `LaborJob` | Abstract building-job contract, reservation, work position, arrival hook and work-cycle hook |
| `WoodcutterJob : LaborJob` | Woodcutter role and cart/workstation data; future felling/hauling mechanics belong here |
| `LaborManager` | Estate-scoped vacancy polling for any `LaborJob`; no woodcutter-specific movement or boarding logic |
| `LaborBuildingDefinition` subclasses | Adapt each S2M building type and its startup placeholders into jobs |
| `LaborPresentation` subclasses | Per-role assets, travel/work variants and optional equipment actors |

New jobs add a `LaborJob` subclass, a building definition and a presentation,
registered in their catalogs. Recruitment, scheduling, worker travel and the
population renderer do not need a new building-type switch. Simulation classes
remain independent of Unity and original asset paths. Workers compose movement
and employment components rather than inheriting the recruitment state machine.
Presenters can override worker placements and phase-specific variants, playback
timers, loop settings and animation revisions for harvesting/hauling/forging
cycles. Equipment transforms are synchronized while working, allowing job-owned
equipment movement to be added without a renderer-specific woodcutter branch.

`Assets.Code.Simulation.Labor.LaborManager` polls vacancies every three simulation
seconds (customizable). Every SawPit starts with one vacant `WoodcutterJob`.
Only seated, unassigned peasants in that exact estate can be selected. A worker
and vacancy are reserved together after a valid route is found; an unreachable
job remains vacant, and workers cannot be assigned twice or borrowed from another
estate. Scheduling and travel use the simulation clock and stop on pause.

The worker changes immediately from the peasant model to `woodcutter.GR2`, walks
to the saw pit's approach, then switches instantly to the original
`woodcutter_riding_cart.GR2` pose as horse/cart equipment appears. Arrival marks
the job occupied on the terminal movement step, including when no frame time
remains. There is no mounting timer, vertical tween or intermediate boarding
state. No felling,
hauling, lumber production or resource delivery is implemented yet.

`S2MLaborPlan` owns map-start actor filtering separately from saved actor data.
An editor HorseCart at the saw pit's authored cart-side position is treated as a
placeholder, suppressed at startup, and replaced by the simulation-owned cart.
Matching nearby WoodCutter placeholders are suppressed as well. Unrelated carts
remain intact. Each job has a unique synthetic cart actor ID and no initial
worker. Cart-side anchors derive from the building transform, preferring the
matching editor cart's saved heading/height; they are not decoded BDG helpers.

Horse, cart body and wheels share a renderer variant, so selecting that
variant shows the full assembly. The rigid body and wheels use their bind meshes:
both GR2 files contain embedded authoring animations that otherwise lower the
body and permute the wheel axes. `UseBindPose` creates identity skin palettes
without modifying the shared animation cache. Variant switching also honors
`HoldPose`, so rigid parts remain static while horse/rider animation continues.
The wheel axle is on local X, with its center at 1.04 actor units; the deck stays
at approximately 1.15. No extra roll is applied. Shared body/wheel recipes apply
the same correction to ordinary placed HorseCart assemblies.

Main also exposes **Horse Cart Settings → Horse Forward Offset**, initially 2.5
actor units along local +Z (previously 1). Only the horse receives this offset.
The rider keeps local Z = -0.7, and the cart body/wheels retain their existing
offsets. This places the shaft ends near the horse's torso while preserving the
rider position requested in the reference screenshot. Reload the map after
changing assembly settings. Walking and sprinting remain independently tunable
under Population Settings; walking animation playback follows the walking speed.

Campfires render the original `camp_fire.GR2`, including its embers material,
plus crossed additive cards using `meshes/misc/fire_orange.tga`. The fire shader
scrolls the original texture and varies intensity using simulation time. It is
stored in Resources so player builds retain it. This is an initial fire effect,
not a decoded original particle-emitter implementation.
The campfire model occupies a one-cell footprint from its saved origin; flames
and seat rings use its center, half a cell along both map axes. Seated model
orientation is converted from the original local -Z facing to simulation +Z
headings.

Validation scripts are `Notes/tools/SimulationValidation.cs` (asset/anchor and
pause checks) and `Notes/tools/RecruitmentValidation.cs` (timing, state machine,
A*, path safety, clip visibility and real-map routes) for an isolated Unity project.

Validated in Unity 6000.4.7f1: 44 serialized keep campfire anchors across 113
local map files; chapter 8 capacities of 8, 48, 16, 88 and 40 (200 peasants);
three seated animation batches; pause/resume without catch-up; and visual
alignment of the wooden keep, campfire and eight inward-facing peasants.
The full C# source compiles with the Input System enabled (zero errors).
Recruitment validation also confirms 200/200 chapter 8 routes, all 200 recruits
eventually seated, one visible clip variant per recruit, first-eight keep
ordering, hovel seated/standing/running transitions and paused movement/timers.
`Notes/tools/LaborValidation.cs` verifies saved stockpile aisles and blocked goods,
200 recruitment routes with saved walkability, eight initially vacant saw pits,
startup placeholder suppression, same-estate staffing, worker travel/instant arrival,
grouped cart visibility, custom polling intervals, pause, duplicate-assignment
protection and rejection of unreachable jobs. Chapter 8's eight saw pits all
reach occupied state with distinct workers.
Wall regressions additionally check that a roofed treasury doorway stays open,
its rear wall blocks direct and smoothed paths, and a floor class cannot reopen
a map wall. All 83 chapter 8 worker-route segments were checked against actual
treasury mesh colliders, including backfaces. Stockpile access, 200 recruit
routes and eight staffed saw pits remain valid with the structural barriers.
Component regressions also verify polymorphic arrival/work hooks, exact-step
instant mounting, independent walk/run distances, occupied-job rejection, and
horse-only assembly changes with a fixed rider transform.
