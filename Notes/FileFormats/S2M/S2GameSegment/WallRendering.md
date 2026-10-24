# Wall rendering and installed assets

## Asset investigation

The Steam installation contains the textures used in the supplied intact and
damaged wall screenshots. Its executable also references this precise family of
landscape textures and wall shaders, providing stronger evidence than filenames
alone:

| Installed path under `meshes/buildings` | Role |
| --- | --- |
| `wall.dds` | Stone wall sides; 256x512 masonry with weathering toward the base |
| `walltop.DDS`, `walltop2.dds` | Stone walking-surface variations |
| `crenelations.dds` | Stone caps / battlement masonry |
| `castle_wall_destroyed.dds` | Exposed broken stone, matching the grey rubble surfaces in screenshot 2 |
| `wood_walls.DDS` | Upright palisade timbers |
| `walltop_wood.DDS` | Timber walkway planks |
| `wood_walls_top.dds` | Timber end grain |
| `wood_walls_beams.DDS` | Timber beam material |

`castle_wall.dds`, `castle_wall_interior.DDS`, and `castle_wall_crack.DDS` are
additional masonry/weathering assets. They should not be substituted merely on
the basis of their names: the executable's landscape wall texture references
explicitly include `wall.dds`, `walltop.dds`, `walltop2.dds`, `crenelations.dds`,
and `castle_wall_destroyed.dds`.

The original `shaders/Landscape/Walls.fx`, `WallSides.fx`, and `FancyWalls.fx`
are available as text. WallSides wraps U and **mirrors V**. They are conventional
lit texture shaders; they do not contain a geometry-generation or GPU damage
deformation implementation. FancyWalls enables tangent normal maps. This does
not prove that no vertex deformation occurs elsewhere in the game.

Three destruction GR2 files were inspected through the existing local Granny
converter using the installed game's runtime:

| File | Meshes | Granny models |
| --- | ---: | ---: |
| `wall_top_destroy.GR2` | 108 | 1 |
| `wall_top_destroy_2.GR2` | 300 | 1 |
| `wood_wall_destroy.GR2` | 188 | 1 |

The first contains many individually named `Box...` mesh fragments with a
`wall.BMP` material reference. Combined with their names and the executable's
`CrumblingWallEmitter` / `CrumblingWoodWallEmitter` names, these are likely
destruction-fragment assemblies, not an intact modular wall placement catalog.
No intact standalone wall-body or battlement GR2 was found by the asset-name and
executable-reference search. The converted files are diagnostic artifacts in
ignored `Temp/WallProbe`; original assets are never copied into tracked files.

The screenshots are consistent with generated wall volumes whose upper corner
heights change, selective removal of battlements, and a separate exposed-rubble
material. Merely removing the top decorations from a fixed rectangular prism
would not reproduce the sloping and jagged broken surfaces in screenshot 2.
The original damage algorithm and health/state encoding are still unconfirmed.

## First runtime implementation

`WallLayerReader` reads the `Map` object's length-prefixed reference block and
walks its tagged arrays. It decodes tag 14 without any absolute map offsets. It
infers the backing stride from the layer byte count, validates complete 24-byte
records and finite corner heights, and retains all eight trailing bytes.
`S2MFile.WallLayer` exposes the result; the raw Map payload remains available.

`S2MWallRenderer` generates chunked meshes with shared materials:

- Shape 1 is a full cell; shapes 2 through 5 use the four diagonal half-cell
  polygons. Zero and unknown shapes are not filled with guessed wall cubes.
- Four saved corner heights define the upper surface. The primary ground-height
  grid defines the base; the wall's elevated secondary plane is not used as ground.
  The stone walking course sits 0.08 height units above the saved body top, with
  a separate textured riser connecting it to the wall sides.
- Saved side masks choose body faces; saved top masks choose parapets and merlons.
  This preserves the omissions at gatehouse/building joins instead of closing
  every neighboring gap. At shared edges between actual wall polygons, any
  exposed difference between their walking heights also receives a side face.
