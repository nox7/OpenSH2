# Building staffing and stone quarries

## Staffing API

`S2MLaborPlan.Buildings` contains a `LaborBuilding` for each placed building and
keep. Main exposes the active `LaborManager` as `main.Labor`; its `Buildings` list
and `TryGetBuilding(id, out building)` provide the same records.

```csharp
if (main.Labor.TryGetBuilding(buildingId, out var building))
{
    int current = building.TotalWorkers;  // Assigned, including workers walking there.
    int maximum = building.MaximumWorkers;
    int arrived = building.ArrivedWorkers; // Have arrived and started their role.
    bool staffed = building.IsFullyStaffed;
    bool ready = building.IsCrewReady;
    bool implemented = building.StaffingConfigured;
}
```

Each `LaborJob` is one worker slot, with its own reservation and approach. A
building can have several slots. `CreateJobs` on a building definition supplies
those slots; the original single-job `Create` API remains supported. Recruitment
polls every vacant slot until its building reaches capacity, chooses only seated
unassigned peasants in the same estate, and validates their route. Reserving one
slot immediately prevents another slot from selecting the same worker. Removal
vacates that slot and the next polls recruit a replacement.

A configured zero-slot building has `0/0` staffing, is fully staffed and requires
no peasant. Buildings without a labor implementation also have no active slots,
but are explicitly marked `StaffingConfigured == false`; their original worker
requirements have not been claimed as zero. Currently stockpiles explicitly require zero workers, saw pits have one slot and
stone quarries require three. Arrived means the worker has started its job,
not that it must remain inside the footprint (woodcutters continue to count while
traveling on their work cycle).

## Verified quarry facts

The reference project uses **three masons**, with separate hammering, lifting and
hook actions, and a local **48-block** maximum. Those factual hints are used by
an independent implementation. Native installed files independently confirm:

- `meshes/units/stone_mason.uni`, with `HAMMERING`, `LIFTING`, `HOOK`, and `WALK`.
- `stonemason_hammering.GR2`, `stonemason_lift.GR2`, `stonemason_hook.GR2`.
- Definition-selected hammer/chisel attachments for the hammering action.
- `meshes/buildings/quarry_parts.GR2`: sixteen animated mechanism meshes.
- `quarry_parts_rock.GR2`: the moving working rock.
- `quarry_rock_pose_00.GR2` through `_04.GR2`: rough to progressively chiseled bench rock.
- `quarry_stone.GR2`: one produced-stone block.

