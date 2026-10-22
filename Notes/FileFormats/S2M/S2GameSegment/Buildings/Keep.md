# Castle keeps

## Types and assets

The S2Game object types are numbered from the wooden keep, not the small stone keep.
The serialized Building footprint and the BDG's leading dimension pair agree:

| S2M type | Asset stem | Unrotated footprint | Count in war_chapter8 |
| --- | --- | --- | ---: |
| Keep1 | Keep_wooden | 4 x 9 | 1 |
| Keep2 | Keep_small | 7 x 7 | 1 |
| Keep3 | keep_medium | 8 x 9 | 3 |
| Keep4 | Keep_large | 12 x 18 | 0 |

Footprints swap width/height for quarter-turn rotations. All four families exist in
the installed building assets; the large family is exercised by other local maps.

Each `meshes/buildings/keep_SIZE.bdg` references closed, open, collision, and table
GR2 files. These are distinct assets:

- `*_closed.GR2`: exterior view.
- `*_open.GR2`: an alternate mesh with interior geometry and materials, including
  floors, interior walls, tapestries, and other details.
- `*_table.GR2`: separate furniture positioned in the same exported coordinate system.
- `*_collision.GR2`: collision geometry independent of the visible view.

Stone keep families also have `*_FL`, `*_FR`, `*_BL`, and `*_BR` sections. Roof,
flag, candle/torch marker, construction, ruined, and low-detail assets also exist
for various families. Their presence does not imply that every piece should be
drawn on top of both complete view meshes. The initial renderer uses the closed
or open model, the interior table, and the independent collision mesh. The exact
wall-section/flag/light assembly rules in BDG remain undecoded.

## Placement block

Keeps contain an actor position followed by an inherited `Building` payload.
The actor position is not the model origin, especially for rotated buildings.
`KeepReader` uses the Building transform, preserving the complete raw payload.

The Building section begins with `A5 C1 FF FF`, followed by a segment-local type
index. On the first use of that type, an inline ASCII registration appears:
`int32 8`, eight bytes `Building`. Following the optional registration are
`int32 1`, `int32 52`, and a 52-byte data body. Later references omit the name.
This explains why fixed offsets work on some keeps but fail on the first keep
in maps such as Axon and Mountain Siege.

Known body fields are:

| Body offset | Type | Meaning |
| ---: | --- | --- |
| 24 | float32 | Model origin X, raw units |
| 28 | float32 | Model origin height, raw units |
| 32 | float32 | Model origin Z, raw units |
| 36 | float32 | Rotation in degrees |
| 40 | int32 | Rotated footprint width |
| 44 | int32 | Rotated footprint height |

Other fields remain preserved without interpretation. Raw positions use 1024
units per map cell; height uses the same scale as terrain. Exported geometry uses
204.8 model units per cell, matching the bridge models. Z-up conversion maps model
XYZ to Unity XZY. The recorded yaw is applied directly after that conversion.

Chapter 8's model placements are:

| Object | Size | Map X | Raw height | Map Z | Yaw |
| --- | --- | ---: | ---: | ---: | ---: |
| Keep1 / 218180760 | Wooden | 58 | 0 | 189 | 0 |
| Keep3 / 861189192 | Medium | 31 | 1706 | 28 | 270 |
| Keep2 / 861202480 | Small | 213 | 0 | 37 | 90 |
| Keep3 / 861229824 | Medium | 181 | 0 | 223 | 0 |
| Keep3 / 861264624 | Medium | 173 | 0 | 90 | 0 |

For example, the first medium keep's actor X is 22, but its model X is 31.
Rendering at the actor position would visibly displace the rotated model.

## Initial runtime rendering

`S2MBuildingAssetLoader` is the map-level entry point. It delegates keep asset
loading to `S2MKeepAssetLoader`, which groups placements by size and loads shared
closed/open/table/collision data. `S2MBuildingRenderer` delegates rendering to
`S2MKeepRenderer`, which shares meshes and materials across instances. Main calls
only the map-level building loader and renderer. Keeps are parented under the
scaled map root and follow terrain's horizontal scale, height scale, and optional
reflected axes.

After replacement assets load, the building loader computes each keep's occupied
rectangle from the serialized rotated footprint and model origin. For yaw 90,
the minimum Z is origin Z minus footprint height; for yaw 270, the minimum X is
origin X minus footprint width. Yaw 180 subtracts both dimensions. Width and
height are already rotated and must not be swapped a second time.

The initial implementation hid entire footprint cells, exposing holes around
stairs and partially covered exterior cells. Footprints now select
`S2MTerrainSettings.PrimaryHeightCells` instead: the terrain renderer draws the
lower primary ground surface beneath the model, not the raised floor/stair
navigation corners. Chapter 8's primary heights throughout each footprint match
that keep's placement height exactly (0 or 1706 raw units). Its 301 keep ground
cells and 72 bridge riverbed cells form a combined 373-cell primary-height mask.

Terrain triangles and ground colliders remain; dedicated keep collision meshes
provide the building's surfaces above them. Slope-material overrides also sample
the primary grid in these cells, so stair navigation heights do not incorrectly
turn the exposed ground into rock. Texture loading uses the same render mask.
Neither primary heights, corner-height planes, nor source material IDs are
modified. Future unit movement can still sample the original floor/stair heights
through `HeightLayer.GetCornerHeight`; render geometry is not a replacement for
that navigation data.

`Main` exposes **Keep Render Settings**:

- **View Mode**: Automatic, Closed, or Open.
- **View Camera**: optional override; otherwise the tagged main camera is used.
- **Interior Open Distance**: camera distance from the exterior bounds in map
  units, default 12. This remains relative to Game Scale.
- **Interior Close Hysteresis**: extra closing distance, default 2, preventing
  rapid switching at the threshold.

Every keep switches independently. Only one visible view is active, tables appear
with interiors, and collision stays active regardless of camera distance. The
distance rule is an initial OpenSH2 behavior, not a recovered original-game value.
There is no animated transition, wall-section cutaway selection, flag animation,
or attached interior lighting yet.

Building materials use opaque shading, with explicit `.alphatest.` texture names
enabling cutouts for details such as drawbridge chains. Exported texture references
are rebased onto the local installation's `meshes` tree before trying sibling
textures: open interiors reference `meshes/units/models/knight.dds`, whose filename
also exists in the buildings directory. The directory must be honored to avoid
silently selecting the wrong texture.

## Validation

All 65 local maps were parsed: 28 keep objects decoded (19 wooden, 2 small,
5 medium, 2 large), including inline Building registrations. Chapter 8's five
transforms and all twelve required assets were checked, as were the four large
keep assets using Barclay Kingdom. All referenced textures for chapter 8's keep
models exist in the installation.

Additional checks covered forced/automatic view decisions, hysteresis, missing
cameras, model transform/unit conversion, sibling and cross-directory texture
resolution, and rejection of non-finite transforms. The Unity assembly builds;
in-editor placement and cutaway appearance still require visual verification.
