# Natural stone under buildings

Natural-stone models are now temporarily suppressed when their map-cell anchors
are occupied by a building footprint. Their decoded records, resource data,
generated meshes and materials remain intact. Removing the building restores the
same models, without reloading or reconverting assets. This currently applies only
to NaturalStone; decorative rocks, vegetation and grass keep their existing behavior.

The implementation uses the saved Building rectangle, including its quarter-turn
origin convention, rather than mesh bounds or colliders. Footprints are half-open
in unflipped map cells. Stone anchors use `floor(MapX), floor(MapZ)`. Model scaling,
game scaling and renderer flips do not change which cells are covered. This is a
cell-occupancy implementation of the observed original-game behavior; the precise
original NaturalStone visibility routine has not been reverse engineered.

## Runtime lifetime

`S2MBuildingOccupancy` is the mutable C# occupancy model. It starts from loaded
map placements, even when a building's model is unsupported. Overlapping buildings
increment coverage counts; removing one does not expose stone still covered by
another. `SetBuilding(placement)` adds, moves or updates a footprint;
`RemoveBuilding(id)` releases it. Neither operation mutates the loaded S2M.

Building and vegetation renderers share a `S2MSceneryOcclusion` context on their
common map-world parent. Either renderer can load first. Keep rendering also
participates when used independently under that same parent. Separate map-world
parents have independent occupancy.

Rendered buildings and keeps now have individual `... Building` lifetime roots.
Each contains its exterior, interior view, static parts and collision instances.
The generated meshes/materials remain owned by their shared asset batch. Destroying
or disabling a lifetime root unregisters its footprint; re-enabling it restores
coverage. Interior/exterior switching only toggles children, preserving occupancy
and collision lifetime. Replacement renderers cannot be unregistered by the old
renderer being destroyed afterward.

For future simulation-driven construction, initialize a new root's
`S2MBuildingPresence` with the world's `S2MSceneryOcclusion.Occupancy` and its
`BuildingPlacement`, or update the occupancy model directly. Changing a Unity
transform alone does not move the logical footprint; call `SetBuilding` with the
new placement. Unsupported non-quarter-turn footprints are not guessed.

`S2MNaturalStoneVisibility` indexes the rendered stones by anchor cell and sets
`Renderer.forceRenderingOff`, suppressing color/depth/shadow rendering together.
Footprint changes visit only affected cells. There are no per-frame scene searches,
physics queries, overlap scans or visibility updates. Destroying the vegetation
root removes its event subscription.

## Validation

`Notes/tools/NaturalStoneOcclusionValidation.cs` passed in Unity 6000.4.7f1 with
the actual `war_chapter8.s2m` models: 30 hidden stone renderers on load, all 176
natural-stone placements retained. Tests cover a real quarry's disable, re-enable,
destruction and new placement, both building/vegetation load orders, different
scenery categories, interior-view changes, and removal of all building coverage.
Rotated/scaled/flipped map checks also verify that regrouping did not move building
geometry or collision meshes. Synthetic cases cover overlaps, all four quarter
turns, half-open boundaries, clipped map edges, footprint moves, repeated updates
and removals, and replacement-owner cleanup.

The ignored test project is `Temp/WallValidationProject`; results are written to
`Temp/StoneOcclusionProbe/validation.txt`.