Once the crew is ready, lifting, hook work and the mechanism begin immediately.
The chiseler holds animation frame zero until **5.675 simulation seconds** have
elapsed (Reforged's tick 227 at 40 Hz). The first finished block is committed at
**15 seconds** (tick 600), then output and chiseling reset every **9.325 seconds**
(373 ticks). This independently implemented schedule uses the reference's timing
facts. It replaces the earlier 6.333-second start and premature hammer playback.

`CycleDurationSeconds` defaults to 9.325 and `InitialLoadingSeconds` to 5.675.
Lift, hook, crane and moving rock share an animation clock mapped so the moving
rock's native 6.333333-second endpoint occurs at the loading beat. Their remaining
return motion fills the rest of the 9.325-second cycle, without pausing. Hammering
runs through its native 9.333333-second clip over each output interval, offset by
the initial loading delay. Each new rock's arrival coincides with completion of
the preceding block. Losing a worker or restarting from full storage requires the
initial loading delay again; existing finished stone is preserved.

At the loading beat, rendering hands the incoming rock off to a separate static
bench rock, using `quarry_rock_pose_00.GR2` through `_04.GR2`. Their authored
positions align with the bench. This separate rock can remain during chiseling
while the lift workers start the next pickup.

Native inspection (`--inspect-animations`) reports zero text/event tracks in the
rig, working-rock and hammering clips. The four bench-model switches remain
estimates at 20%, 40%, 60% and 80% of chiseling, controlled by `RockStageFractions`.
Main / Quarry Settings exposes the cycle, initial loading delay and fractions.
The new timing field names supersede the old clip-end-based serialized defaults.
All clocks use `Game.Simulation`, including pause and speed changes. At capacity
the workers stop and both in-progress rocks are hidden.

## Shared production and local storage

All required workers must arrive before production can advance. Each worker
contributes only the work time left after traveling that step. `LaborManager`
flushes shared building work after all workers advance, taking the minimum of
those credits. Three workers therefore do not produce at three times the rate,
and a late arrival cannot contribute the whole frame's travel time as labor.

`QuarryJob.Production` is shared by all slots in that quarry. Query
`StoredStone`, `MaximumStone`, `IsFull`, and `IsWorking` there. At capacity,
production and work animations stop. `TryTakeStone(count)` is the bounded future
hauler interface. Opening room restarts production if the crew is complete;
losing a worker stops/reset the current cycle without deleting output. A later
replacement restores production. Withdrawals never make the amount negative.

Stone remains work-in-progress at the quarry. It is not credited to the estate's
stockpile inventory. [Ox handlers](OxHauling.md) collect 16 actual stone from this buffer, then deposit those same 16 units
one by one through the inventory transaction API. No resource multiplication occurs.

## Rendering and configuration

Worker work clips are authored in building coordinates, with distinct stations
already embedded in the motion. On arrival their rendering uses the quarry origin
and native building scale, rather than adding walking-tile offsets a second time.
Walking uses ordinary same-estate ground pathfinding and actor scale. Worker,
rig and output rendering use the shared GPU actor pipeline: no Animator per
mason or GameObject per output block. The rig and output belong to the building,
so removing the first mason cannot hide the remaining stone or mechanism.

Quarry staffing is fixed by `QuarryProduction.RequiredWorkers == 3`. Crew size is
not an Inspector setting; an incomplete crew cannot produce. Main / Quarry
Settings exposes capacity (1..48), continuous cycle timing, initial loading delay and stage fractions and stone layout.
Profiles are copied per quarry; reload the map to apply Main changes.

Finished stone now packs into a tight 3-by-3 grid on the front plank platform,
to the right of the chiseling station, with further layers above it. Native model
triangles place that platform at approximately Y 0.293, X 0.288..2.736 and Z
0.001..0.917 in building-local map cells. The finished-block mesh uses a 0.75
scale so the bank fits on the boards without intersecting the chisel workbench
or the opening behind it. Each layer starts with its center block and fills a square spiral around it.
The 48-block limit fills five complete layers and three blocks of a sixth. Blocks rotate 28.72 degrees about their mesh center to align their native
edges with the platform, using 0.22-cell grid spacing. Runtime transform changes
preserve the configured asset scale. This placement follows the user's original-game screenshot.

`Stone Layout` supplies odd `GridColumns` and `GridRows` (both default to 3),
spacing, layer height and XYZ offset. The new grid field names replace the previous
5-by-3 serialized dimensions so an existing Main component adopts the new layout. The removed `Workers` field is ignored on load;
staffing always uses the fixed requirement.

## Validation

`Notes/tools/QuarryValidation.cs` tests independent reservations, assigned/arrived
counts, explicit zero-worker and unimplemented buildings, incomplete crews, exact
continuous overlapping phase timing, exclusive bench-stage visibility and center-out layers, arrival fractions, pause and 2x speed, finite capacity,
withdrawal/restart, worker loss/replacement and a fixed three-worker crew with a one-block capacity override.
The visual regression checks all 48 block bounds against the front platform
and confirms the first layer rests on the boards. It also checks actual rendered
mesh edges for platform alignment, verifies rotation preserves the block center,
and checks block width against grid spacing. The chapter-8 run recruits nine distinct peasants into three quarries, loads all
native work/rig/stone variants, fills every quarry to 48, and verifies automatic
replacement/refill. Output survives worker removal and is not added to stockpile
inventory. Native working poses and full buffers are captured under ignored
`Temp/QuarryValidation`.

`Notes/tools/LaborValidation.cs` continues to verify all 200 recruit routes,
eight saw-pit slots plus nine quarry slots, doorway/stockpile navigation,
woodcutter cart assembly, zero-time updates and generic labor behavior.

