# Saved walking surfaces

`ContextLayer` contains a counted field holding three grids of packed uint16
entries. Each grid begins with its int32 cell count, then `count * 2` bytes.
The containing field is `12 + count * 6` bytes long. The count is a square
backing grid; its side can exceed the playable terrain size and must remain the
indexing stride. An inherited ContextLayer type marker/name may precede the
field. `WalkingLayerReader` validates all three counts before exposing the data.

The low byte is the saved walking class. Existing actor decoding already used
it to distinguish ground placements from elevated placements. The upper byte
and all three uint16 grids are retained without reinterpreting their flags.

Observed classes used by initial ground pathfinding:

| Class | Observed use | Initial ground behavior |
| ---: | --- | --- |
| 0 | Open ground, including passages through stockpiles | Traversable |
| 1 | Solid buildings and stockpile goods regions | Blocked |
| 4 | Building/bridge walking floors, gate interior floors | Traversable |
| 10 | Gate passage boundaries | Traversable |

Other classes remain blocked in this initial ground module; multi-level wall
walking and teleport/stair transitions are not implemented. This is an observed
subset, not a complete recovered original-engine walking-class enum.

Saved surfaces take precedence over projected roof geometry and rectangular
building placement margins, but do not override solid walls. Ground navigation
combines them with the wall layer and body-height barriers sampled from visible
building walls/parts. This preserves archways while blocking rear walls and
excludes opaque collision proxies that seal real doorways. Gate floors use the
primary ground heights to avoid mistaking a gate roof for its ground passage.
The initial renderer retracts grilles/omits shut leaves for declared class-10
passages so routing and presentation agree. Live gate control is not decoded.
Terrain step-height checks and the campfire obstacle still apply. If a map lacks
a recognized ContextLayer, navigation uses its geometry/footprint fallback.

Chapter 8's first stockpile has blocked class-1 goods at `(42,28)` and an open
class-0 aisle at `(42,31)`. The validation route crosses that aisle from
`(38.5,31.5)` to `(44.5,31.5)`. Its 200 recruit routes remain connected when
the saved surfaces, including classes 4 and 10 at gatehouses, are used.
