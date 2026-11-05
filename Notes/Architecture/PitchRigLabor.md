# Pitch rig labor

`PitchRigBuildingDefinition` registers one required `PitchWorkerJob`. It uses the
existing estate labor demand and recruitment system; map-start `PitchWorker`
placeholders do not fill that vacancy. A rig requires pitch swamp under all four
cells of its 2×2 extraction footprint. Swamp is not depleted by extraction.

The native reference (`PitchWorkerUnit`, `CraftTrade.PitchRig`) specifies one
worker, five pumping loops, and two pitch carried directly to a stockpile. There
is no local production pile or ox collection. The installed worker scoop and rig
scooping clips both last 1.333333 seconds and contain no text-track events. The
worker and animated rig part share the job's pumping clock. The actual loaded
worker clip duration determines extraction time; at normal speed a batch takes
approximately 6.667 simulation seconds, plus travel.
The pumping clip is authored in building coordinates and uses the rig origin,
rotation and scale, independently of the logical worker interaction point.

Outside the rig, movement uses ordinary ground navigation and the shared bounded
pathfinding queue. The native short entry/exit maneuver crosses the known rig
platform directly through a pier waypoint; swamp remains blocked for general
navigation. Platform coordinates use rotated building-local `(1.125, 0)` and
`(1.125, 1.125)`, with the native worker floor of 382 raw height units.

After pumping, the worker carries exactly two pitch in a full pot. The shared
`StockpileLocator` queries the authoritative inventory for reachable, non-full
same-estate stockpiles. Deposits use the shared `StockpileDeliverySettings`
interval and commit one pitch at a time. No store or no capacity means waiting
with the existing cargo, with bounded retries. A later registered stockpile is
automatically discovered. After unloading the worker returns and repeats. Worker
removal cancels pending path requests, clears carried goods, and stops the rig;
replacement staff begin a new cycle.

`Main.pitchRigSettings` exposes walking speed, pumping animation speed and retry
interval. Worker count and batch size follow the native requirements. All action
timers and movement consume simulation seconds, and rendering uses the same
simulation clock, so game speed and pause apply once.

`S2MPitchRigLayout` reads pitch swamp corners from the water height plane, seats
the visible rig just above the highest covered corner (0.02 map-world units),
and supplies the same base to its worker and animated scoop. This is a rendering
anchor: it does not rewrite saved terrain, walking surfaces or swamp navigation.
The scoop is supplied by the labor presentation, replacing the old static part,
and remains stationary when unstaffed or between pumping cycles.

Validation entry point: `Notes/tools/PitchRigValidation.cs` (`PitchRigValidation.Run`
in an isolated Unity editor project). It exercises synchronous and budgeted
navigation, production counts, estate restrictions, incremental deposits,
repeat/removal behavior, simulation time, native clips/attachments and real-map
swamp anchors.
