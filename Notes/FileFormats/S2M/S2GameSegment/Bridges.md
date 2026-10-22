# Bridges

`Bridges` is a separate S2Game object, not a Landscape surface-material or
landscape-feature category. The two completed river crossings in `war_chapter8.s2m`
retain ordinary feature values 0 (land) and 1 (river). Their raised navigation
surfaces are baked into HeightLayer corner plane 0 while the primary heights still
describe the bank/riverbed. Consequently height or feature-ID heuristics cannot
reliably distinguish these crossings from ordinary terrain.

## Observed record layout

The payload begins with two little-endian int32 fields:

| Offset | Meaning |
| ---: | --- |
| 0 | Byte count following this field (payload length minus 4) |
| 4 | Number of 26-byte span records |

Each record has the following observed layout:

| Record offset | Type | Interpretation |
| ---: | --- | --- |
| 0 | byte | State: 1 accompanies a completed deck; 0 is unfinished/inactive in inspected maps |
| 1 | byte | Longitudinal axis: 0 is X, 1 is Z |
| 2 | int32 | Start X cell |
| 6 | int32 | Start Z cell |
| 10 | int32 | End X cell (inclusive longitudinal coordinate) |
| 14 | int32 | End Z cell (inclusive longitudinal coordinate) |
| 18 | int32 | Unknown; zero in inspected maps |
| 22 | int32 | Unknown; zero in inspected maps |

The transverse footprint is two cells starting at the recorded X or Z. For example,
an along-Z span from `(142,172)` to `(142,195)` covers X 142..143 and Z 172..195.
All completed records observed are axis-aligned, ordered in the positive direction,
and have even longitudinal lengths. The implementation preserves unknown fields
and raw bytes, and only renders state 1 with the supported layout and zero tails.
The exact lifecycle meaning of state 0 requires controlled construction probes.

Chapter 8 contains these three records:

| State | Axis | Start | End | Rendering |
| ---: | --- | --- | --- | --- |
| 1 | Z | (142,172) | (142,195) | Completed modular bridge, 24 x 2 cells |
| 1 | Z | (245,48) | (245,59) | Completed modular bridge, 12 x 2 cells |
| 0 | X | (83,42) | (92,42) | Unfinished/inactive; retain terrain |

The first two footprints match the raised plane-0 cells exactly. The flat deck
navigation height is 1024 raw units above the banks, with approach corner values
of approximately 614.4 and 0. The third record coincides with a separate
`ConstructingBridge` building object and does not have that completed raised deck.
`war_chapter2.s2m` has one state-0 along-Z record and receives no replacement.

## Model assets and BDG files

The installed `meshes/buildings/bridge.bdg` is a 283-byte binary descriptor containing
the filename `bridge_end.GR2` at offset 0x20. It is not a mesh. Its role as a building
asset descriptor is inferred from this reference and the analogous
`bridge_wooden.bdg`, which contains `bridge_wooden.GR2`, construction-stage model
names, and a destroyed-model reference. The complete BDG schema is not decoded;
the runtime does not interpret its other fields or treat every printable string
as a valid filename.

Completed spans currently use `bridge_end.GR2` and `bridge_middle.GR2`, both with
`bridgemap.dds`. They are modular rather than one mesh stretched across a river:

- Each module occupies a nominal 409.6 x 409.6 model-unit square (two map cells).
- The first end is exported at nominal X/Y 0..409.6.
- The middle is exported at nominal X 0..409.6, Y 409.6..819.2. Its baked longitudinal
  offset must be compensated when repeating modules.
- The last end is rotated 180 degrees; along-X assemblies are rotated 90 degrees.

The initial model scale is therefore `HorizontalCellSize / 204.8`. Railings and
supports extend outside the nominal square. Vertical placement uses the starting
bank's plane-0 corner heights, not the elevated navigation deck (which would raise
the model twice). The mesh's visible walking surface differs slightly from the
navigation height; exact visual calibration against the original game remains
unverified in Unity.

## Runtime behavior

`S2MBridgeAssetLoader` loads both replacement meshes before producing a two-cell-wide
terrain mask. `Main` passes this mask to `S2MTerrainRenderer` and renders the modular
assemblies with `S2MBridgeRenderer`, under the same scaled map root. This mask selects
`S2MTerrainSettings.PrimaryHeightCells`: masked cells render the primary ground grid
instead of the elevated plane-0 walking corners. Terrain triangles and colliders
remain beneath the bridge, preserving the riverbed and bank surfaces. Bridge mesh
colliders supply the deck, and building textures use opaque materials rather than
the foliage alpha-test rules. Water remains on its independently decoded surface
plane. The earlier implementation skipped masked cells entirely, leaving riverbed
holes; the mask now replaces only the raised terrain surface with base terrain.

Unfinished, unknown, out-of-bounds, odd-length, or otherwise unsupported spans retain
their terrain. The mask is runtime render data; decoded HeightLayer navigation
heights and the original map bytes remain unchanged. Loading/rendering through the
standalone terrain component without supplying a replacement mask keeps all terrain.

Validation covered all 65 local maps: every Bridges object decoded, and 46 completed
spans with decoded HeightLayers met the supported layout. Chapter 8 produces 72
masked cells, four end instances, and fourteen middle instances. Additional checks
covered both axes, exact mask coverage, module counts/anchors, invalid bounds,
unknown states, and malformed record counts. An in-editor visual check remains.
