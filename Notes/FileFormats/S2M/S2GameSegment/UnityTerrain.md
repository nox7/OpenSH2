# Rendering HeightLayer with Unity Terrain

`S2MFileLoader` parses the file into an `S2MFile`. `S2MTerrainRenderer` then creates a runtime `UnityEngine.Terrain` from that already loaded map's confirmed primary `HeightLayer` plane. File loading and scene rendering are deliberately separate so other renderers can reuse the same map data.

## Runtime components

1. Add `S2MMapLoader` to a GameObject, set its S2M file path, and enable **Load On Start**. A relative path is resolved beneath `Application.streamingAssetsPath`; an absolute path is also accepted.
2. Add `S2MTerrainRendererComponent`, assign the map loader, and enable **Render When Map Loaded**. The generated Terrain becomes a child of the renderer component's GameObject.

`S2MMapLoader.LoadedMap` retains the parsed `S2MFile`. Terrain, water, estate, foliage, and placed-object renderers can all consume this same reference without reopening the file.

Water uses this architecture through the separate `S2MWaterRendererComponent`. Assign the same `S2MMapLoader` used by the terrain component. Its generated cell mesh reads `S2MFile.WaterLayer` without performing file I/O. River, sea, and pitch/swamp use `HeightLayer` corner plane 1; ground-hugging moats use plane 0. The four surfaces are separate submeshes with independently assignable materials. Unknown landscape-feature values are skipped rather than guessed to be water.

The component exposes these initial calibration settings:

- `HorizontalCellSize`: horizontal Unity units per serialized S2M cell; default 1.
- `HeightUnitScale`: vertical Unity units per raw S2M height unit; default 1/1024.
- `FlipX` and `FlipZ`: reverse either serialized axis after entity coordinates establish the final orientation.
- `Material`: optional Terrain material override.

Code can keep loading and rendering in separate stages:

```csharp
S2MFile map = S2MFileLoader.Load(s2mFilePath);

Terrain terrain = S2MTerrainRenderer.Render(
  map,
  parentTransform,
  new S2MTerrainSettings());
```

## Resolution conversion

Unity TerrainData requires a `2^n + 1` heightmap resolution. The decoded S2M plane is 256×256, so the renderer creates a 257×257 TerrainData heightmap. It copies all source samples directly and duplicates the final row and column to form Unity's required outer edge. It does not interpolate interior samples.

Raw heights are normalized to the 0–1 values required by `TerrainData.SetHeights`. The TerrainData vertical size is set to `(maximum - minimum) × HeightUnitScale`, and the Terrain object's local Y position is set to `minimum × HeightUnitScale`. Together these preserve each primary height as `rawHeight × HeightUnitScale` in local space.

## Current limits

Only the primary height plane is rendered. `HeightLayer` also exposes three separately counted planes with four corner heights per cell. Plane 0 contains base-terrain cell corners and has occasional per-cell overrides in campaign maps. Planes 1 and 2 are zero in every controlled terrain-brush probe and sparse in campaign maps; they are not smoothing layers.

The controlled specific-height probes reveal six standard levels between 0 and 10,240. This makes 1/1024 the best current vertical calibration: the first level is about 1.67 Unity units and the maximum is 10. The earlier 1/256 default made every feature four times too tall and was the main cause of the exaggerated Unity peaks.

The primary plane can still contain intentional one-cell discontinuities. In `war_chapter1.s2m`, the maximum primary sample is 10,240 and an immediate neighbor is -256. At 1/1024 this is approximately 10.25 vertical units across one horizontal cell. Plane 0's per-cell corner overrides may refine some of this steep terrain, but Unity Terrain cannot retain different heights for coincident cell corners. A later custom, chunked mesh renderer should consume plane 0 directly if those overrides prove visually significant.

Clamping the maximum is not recommended. The six controlled levels show that 10,240 is a legitimate authored height, and clamping would discard map data.

The grass-to-rock change seen after raising a tile is not represented by a changed height-layer plane. In the flat-versus-one-raise probe, the only semantic change inside `Landscape` was another copy of the new `1706` height. This suggests that the original material chooses rocky detail procedurally from slope/normal data, although the complete `Landscape` schema is still unknown.

The terrain renderer does not yet apply terrain textures, estate coloring, vegetation, rocks, or placed objects. Water-like surfaces are handled by the separate water renderer. The other features are serialized in separate S2Game objects and should be layered onto the generated terrain as their schemas are decoded.
