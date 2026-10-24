# Building terrain cutouts

## Finding and confidence

The investigated pits do not have a baked downward excavation in the decoded height grids. Instead, the S2M Landscape object contains a separate per-cell byte grid in **tag 18** whose marked cells align with the areas where building meshes replace terrain. This is a high-confidence terrain suppression/cutout candidate, not yet a proved original-engine rendering contract. The original engine code and a visual toggle of this grid have not been tested.

This is different from the derived `PrimaryHeightCells` render mask: that mask chooses underlying primary ground rather than raised building walking floors, but still draws triangles. The implementation now applies authorized saved cutouts as a separate visibility mask, as described below.

## Implemented rendering

`LandscapeReader` decodes tag 18 into `Landscape.TerrainCutoutValues`, transposes
it with the backing-grid stride, and `S2MFile` crops it to the playable dimensions.
The optional block may be absent (null); a present truncated grid is rejected.
Raw byte values and the complete Landscape payload remain preserved. Only the
observed value 1 authorizes suppression; unknown byte values are not treated as
bit fields or interpreted as holes.

After assets load, `S2MBuildingAssetLoader` intersects saved cutouts with the
rotated, clipped footprints of successfully loaded keep and ordinary-building
placements. It exposes the result as `S2MBuildingData.TerrainCutoutCells`.
Unsupported/skipped buildings and unclaimed saved cells cannot create holes.
Completed bridge coverage is excluded so the riverbed remains. The rule is
independent of concrete building names: additional supported building families
participate through their loaded placement batches.

`Main` passes that mask to `S2MTerrainSettings.TerrainCutoutCells` before terrain
generation. `S2MTerrainRenderer` skips masked cells before emitting either normal
or subdivided cliff geometry. Terrain MeshColliders share the generated mesh, so
the same cells are absent from collision. Entirely suppressed chunks allocate
neither a mesh nor a collider. Mask indexing precedes optional axis flips and
scaling; dimensions are validated. Terrain rendered without an authorized mask
retains its ground.

This does not lower heights, trim placement clearance, or overwrite navigation
surfaces. `PrimaryHeightCells` continues to select lower ground for surviving
terrain around building entrances. Each building retains its dedicated collision
mesh or existing exterior-mesh fallback. Building addition/removal should rebuild
the authorized visibility mask and affected terrain geometry rather than mutate
the source height arrays.

Chapter 8 enables 1,356 of the 1,368 saved marked cells, including all 70 cells for
the three target pit families. Twelve marked cells outside decoded loaded
footprints remain terrain. Each medium keep removes its 42 marked interior cells
and retains the other 30 reserved cells.

Validation exercises the actual renderer with managed Unity mesh/component test
stand-ins: ordinary and cliff cell suppression, shared visual/collider topology,
all axis flips, scaling, empty chunks, absent/invalid masks, and source-array
preservation. Checked 130,412 triangle interiors from the generated chapter-8
terrain against the mask. Reader checks cover prefixless/prefixed Landscape data,
transposition, cropped dimensions, absent/truncated blocks and unknown values;
asset checks retain terrain for unsupported buildings. Native Unity rendering
and physics still require in-editor visual verification.

## Height evidence

Audited all 65 local maps, locating 84 target buildings: 14 `GongPit`, 69 `SawPit`, and one `FishPond` (the eel pond). All 77 placements with a decoded HeightLayer (14 + 62 + 1) have primary heights and every plane-0 corner at their placement height throughout the recorded footprint. Landscape heights agree for those placements.

Seven saw pits are in older map variants without a decoded HeightLayer. Their Landscape grid is flat at placement height for five; two Peace Campaign saw pits have small upward variations of 1.06–6.36 and 7.24–14.34 raw units, not a downward pit profile. Treat those maps as incomplete height evidence.

The other two corner planes do not form a pit at the Chapter 8 targets. They are zero there, including on the raised 1706-height gong pits. Zero is also the default outside alternate surfaces and must not be interpreted as an excavation floor.

Chapter 8 contains six gong pits, eight saw pits and one eel pond. All have flat Landscape/primary/plane-0 heights at their placement Y. For example, `FishPond` object 210287648 at (168,230), yaw 0, reserves 6x5 cells: every primary, Landscape and all three corner-plane sample values in that rectangle is 0.

The model geometry is genuinely below its origin:

| Installed mesh | Lowest model vertex relative to placement, default map units |
|---|---:|
| `gong_farm.GR2` | -1.0855 |
| `saw_pit.GR2` | -0.7304 |
| `fishpond.GR2` | -1.5731 |

These are bounds, not recovered terrain excavation depths. They use the existing 204.8 model units per cell and default vertical scale. The meshes supply underground geometry that a flat terrain surface currently covers.

## Landscape tag 18

In the tested chapter-8/controlled-map layout, tag 18 starts at payload offset 2,105,448; its 65,536 bytes start at 2,105,456. Offsets vary with serialization variant, so locate the tag through the block directory. The stored grid is 256x256 in Landscape column-major order:

```text
serialized index = x * storedSide + z
row-major index  = z * playableWidth + x
```

Read the stored side from the tag-4 backing height-grid size, transpose consistently with the other Landscape grids, and then crop to the playable map size. Do not use the smaller logical map side as the serialized stride. Sampled values are 0 and 1. A value of 1 is the cutout candidate.

