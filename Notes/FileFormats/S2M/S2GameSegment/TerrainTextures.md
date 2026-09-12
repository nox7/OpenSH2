# Terrain texture selection and tinting

Terrain texture files are supplied by the user's legally installed Stronghold 2 game
under `terrain/`. `landtex.txt` in the installation lists the authoring texture names
in its `GROUND1` group; the listed `.BMP` names resolve case-insensitively to files
such as `terrain/mountain.dds` and `terrain/grass_lush.dds`.

The S2M does not store those paths. It stores compact material and appearance data in
the `Landscape` object's fixed-size blocks. OpenSH2 must map those values to the
locally installed assets at runtime rather than redistribute the DDS files.

## Controlled paint probe

The following 256x256 maps were compared against `terrain-flat.s2m`:

- `terrain-mountain-texture.s2m`: one 3x3 click using the editor brush believed to
  be `mountain.dds`.
- `terrain-mountain-texture-tinted.s2m`: the same painted 3x3 area with a very dark
  tint applied.

The brush occupies serialized cells `(x=8..10, z=245..247)`. `Landscape` grid data
is X-major in the file (`index = x * 256 + z`), and `LandscapeReader` transposes its
currently decoded grids to row-major order for runtime use.

### Surface material ID: tag 27

The first 65,536 bytes of tag 27 are one byte per cell. The paint probe changes exactly
the nine brush cells from `0x0C` (the all-`grass_lush.dds` flat map) to `0x08`.
Therefore tag 27 is the primary per-cell terrain-material selector.

`terrain-all-textures.s2m` confirms the lookup for the first 24 selectable `GROUND1`
entries: their zero-based index in `landtex.txt` plus `8` is the tag-27 material ID.
The map paints each entry as a 3x3 group, separated by a 1x3 `grass_lush` group. The
groups run from X=8 through X=102 at Z=245..247. `grass_lush` is visually invisible in
the diff because both its own 3x3 group and every separator retain the flat-map value
`0x0C`.

| Material ID | `terrain/` asset |
| ---: | --- |
| `0x08` | `mountain.dds` |
| `0x09` | `mountain_edge.dds` |
| `0x0A` | `grass_rough.dds` |
| `0x0B` | `grass_dark.dds` |
| `0x0C` | `grass_lush.dds` |
| `0x0D` | `grass_yellow_3.dds` |
| `0x0E` | `grass_yellow_2.dds` |
| `0x0F` | `grass_yellow.dds` |
| `0x10` | `mud.dds` |
| `0x11` | `cracked_peat.dds` |
| `0x12` | `building_blend.dds` |
| `0x13` | `mud_edge.dds` |
| `0x14` | `sand.dds` |
| `0x15` | `sand_dunes.dds` |
| `0x16` | `sea_bed.dds` |
| `0x17` | `sand_edge.dds` |
| `0x18` | `heath.dds` |
| `0x19` | `heath_2.dds` |
| `0x1A` | `heath_edge.dds` |
| `0x1B` | `grass_flowers.dds` |
| `0x1C` | `stoney_ground.dds` |
| `0x1D` | `rubble.dds` |
| `0x1E` | `rock_1.dds` |
| `0x1F` | `forest_floor.dds` |

The IDs below `0x08`, and the remaining entries after this tested section of
`landtex.txt`, have not been assigned semantics by this probe.

The remaining 167,936 bytes of tag 27 are not decoded. The paint probe changes one
additional byte there (local offset 5,821, `2 -> 3`), so this tail likely participates
in blending, transition selection, or another paint-derived state. It must not be
discarded when implementing textured terrain.

### Tint: tag 22

Tag 22 is 524,288 bytes. Its first 262,144 bytes behave as a 256x256 RGBA-style
appearance grid with four bytes per serialized cell. Applying the dark tint changes
the first three bytes of each of the same nine cells from `(255,255,255)` to
`(60,60,60)`; the fourth byte remains `255`. This identifies an independent per-cell
RGB terrain-tint multiplier. The remaining 262,144 bytes in tag 22 are still unknown.

The equal RGB test does not establish channel ordering beyond showing that all three
colour channels are written. A coloured tint probe is required before naming the
channels or using alpha.

### Texture orientation probe: tag 30

`terrain-flat-rotate-3x3.s2m` is not byte-identical to `terrain-flat.s2m` after
inflation. The primary tag-27 material grid remains unchanged (both maps are
`grass_lush`), but one byte changes at the same serialized cell index in two
otherwise-unknown grids:

| Block | Relative offset | Change |
| --- | ---: | --- |
| tag 27 tail (after its first 65,536-byte material grid) | `6,077` (tag-27-local `71,613`) | `0 -> 3` |
| tag 30 | `6,077` | `0 -> 1` |

Tag 30 is 167,936 bytes long. Its first 65,536 bytes form a complete 256x256
grid, and a survey of `terrain-flat.s2m` shows only values `0..3` in that grid.
The controlled rotation action changing one of those values from 0 to 1 is strong
evidence that it is a four-state per-cell texture orientation or variation index.
The corresponding tag-27-tail byte change means that block may carry related
transition/brush state; it is still retained as raw data and is not used as the
authoritative rotation source. The remaining tag-30 bytes have the same composite
size as the unknown tag-27 tail (`2 * 256x256 + 192x192`) and remain undecoded.

