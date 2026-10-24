# Map-level building loading

The game loop loads supported structures through one `S2MBuildingAssetLoader.Load`
call and renders them through one `S2MBuildingRenderer.Render` call. Main does not
instantiate individual keep or bridge loaders/renderers.

The map-level loader currently delegates to:

- `S2MKeepAssetLoader`: four keep families, with shared exterior/interior/table/
  collision data and placements grouped by size.
- `S2MPlacedBuildingAssetLoader`: catalog-driven ordinary buildings, grouped by BDG;
  active exterior/interior/collision files come from the descriptor.
- `S2MBridgeAssetLoader`: supported completed modular bridge spans. Construction
  bridges retain their previous behavior.

Further building families belong behind these map-level entry points. Specialized
loaders can retain their own asset/assembly rules without adding calls to Main.
See [BuildingAssetInventory.md](BuildingAssetInventory.md) for the complete local-map
inventory and [BuildingAssetReview.md](BuildingAssetReview.md) for unresolved cases.
Landscape tag 18 is decoded into a separate saved cutout grid; see
[TerrainCutouts.md](TerrainCutouts.md). The building loader authorizes cutouts
inside successfully loaded building footprints, excluding bridge riverbeds.
The terrain renderer omits these cells from both visual and collider meshes.
Farm soil is handled independently through building-owned material/orientation
overrides reconstructed from Landscape tag 19; see [FarmSurfaces.md](FarmSurfaces.md).
Removing a patch restores ordinary terrain appearance without changing source data.
Concrete building payloads remain raw; only the shared placement and Hovel
appearance block and verified composite-building references are decoded for
ordinary buildings. [CompositeBuildings.md](CompositeBuildings.md) describes
kitchen/pantry and courthouse/dungeon pairing.

The full map-recorded `BuildingPlacement.FootprintWidth`/`FootprintHeight` are
retained in `S2MFile.BuildingPlacements` and in each loaded batch's placements.
These dimensions already account for rotation and are never replaced with BDG
dimensions or trimmed to visible mesh bounds. The larger rectangles likely
include clearance that prevents neighboring buildings from being placed too
close, consistent with the original game's colored placement squares. Keep this
reserved area for future placement-overlap checks and debugging, including for
buildings whose meshes are unsupported. The exact clearance rules still need
verification; a reserved placement cell is not necessarily blocked for units.
Mesh colliders and the original walking heights remain separate data.

`S2MBuildingData` returns loaded family data, occupied building rectangles, and a
`PrimaryHeightCells` render mask indexed `z * terrainWidth + x`. This combines building
footprints with bridge coverage, drawing primary ground/riverbed heights rather
than the elevated walking floors, stairs, and decks. The bridge-only mask is
copied before adding keep footprints so it remains independent.

Footprints come from common `S2MFile.BuildingPlacements` metadata, including
buildings whose visual assets are not implemented. Selecting ground must not
depend on whether a building mesh happens to be supported. The building loader
finishes before terrain texture loading and geometry generation.
The same mask drives height sampling and derived slope materials. It is applied to the
decoded grid before renderer axis flips, so optional flips and Game Scale affect
buildings and their supporting terrain together. All ground triangles/colliders
remain in unmarked cells, including partially exposed cells around entrances.
Authorized tag-18 cutouts are omitted separately. Original map heights,
raised navigation corner planes, and material IDs are retained without mutation.

Keep footprints use the dimensions recorded in the inherited Building block, not
visual mesh bounds (which can include banners, stairs, and overhangs). Their model
origin rotates about a different corner at each quarter turn; the rectangle's
minimum must be adjusted accordingly. Map-edge rectangles are clipped to the
playable terrain, and overlapping rectangles are combined as a union.

`BuildingPlacementReader` decodes the shared first inherited payload: a counted
52-byte Building block with optional inline `Building` type registration. It
requires a confirmed segment-local Building type index (from an inline name or
known keep), positive dimensions, and finite transforms. A matching byte length
alone is not sufficient. This common decoder also supplies KeepReader, avoiding
separate parsing rules per concrete building family. Unknown concrete objects
retain their original raw payload and are not promoted to fully decoded buildings.

Chapter 8 has 246 common building placements, including three 6x5 StoneBarracks
footprints. Their original ground is flat but navigation corners are elevated.
The renderer selects their ground and loads their barracks meshes through the
generic BDG-backed loader.
Unsupported non-quarter-turn footprints remain unchanged rather than guessing
which terrain to flatten. Separate wall/other landscape systems and complete
navigation classification remain under investigation.

Validation checked the map-level loader across all 65 local maps, compared all
28 keep footprints against independently transformed corners, and exercised all
quarter turns, normalized negative/greater-than-360 angles, and map-edge clipping.
Chapter 8 restores ground in 301 keep cells and 72 bridge riverbed cells. Further
checks compare source height/material arrays before and after asset/mask creation
and verify base-height slope materials without altering unmasked terrain.
