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

Saved surfaces are now authoritative for ordinary building navigation. Decorative
mesh triangles, open door leaves, roof overhangs and model bounds do not veto a
saved open tile. This follows the tile-based evidence in the map rather than
claiming to reproduce the unrecovered original pathfinding algorithm.
The wall layer, water/bridge handling, terrain steps and dynamic foliage blockers
still apply. Declared gate passages use ground heights; resolved shut gate parts
remain explicit barriers. `PopulationSettings.PreferSavedWalkingSurfaces` defaults
to true; false retains the previous geometry overlay for diagnostics. Maps without
a recognized ContextLayer use geometry/footprint fallback.

These are walking classifications, not another elevation heightmap. The stockpile
stripe is an authored open class inside a blocked goods footprint. Higher walking
levels, the packed upper-byte flags and the full class enum are still undecoded;
this change does not claim complete original-engine navigation parity.

Chapter 8's first stockpile has blocked class-1 goods at `(42,28)` and an open
class-0 aisle at `(42,31)`. The validation route crosses that aisle from
`(38.5,31.5)` to `(44.5,31.5)`. Its 200 recruit routes remain connected when
the saved surfaces, including classes 4 and 10 at gatehouses, are used.