- Flag bit 2 selects timber materials; other supported shapes use masonry.
- Stone walkway, wall side, and battlement cap textures are separate materials.
  Timber sides, plank walkway, and end grain are also separate.
- Stone parapets are 0.30 cells thick. Alternating merlons occupy 70% of their
  repeat interval. Their faces remain vertical and flush with the wall footprint;
  only the coping slopes, with its inner edge 0.035 height units above its outer
  edge. Each stone course segment and merlon is level along the edge's length;
  neighboring segments step to follow the grade. Dimensions are exposed in wall settings.
- Wooden parapets have six-sided pointed log tops, using the exposed end-grain
  texture on the tapered faces, rather than the earlier square crenellations.
  Their sizes and repeat pitch remain visual calibration, not recovered original
  engine constants.
- Materials use URP Lit (Standard fallback), opaque matte lighting, and the wall
  side texture's mirrored V sampler. Normal maps are not imported in this pass.
- MeshColliders share the generated geometry. Playable bounds, horizontal and
  vertical scales, and X/Z flips follow the terrain settings. Full backing-grid
  stride is retained when the playable area is smaller.
- Generated meshes, materials, and textures are destroyed with the render root.
  Shape-zero elevated building/platform surfaces are left to their owning systems.

`Main.RunTests` calls the renderer alongside buildings under `S2M Map World`.
Its serialized `wallRenderSettings` can hide battlements or adjust their dimensions.
Loading a map through a standalone `S2MMapLoader` decodes the same layer; callers
can invoke the renderer with that loaded map.

This renders the saved wall layout. It does not implement siege damage, animated
debris, health progression, the rubble-material selection rule, stairs, or exact
royal/strong-wall styling. The shape enum must not be treated as a damage enum.
The four heights and distinct edge masks are preserved for a later damage system.

## Validation

`Notes/tools/WallRenderValidation.cs` is a standalone Unity editor batch harness
for an isolated test project. It checks block relocation and backing stride,
truncated data rejection, actual chapter 8 decoding, mesh winding, installed
textures/shaders, exterior and top collision, diagonal occupancy, a gate approach gap,
axis flips and non-default scaling, and an empty-map control. It also generates
URP preview images of stone walls, wooden walls, and the map layout.

Temporary project and output paths are `Temp/WallValidationProject` and
`Temp/WallProbe/Rendered`; they are ignored by Git. The user's open Unity project
and scene are not used as the batch test workspace.

The initial Unity 6000.4.7f1 run passed: 1,076 wall cells, 23 nonempty mesh chunks,
31,295 triangles including generated battlements. The runtime sources also
compiled against the project's Unity references with no errors. Stone, wood,
and overview PNGs were inspected. The harness warms up URP before its first
offscreen capture to avoid first-submission texture-binding artifacts.

## Reference-driven architectural refinement

Inspection of `gatehouse_wooden.GR2` found one mesh with 72 triangles: 48 bark
triangles using `wood_walls.bmp`, and 24 tip triangles using `wood_walls_top.bmp`.
Those 24 triangles form **four six-sided pointed cones**. Each tip rises about
114.17 native model units above its log collar, with diameter about 56.84 units
(approximately 0.5575 high and 0.2775 wide at 204.8 model units per map cell).
The wall renderer generates equivalent hexagonal points at a calibrated pitch,
with the same installed end-grain DDS texture and local UVs for each point.
It does not instance an entire gatehouse or embed extracted gate vertices.

`siege_spikes.GR2` and `tower_wooden.GR2` were also inspected. The former is a
separate siege-obstacle assembly; the latter uses bark and plank materials rather
than the gate's separate pointed end-grain group. The gate therefore supplies a
direct geometric/material reference for the palisade detail in the screenshot.