The changed index is `x=23,z=189` in the serialized X-major convention (and
`x=189,z=23` after the reader's transpose). The exact editor brush anchor was not
recorded, so this coordinate is a serialization check rather than a compass
calibration. A second probe that rotates the same cell through all four states, or
rotates several isolated cells, would confirm the mapping and whether the tag-27
tail contains an independent blend orientation.

### Slope-forced surfaces

The slope probes distinguish a derived rock surface from an authored cliff selector:

| Probe | Height-layer change | Tag-27 material change |
| --- | --- | --- |
| `terrain-min-slope.s2m` | Four surrounding cells use a raised corner value of about `905.3849` | None; the cells remain grass (`0x0C`) |
| `terrain-min-cliff-face.s2m` | The same four-cell footprint uses about `1852.088` | Four surrounding cells become `0x27` |

The first probe therefore requires a renderer-side slope rule for `rock_1.dds`
(`0x1E`). The second confirms `0x27` as an explicit cliff-face material selector;
its asset is `terrain/cliff_face.dds` (with a separate normal-map asset in the
installation). The runtime effective material grid preserves authored `0x27` values
and derives only `0x1E` from a ground-edge height difference of at least 900 raw
units. It must not derive `0x27` from height alone: doing so also cliffizes the four
diagonal corner cells, while this controlled file explicitly selects only the four
side cells.

The same transition changes 16 bytes in Landscape tag 5 (a 262,144-byte block),
from zero to directional-looking values `2`, `3`, `4`, and `5`. Its packing does not
map directly to the 256x256 material grid and its role is not yet proven; it may be
cliff topology metadata or a derived collision/pathing structure. The renderer does
not consume it until that layout can be established without guessing.

Cliff geometry is a cell-level topology decision. A selected side cell with two
adjacent high corners and two adjacent low corners retains all four serialized
corners, but replaces the ordinary two-triangle slope with three strips split at the
cell bisector: two upper surface triangles, two vertical cliff-face triangles, and
two lower surface triangles. The bisector is duplicated at the high and low edge
heights, producing the vertical wall without moving the original terrain vertices.
The horizontal strips use `rock_1.dds`; only the wall uses `cliff_face.dds`.

Cells with one selected high corner use the corresponding triangular version of the
same subdivision. In `terrain-min-cliff-face.s2m` those diagonal corner cells are not
selected, so they remain ordinary rock slopes. This matches the visible corner ramps
in the controlled screenshot and allows a higher editor elevation to cliffize them
only if the file starts selecting those cells. The cliff normal map is catalogued but
not yet applied by the terrain shader.

## Runtime representation

`S2MTerrainTextureAssetLoader` is separate from terrain rendering. It examines an
effective material grid, demand-loads only the confirmed DDS files referenced by that
grid from the user's installed game, and returns `S2MTerrainTextureData`.
`S2MTerrainTextureRenderer` applies that data to an already generated terrain mesh;
it performs no map or DDS file I/O.

The effective grid is a copy of tag 27's primary material-ID grid. It then forces a
2x2 area beginning at each decoded Forest record's coarse `CellX*2, CellZ*2` anchor
to material `0x1F` (`forest_floor.dds`). This produces the original forest-floor
ground under every rendered forest tree without mutating the loaded map data. The
2x2 footprint agrees with the controlled tag-19 occupancy marks for tree Families 0
and 1; Family 2 has no corresponding feature mark, so its floor override is inferred
from its Forest placement record.

The renderer packs the loaded textures into a runtime `Texture2DArray`, avoiding the
D3D11 sampler limit that would prevent one shader from binding all 24 DDS files
individually. Each terrain-cell vertex writes its tag-27 material ID and candidate
tag-30 rotation value to UV1, the first tag-22 RGBA grid to vertex color, and the four
cardinal tint/rotation values to additional UV channels. The `OpenSH2/Stronghold 2
Terrain` shader selects the texture-array slice, applies the per-cell quarter-turn,
and multiplies it by the blended tint. Unknown material IDs fall back to the grass-lush
slice until they are decoded. `S2MTerrainTextureRenderSettings.ApplySerializedTextureRotation`
controls this candidate rotation path and defaults to enabled.

For an Inspector-driven scene, add the components in this dependency chain:

1. `S2MMapLoader`
2. `S2MTerrainRendererComponent`
3. `S2MTerrainTextureLoaderComponent`, referencing the map loader and legal game installation
4. `S2MTerrainTextureRendererComponent`, referencing the terrain renderer and texture loader

The terrain shader blends a cell's texture and RGB tint toward each cardinal neighbour;
`AdjacentMaterialBlendWidth` controls the width of both transitions and defaults to
0.2 cell. This removes the artificial hard square borders while retaining each
serialized material selection and tint. It is an adjacency-based approximation: the
remaining tag-27 tail must still be decoded before reproducing the original engine's
exact blend weights and transition shapes. `TextureScale` is exposed on the texture
renderer settings because the original texture-repeat calibration is not yet established.

## Other coupled data

The paint operation also changes one byte in tag 30 and the four-byte tag-25/tag-28
values. These may be derived bookkeeping or related texture state; the texture renderer
should initially retain them as raw data and avoid assigning semantics without another
controlled probe.

## Repeatable comparison tool

`Notes/tools/CompareLandscapeBlocks` compares `Landscape` blocks from three inflated
S2Game streams and reports changed bytes with material-grid coordinates. For example:

```powershell
dotnet run --project Notes/tools/CompareLandscapeBlocks/CompareLandscapeBlocks.csproj -- `
  C:\Users\garet\Documents\Stronghold 2\Maps\terrain-flat-inflated\03-zlib-1505.bin `
  C:\Users\garet\Documents\Stronghold 2\Maps\terrain-mountain-texture-inflated\03-zlib-1519.bin `
  C:\Users\garet\Documents\Stronghold 2\Maps\terrain-mountain-texture-tinted-inflated\03-zlib-1526.bin
```

Run `dump_s2m_inflated_segments.ps1` first to produce those inputs. The generated
filename includes the compressed-stream offset, so it can differ for another map.