| Target | Placements across local maps | Marked cells per placement |
|---|---:|---|
| Gong pit | 14 | All 4 cells of its 2x2 footprint |
| Saw pit | 69 | Exactly 2 cells: all cells of its 1x2 footprint, or only the physical 1x2 part of its larger 1x4 reserved area |
| Eel pond | 1 | All 30 cells of its 6x5 footprint |

For the 13 larger saw-pit placements, the distinction is especially useful: the grid marks only two of the four reserved cells. This independently supports retaining placement clearance separately from visible/collision geometry and terrain suppression.

Chapter 8 has 1,368 marked tag-18 cells globally; 70 belong to the three target families. Tag 19 is 0x24 (36) throughout their footprints, but this general structure category is shared by ordinary buildings and cannot identify the cutouts.

## Independent controlled comparison

The existing `terrain-building-before.s2m` / `terrain-building-after.s2m` pair adds an apple farm at (14,229) and a bakery at (19,227), yaw 0. Comparing the Landscape blocks:

- Tag 18 changes exactly one byte: serialized index 5348 = (20,228), from 0 to 1. This is the bakery's local cell (1,1).
- Tag 19 changes 17 bytes for placement/features; this is separate from tag 18.
- Landscape tag 4 height data is byte-for-byte unchanged.
- HeightLayer primary and all three corner planes are also byte-for-byte unchanged.

Thus a persisted, placement-dependent grid changes without a height edit. A shadow, occlusion or another structure flag has not been formally ruled out, but the pit and interior-only masks make terrain suppression the strongest interpretation.

## Keeps and other buildings

The same grid marks partial building shapes rather than every reserved cell:

| Chapter 8 example | Tag-18 marked / reserved cells |
|---|---:|
| Wooden keep 218180760 | 24 / 36 |
| Small keep 861202480 | 36 / 49 |
| Each medium keep | 42 / 72 |
| Each stone barracks | 16 / 30 |
| Each bakery | 1 / 4 |
| Hovels | 0 / 120 total across 20 placements |

For the medium keep at (181,223), its 8x9 reserved rectangle has this tag-18 pattern (`#`=1):

```text
........
.######.
.######.
.######.
.######.
.######.
.######.
.######.
........
```

This leaves exterior/entrance margins intact and closely matches the earlier problem with hiding whole keep rectangles. It is evidence for a finer authored mask, rather than deriving suppression from the full reserved footprint.

## Building payloads and descriptors

The counted concrete bodies are 8 bytes for GongPit, 25 for SawPit, and 44 for FishPond. Every sampled GongPit concrete body is all zero, including pits at different world heights. No per-instance excavation depth or polygon has been identified in these bodies. Saw-pit fields vary and are not fully decoded; their complete payloads remain preserved. This does not prove every relevant rule is absent from building data.

The BDGs reference the appropriate pit models. `fishpond.bdg` also references `fishpond_water.GR2`, `fishpond_parts.GR2` and `fishpond_eelswim.GR2`; those separate water/trap/animation components are not yet loaded by the ordinary building renderer. A terrain opening alone would therefore not complete the eel pond's appearance.

The origin of the mask at original-game placement/load time remains unknown: it may be generated from BDG/type rules, hardcoded placement logic, or another asset. What is established is that the sampled saves already contain the spatial pattern in tag 18. No hardcoded excavation depth is needed to recover that saved pattern.

## Rendering implication

The implemented cutouts should be visually checked at the three target families in Unity against the original game. Primary/corner height arrays and placement footprints remain preserved. The same suppression is applied to terrain colliders so an invisible terrain surface does not cap the pit. Building collision meshes or the existing fallback cover the exposed underground surfaces.

Do not lower all terrain to a model's minimum bound, interpret zero alternate planes as pit floors, or suppress an entire clearance footprint. Applying tag 18 without loaded-building coverage would need a broader audit, including unsupported buildings and masked cells outside decoded footprints. Those unclaimed cells currently remain visible terrain.

## Chapter 8 target records

| Type | Object ID | Origin X,Z | Raw Y | Tag-18 marked / reserved |
|---|---:|---|---:|---:|
| GongPit | 107872536 | 35,36 | 1706 | 4 / 4 |
| GongPit | 107868696 | 52,26 | 1706 | 4 / 4 |
| GongPit | 861215168 | 150,234 | 0 | 4 / 4 |
| GongPit | 861212608 | 200,231 | 0 | 4 / 4 |
| SawPit | 217298048 | 228,7 | 0 | 2 / 2 |
| SawPit | 217300568 | 230,8 | 0 | 2 / 2 |
| GongPit | 861218752 | 206,35 | 0 | 4 / 4 |
| SawPit | 217298328 | 167,201 | 0 | 2 / 2 |
| SawPit | 217299448 | 170,201 | 0 | 2 / 2 |
| SawPit | 217299728 | 172,202 | 0 | 2 / 2 |
| SawPit | 217298608 | 175,202 | 0 | 2 / 2 |
| FishPond | 210287648 | 168,230 | 0 | 30 / 30 |
| GongPit | 861214656 | 173,68 | 0 | 4 / 4 |
| SawPit | 861268480 | 186,65 | 0 | 2 / 2 |
| SawPit | 861269880 | 185,68 | 0 | 2 / 2 |

All 84 per-instance measurements are retained in [TerrainCutoutAudit.csv](TerrainCutoutAudit.csv). Heights in that file are raw S2M units; empty HeightLayer columns indicate a missing decoded layer, not zero.
