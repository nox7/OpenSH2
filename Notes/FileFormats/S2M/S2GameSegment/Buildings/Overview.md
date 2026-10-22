# Map-level building loading

The game loop loads supported structures through one `S2MBuildingAssetLoader.Load`
call and renders them through one `S2MBuildingRenderer.Render` call. Main does not
instantiate individual keep or bridge loaders/renderers.

The map-level loader currently delegates to:

- `S2MKeepAssetLoader`: four keep families, with shared exterior/interior/table/
  collision data and placements grouped by size.
- `S2MBridgeAssetLoader`: supported completed modular bridge spans. Construction
  bridges retain their previous behavior.

Further building families belong behind these map-level entry points. Specialized
loaders can retain their own asset/assembly rules without adding calls to Main.
This is orchestration of currently supported types, not a decoder for all of the
still-raw building objects in S2M.

`S2MBuildingData` returns loaded family data, occupied building rectangles, and a
`PrimaryHeightCells` render mask indexed `z * terrainWidth + x`. This combines keep
footprints with bridge coverage, drawing primary ground/riverbed heights rather
than the elevated walking floors, stairs, and decks. The bridge-only mask is
copied before adding keep footprints so it remains independent.

Only successfully loaded replacement assets contribute footprints. The
building loader finishes before terrain texture loading and geometry generation.
The same mask drives height sampling and derived slope materials. It is applied to the
decoded grid before renderer axis flips, so optional flips and Game Scale affect
buildings and their supporting terrain together. All ground triangles/colliders
remain, including partially exposed cells around entrances. Original map heights,
raised navigation corner planes, and material IDs are retained without mutation.

Keep footprints use the dimensions recorded in the inherited Building block, not
visual mesh bounds (which can include banners, stairs, and overhangs). Their model
origin rotates about a different corner at each quarter turn; the rectangle's
minimum must be adjusted accordingly. Map-edge rectangles are clipped to the
playable terrain, and overlapping rectangles are combined as a union.

Validation checked the map-level loader across all 65 local maps, compared all
28 keep footprints against independently transformed corners, and exercised all
quarter turns, normalized negative/greater-than-360 angles, and map-edge clipping.
Chapter 8 restores ground in 301 keep cells and 72 bridge riverbed cells. Further
checks compare source height/material arrays before and after asset/mask creation
and verify base-height slope materials without altering unmasked terrain.