The refined Unity harness additionally checks the raised walking surface,
vertical merlon faces, wider and outward-sloping coping, and the difference in
height between the center and edge of a pointed wooden spike. Detail previews
are generated in `Temp/WallProbe/Refined`.
The refined run passed with the same 1,076 cells / 23 chunks and 29,225 triangles.
Both detail previews were visually checked against the supplied references.

## Vertical parapets and height-step closure

The architectural correction removes the merlon's horizontal shear entirely,
including the old `StoneMerlonOutset` setting. A previously serialized outset
therefore cannot make the blocks lean. The higher inner cap edge is retained;
the outer and inner side faces have constant X/Z coordinates over their height.

Saved side masks alone do not describe all visible geometry: they omit shared
wall edges even where adjacent cells have different upper corner heights. For a
masked-out cardinal edge, the renderer now checks that the neighboring playable
cell contains a wall polygon with the opposite matching edge. It then emits only
the vertical strip where this cell is higher than its neighbor. Heights include
the stone walking course, so stone/timber transitions are closed as well.

If the two linear height profiles cross along an edge, the strip is clipped at
their intersection. Each cell supplies only its higher portion, avoiding reversed
or overlapping faces. Equal-height edges remain culled. Absent wall cells and
unmatched triangular edges retain the saved openings; this rule does not fill
gatehouses indiscriminately. Neighbors are fetched from the original layer grid,
including across render-chunk boundaries.

The harness has collision regressions for a height step across X=31/32 (two
chunks), slopes crossing in both directions, a stone-course/timber transition,
equal-height edges, and a masked opening against an absent wall. A synthetic
two-height detail preview is saved with the map renders in `Temp/WallProbe/SeamFix`.
The corrected Unity run passed: 1,076 cells, 23 chunks, 29,447 triangles, including
the new seam cases. Stone parapets, a controlled two-height wall, and the chapter
8 stepped wall near `(71,18)` were rendered and visually inspected.

## Wall corner order and level battlement sections

The earlier renderer incorrectly treated Map tag 14's heights as HeightLayer
plane-zero corner order. The wall's raw fields instead run `(x,z)`, `(x+1,z)`,
`(x,z+1)`, `(x+1,z+1)`. `WallCell.GetHeight` now maps geometric corners through
`2,3,0,1`, retaining the original fields unchanged. This corrects the reversed
Z-direction gradients without transposing the placement grid or changing the
shape/edge masks.

An enumeration of all 24 corner permutations favored this mapping. Comparing
shared endpoints of adjacent full wall cells gives this further evidence
(a mismatch means a difference greater than one raw height unit):

| Map | Endpoint comparisons | Earlier mismatches | Corrected mismatches |
| --- | ---: | ---: | ---: |
| Documents chapter 8 | 2,270 | 176 | 47 |
| Documents chapter 1 | 2,750 | 585 | 77 |
| Steam chapter 8 | 2,072 | 176 | 47 |

Remaining differences are retained as saved discontinuities and closed by the
height-difference faces. Heights are not indiscriminately averaged across walls.

Stone parapet courses are now generated one repeat interval at a time. Their
bottom edges meet the graded walk, while their upper edges are level along the
wall, positioned above the interval's higher endpoint. Each merlon starts on that
level course, so its left/right ends have equal heights. The small coping slope
is applied only across the parapet thickness. Wooden palisade behavior is unchanged.

The Unity harness contains separate X- and Z-axis ramp checks, continuity across
Z=31/32 (a chunk boundary), and rays sampling both ends of one merlon on the
Z ramp. It also checks that the next merlon has a different elevation, preserving
the stepped silhouette. Previews are written to `Temp/WallProbe/CornerOrder`.
The updated run passed with 1,076 cells, 23 chunks, and 35,380 triangles.
The synthetic Z-ramp and chapter 8 slope previews were visually inspected; the
previously reversed, stepped walk now joins as a continuous graded surface.
