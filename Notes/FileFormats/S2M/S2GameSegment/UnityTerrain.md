# Rendering HeightLayer with a chunked Unity mesh

`S2MFileLoader` parses the file into an `S2MFile`. `S2MTerrainRenderer` then creates
runtime mesh chunks from that already loaded map's `HeightLayer`. File loading and
scene rendering are deliberately separate so other renderers can reuse the same map
data.

## Runtime components

1. Add `S2MMapLoader` to a GameObject, set its S2M file path, and enable **Load On Start**. A relative path is resolved beneath `Application.streamingAssetsPath`; an absolute path is also accepted.
2. Add `S2MTerrainRendererComponent`, assign the map loader, and enable **Render When Map Loaded**. The generated terrain root and its mesh chunks become children of the renderer component's GameObject.

`S2MMapLoader.LoadedMap` retains the parsed `S2MFile`. Terrain, water, estate, foliage, and placed-object renderers can all consume this same reference without reopening the file.

Water uses this architecture through the separate `S2MWaterRendererComponent`. Assign the same `S2MMapLoader` used by the terrain component. Its generated cell mesh reads `S2MFile.WaterLayer` without performing file I/O. River, sea, and pitch/swamp use `HeightLayer` corner plane 1; ground-hugging moats use plane 0. The four surfaces are separate submeshes with independently assignable materials. Unknown landscape-feature values are skipped rather than guessed to be water.

The component exposes these initial calibration settings:

- `HorizontalCellSize`: horizontal Unity units per serialized S2M cell; default 1.
- `HeightUnitScale`: vertical Unity units per raw S2M height unit; default 1/1024.
- `FlipX` and `FlipZ`: reverse either serialized axis after entity coordinates establish the final orientation.
- `UsePerCellCornerHeights`: preserve the four HeightLayer plane-0 corner values owned by each cell; enabled by default.
- `CellsPerChunk`: terrain-cell width and height of each generated mesh chunk; default 32.
- `GenerateMeshColliders`: create a MeshCollider for each chunk; enabled by default.
- `Material`: optional material override shared by generated mesh chunks.

Code can keep loading and rendering in separate stages:

```csharp
S2MFile map = S2MFileLoader.Load(s2mFilePath);

GameObject terrain = S2MTerrainRenderer.Render(
  map,
  parentTransform,
  new S2MTerrainSettings());
```

## Exact cell geometry

Unity TerrainData cannot represent two different heights at the same grid coordinate.
That loses authored S2M plane-0 corner overrides, so it is not the final terrain
backend. The renderer instead emits one quad per serialized cell, with four independent
vertices that retain the documented plane-0 corner order. A 256x256 map at the default
32-cell chunk size produces 64 mesh chunks.

Raw corner heights are converted directly to local Unity Y using `HeightUnitScale`.
Every generated cell duplicates its vertices intentionally, allowing neighboring cells
to disagree where the original data does. Normals initially derive from the primary
height grid so ordinary terrain receives smooth lighting without welding those
independent corner positions. A later shader/normal pass can refine lighting around
intentional discontinuities.

## Current limits

The renderer uses plane 0 for base terrain when it is available. The primary height
plane is retained as a fallback and for smooth-normal generation. Planes 1 and 2 are
zero in every controlled terrain-brush probe and sparse in campaign maps; they are not
smoothing layers.

The controlled specific-height probes reveal six standard levels between 0 and 10,240. This makes 1/1024 the best current vertical calibration: the first level is about 1.67 Unity units and the maximum is 10. The earlier 1/256 default made every feature four times too tall and was the main cause of the exaggerated Unity peaks.

The primary plane can still contain intentional one-cell discontinuities. In `war_chapter1.s2m`, the maximum primary sample is 10,240 and an immediate neighbor is -256. At 1/1024 this is approximately 10.25 vertical units across one horizontal cell. The chunked renderer retains the additional plane-0 corner overrides directly, including their distinct coincident-corner values.

Clamping the maximum is not recommended. The six controlled levels show that 10,240 is a legitimate authored height, and clamping would discard map data.

The grass-to-rock change seen after raising a tile is not represented by a changed height-layer plane. In the flat-versus-one-raise probe, the only semantic change inside `Landscape` was another copy of the new `1706` height. This suggests that the original material chooses rocky detail procedurally from slope/normal data, although the complete `Landscape` schema is still unknown.

The terrain mesh writes the per-cell tag-27 material ID, tag-19 feature ID, and the
candidate tag-30 quarter-turn value to UV1, tag-22 tint to vertex color, and the four
cardinal neighbour tint/rotation values to additional UV channels.
`S2MTerrainTextureRenderer` consumes those attributes with the separately loaded,
legally installed DDS assets. Its shader uses the same smooth cardinal-edge
weights for both material textures and terrain tint, so tint transitions are no longer
hard square boundaries. The blend width is controlled by
`S2MTerrainTextureRenderSettings.AdjacentMaterialBlendWidth` (default `0.2` cell), on
`S2MTerrainTextureRendererComponent` or when calling `S2MTerrainTextureRenderer.Apply`.
The remaining tag-27 blend payload is still under investigation, so these weights remain
an adjacency-based approximation. Water-like surfaces are handled by the separate water
renderer. Estate coloring, vegetation, rocks, and placed objects are serialized in
separate S2Game objects and should be layered onto the generated terrain as their schemas
are decoded.
