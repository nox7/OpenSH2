# Iron mine labor and hauling

An `IronMine` requires **two workers**, with no configurable reduced crew.
Each has an independent reservation and ground approach, supplied by normal
same-estate recruitment. Production waits for both to arrive. The building's
assigned/arrived/maximum counters use the existing `LaborBuilding` API.

## Reference timing and native assets

Factual SH2 Reforged hints: two miners; a local pile limit of ten; a 40 Hz
production sequence with beats 0, 140, 574, 688, 827, 1248, 1436 and a loop after
1576. This implementation independently translates those rules to simulation
seconds:

| Time | Action |
| --- | --- |
| 0 | Lever worker lowers the lift; the climber waits. |
| 3.5 | Climber descends. |
| 14.35 | Climber ascends by reversing the climbing clip. |
| 17.2 | Lever worker raises the lift; ore appears on it. |
| 20.675 | Climber carries ore to the furnace. |
| 31.2 | Worker returns; molten-iron part plays. |
| 35.9 | One iron enters the local output pile. |
| 39.425 | Next cycle starts. |

`iron_miner.uni` selects the original `stonemason.GR2` body, actions `WALK`,
`CLIMBING`, `TURNIN LEVER`, `MOVING IRON`, and `WALK BACK`, including its ore/tool
attachment definitions. Native climb, lever, carrying and return clips are
2.866667, 3.5, 10.5 and 2.633333 seconds. One-shot clips clamp at their endpoints
while waiting for the next production beat. Reverse playback is used for lift
lowering, climbing up, and the corresponding worker action.

`iron_mine_parts_lift.GR2`, `_rock.GR2` and `_molten.GR2` supply the moving parts.
Ore on the lift is visible during raising; molten iron during the final furnace
stage. All work poses and parts use the building origin, rotation and native
scale. The local pile uses the ten `stockpile/iron_N.GR2` count models at the
reference's building-local offset (1.75, 0.125, 0.298828125 map cells).

## Production state and customization

`IronMinerJob.Production` is shared by the two workers. It uses the minimum of
their credited work after travel, so work is neither doubled nor credited while
walking. `StoredIron`, `MaximumIron`, `IsFull`, and `IsWorking` expose the buffer.
At ten iron production stops. Hauling frees space and permits a new cycle.
Losing either worker resets incomplete work, preserving finished output and its
building-owned rendering. Normal recruitment fills the vacancy.

Main / Iron Mine Settings exposes cycle duration, local capacity (1..10), and
pile position. Crew size stays fixed. The default cycle is 39.425 game seconds;
changing it scales all stage times and animation rates together. Simulation
pause/speed applies to production, movement and delivery once. Rendering uses
shared GPU animations and instanced count variants; there is no Animator or
GameObject per miner, pile unit, or mechanism.

## Shared ox producer contract

Both mine and quarry implement `IOxProduction`. `OxHaulingSource` identifies its
resource, estate, pickup point and exclusive handler claim. The map's network
indexes both source types by estate; an empty ox chooses the nearest reachable
unassigned mine or quarry in its estate, including temporarily empty producers.
It keeps that claim while waiting to fill and releases it only on departure or
removal. Cargo cannot mix iron and stone, including a retry after a lost claim.

Iron defaults to **eight actual units per load**, collected one every 0.25 game
seconds, following the reference's mine loading cadence. Native
`oxen_iron_01.GR2` through `_08.GR2` provide its visible cargo on the pelvis-bound
carrying frame. Stone keeps its separate sixteen-unit capacity and eight visual
stages. Each resource is conserved one-for-one. Same-estate stockpile unloading
uses Main / Stockpile Delivery Settings / Seconds Per Item (default 0.15), one
iron or stone per transaction. Full stores retain cargo and retry; removal hides
the ox and frees the producer claim as before.

## Validation

`Notes/tools/IronMineValidation.cs` tests two-worker gating, each stage including
reverse playback, first output, shared-clock credit, pause/2x, capacity ten,
withdrawal/restart and worker replacement. It tests eight real iron pickups and
individual same-estate deposits without stone contamination. Its native-map run
finds `[Patched] Kill Barclay.s2m`, recruits miners and handlers, loads all worker,
attachment, part, iron-pile and ox cargo variants, renders each stage and the full
pile, and witnesses live iron delivery. Results are in ignored
`Temp/IronMineValidation`. Chapter 8 contains no placed iron mines; the existing
stone ox regression remains in `Notes/tools/OxHaulingValidation.cs`.
