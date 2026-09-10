# Rendering HeightLayer with Unity Terrain

`S2MFileLoader` parses the file into an `S2MFile`. `S2MTerrainRenderer` then creates a runtime `UnityEngine.Terrain` from that already loaded map's confirmed primary `HeightLayer` plane. File loading and scene rendering are deliberately separate so other renderers can reuse the same map data.

## Runtime components

1. Add `S2MMapLoader` to a GameObject, set its S2M file path, and enable **Load On Start**. A relative path is resolved beneath `Application.streamingAssetsPath`; an absolute path is also accepted.
2. Add `S2MTerrainRendererComponent`, assign the map loader, and enable **Render When Map Loaded**. The generated Terrain becomes a child of the renderer component's GameObject.

`S2MMapLoader.LoadedMap` retains the parsed `S2MFile`. Terrain, water, estate, foliage, and placed-object renderers can all consume this same reference without reopening the file.

The component exposes these initial calibration settings:

- `HorizontalCellSize`: horizontal Unity units per serialized S2M cell; default 1.
- `HeightUnitScale`: vertical Unity units per raw S2M height unit; default 1/256.
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

Only the primary height plane is rendered. The twelve additional height-like samples per cell remain available in `HeightLayer.AdditionalHeightSamples`, but their spatial meaning has not been proven. They may encode sub-cell terrain geometry or smoothing information; blending them into the primary plane now would be speculative.

This renderer does not yet apply terrain textures, estate coloring, water, vegetation, rocks, or placed objects. Those features are serialized in separate S2Game objects and should be layered onto the generated terrain as their schemas are decoded.
