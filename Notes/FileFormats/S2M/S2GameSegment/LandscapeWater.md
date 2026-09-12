# Landscape features and water data

Controlled probes compared the following 256x256 maps against `terrain-flat.s2m`:

- `terrain-river.s2m`: one click with a 3x3 river brush
- `terrain-river-opposite.s2m`: the same river with the opposite flow direction
- `terrain-sea.s2m`: one click with a 3x3 sea brush
- `terrain-sea-opposite.s2m`: the same sea with the opposite flow direction
- `terrain-pitch-swamp.s2m`: one click with a 3x3 pitch/swamp brush
- `terrain-moat.s2m`: a moat probe
- `terrain-pitch-trap.s2m`: a pitch-trap probe
- `terrain-tree-type-1.s2m`, `terrain-tree-type-2.s2m`, and `terrain-tree-type-3.s2m`:
  three individual clicks with a 1x1 tree brush in each map

The flat, river, sea, moat, swamp, and trap probes retain the same 151-object count;
these features do not add placed top-level objects. Their state is stored in fixed-size
blocks inside the existing `Landscape` object. Each tree probe also retains 151
top-level objects but enlarges the existing `Forest` object by 195 bytes.

## Landscape block directory

In the current serialization variant, `Landscape` begins with a 12-byte prefix followed by a special height block. Some older maps, including `war_chapter1`, omit that prefix. The remaining payload consists of repeated `int32 tag`, `int32 byteLength`, and data records.

| Payload offset | Tag | Data bytes | Current interpretation |
| ---: | ---: | ---: | --- |
| 12 | 4 | 262,144 | 256x256 `float32` Landscape height grid |
| 262,172 | 2 | 524,288 | Unknown |
| 786,468 | 27 | 233,472 | Composite surface data; first 65,536 bytes are surface material IDs |
| 1,019,948 | 30 | 167,936 | Unknown |
| 1,187,892 | 5 | 262,144 | Unknown four-byte cell records |
| 1,450,044 | 26 | 65,536 | Unknown byte grid |
| 1,515,588 | 7 | 65,536 | Water flow-direction byte grid |
| 1,581,132 | 22 | 524,288 | Unknown |
| 2,105,428 | 16 | 12 | Unknown |
| 2,105,448 | 18 | 65,536 | Unknown byte grid |
| 2,170,992 | 19 | 65,536 | General landscape-feature byte grid |
| 2,236,536 | 20 | 28,676 | Unknown |
| 2,265,220 | 25 | 4 | Unknown |
| 2,265,232 | 28 | 4 | Unknown |
| 2,265,244 | 31 | 4 | Unknown |
| 2,265,256 | 33 | 4 | Unknown |

Offsets above describe the 12-byte-prefix variant and point to each block's tag. Data begins eight bytes later. The initial tag-4 record additionally stores the logical map size and an unknown value before its byte length, so its height data begins at payload offset 28. In the prefixless variant all these offsets are 12 bytes earlier.

The logical-size value can be `128` even though the height byte length is still 262,144 and therefore contains a 256x256 backing grid. The reader derives serialized dimensions from the block length rather than treating this field as the array dimension.

Landscape grids are serialized in X-major order:

```text
serializedIndex = x * 256 + z
```

`LandscapeReader` transposes decoded grids into the row-major `z * width + x` convention used elsewhere by OpenSH2.

Both prefix variants and 128/256 logical sizes were validated across all 49 maps currently in the supplied Maps directory. Every `Landscape` payload decoded successfully.

## Landscape feature type: tag 19

Tag 19 is a categorical landscape-feature grid, not a water-presence bit field. The
controlled probes establish these exact values:

| Feature | Value | Footprint in probe |
| --- | ---: | ---: |
| None | `0x00` | unchanged cells |
| River | `0x01` | 3x3 |
| Sea | `0x03` | 3x3 |
| Pitch/swamp | `0x05` | 3x3 |
| Moat | `0x07` | 2x2 |
| Tree types 1 and 2 | `0x21` (33) | 2x6 |
| Pitch trap | `0x28` (40) | 2x2 |
| Building probe | `0x26` (38) | 13 cells |

Tree type 3 does not change tag 19 or any other decoded Landscape block. All three
tree probes instead add 195 bytes to the `Forest` object. Tree types 1 and 2
additionally mark their occupied Landscape cells with value 33. The reason tree type
3 omits that occupancy marking is not yet known.

The tree probes disprove the earlier low-bit hypothesis: value 33 has bit 0 set but
is not river water. Masking it to `01` caused tree footprints to be emitted by the
water renderer. Campaign values such as 33, 35, and 37 must therefore remain unknown
until controlled probes establish whether they are separate categories or composite
states. `WaterLayer.LandscapeFeatureValues` preserves every raw byte, and
`GetWaterType` recognizes only exact controlled values 1, 3, 5, and 7.

## Tree records in `Forest`

The `Walls` object immediately before `Forest` is empty in these probes. Each controlled
tree map contains three separately clicked placements. Together they add three 65-byte
records (195 bytes total) to the array at the start of the separate `Forest` payload.
This establishes one `Forest` record per placed tree, not a three-record cluster from
one brush operation. In each tree map:

- the byte-length field at payload offset 0 counts the bytes following that field;
- the element count at offset 4 is `3`;
- the first record begins at offset 8;
- each record is 65 bytes;
- `uint16` offset 48 within every record is `0`, `1`, or `2` for controlled tree types
  1, 2, or 3 respectively.

The controlled records and campaign data establish this currently decoded layout:

| Record offset | Type | Interpretation |
| ---: | --- | --- |
| 0, 4, 8 | `float32` | world X/Y/Z; divide by 1024 to obtain map units |
| 12 | `float32` | yaw in degrees |
| 16, 20 | `float32` | two scale fields (normally 1) |
| 24 | `float32` | randomized uniform appearance scale |
| 28, 32, 36 | `float32` | randomized tint/appearance values |
| 40 | `int32` | randomized value/seed |
| 44, 46 | `uint16` | coarse cell X/Z |
| 48 | `uint16` | landscape family; 0, 1, 2 select controlled tree types 1, 2, 3 |
| 50 | `uint16` | graphical variant |
| 52 | 13 bytes | unknown/reserved; zero in current probes |

After the records, `Forest` retains 32,772 trailing bytes. The first four are another
currently unknown field and the remaining 32,768 form a fixed block whose meaning is
not yet known. `war_chapter8.s2m` contains 1,012 records and validates the same framing.
The runtime reader preserves all unknown bytes losslessly.

## Flow direction: tag 7

The default-flow maps store `0` in all nine painted cells. Changing to the opposite flow stores `16` in all nine cells, without changing their water-state or material values.

The moat and pitch/swamp probes do not change tag 7. Their flow value remains zero;
at present they should be treated as non-flowing surfaces rather than as direction-zero
river water.

In `war_chapter8.s2m`, water cells use direction values throughout the range `0..31`, with no value above 31. The direction is therefore a five-bit angular index with 32 steps:

```text
angle = direction * (360 / 32)
angle = direction * 11.25 degrees
oppositeDirection = (direction + 16) mod 32
```

The editor screenshots do not establish a compass orientation. OpenSH2 currently treats code 0 as positive Unity X and exposes `FlowAngleOffsetDegrees` for later calibration. The water mesh writes the decoded flow vector to UV channel 1 (`TEXCOORD1`) for an animated water shader.

## Surface material: tag 27

The first 65,536 bytes of tag 27 form a material/surface grid. The controlled cells contain:

| Surface | Value |
| --- | ---: |
| Flat grass | `0x0C` |
| River | `0x1A` |
| Sea | `0x1F` |

Moat, pitch/swamp, pitch trap, and the tree probes leave this material ID at grass
value `0x0C`. Their distinct visual ground textures must therefore use another part of
tag 27 or one of the remaining Landscape blocks. Campaign water uses more material IDs,
especially along shores and transitions, so these values should not be used as the sole
surface-presence test. Exact controlled values in tag 19 are currently authoritative.

The texture-paint probe identifies tag 27 as the primary ground-material selector:
`terrain-flat` uses `0x0C` and the nine cells painted with the presumed mountain brush
use `0x08`. The paint also changes one byte in tag 27 after its initial 65,536-byte
grid, so the remaining bytes likely contribute blending or transition state. See
[Terrain texture selection and tinting](./TerrainTextures.md) for the evidence and
the independent tag-22 tint grid.

The river and sea probe values (`0x1A` and `0x1F`) overlap the now-confirmed
ground-texture IDs for `heath_edge` and `forest_floor`. They must therefore not be
treated as an authoritative water-material lookup; water type remains defined by exact
tag-19 feature values, and water rendering should continue to use its separately
configured river/sea materials.

## Ground and water-surface heights

Painting water also changes the base terrain:

- River depresses the four central ground vertices to `-640`.
- Sea depresses the central four vertices to `-640` and a surrounding ring to `-256`.
- Opposite flow has exactly the same ground geometry as its original-direction counterpart.
- Pitch/swamp depresses the same four central ground vertices as river, to `-640`.
- Moat, pitch trap, and all three tree probes do not change decoded height data.

The matching corners in `HeightLayer` plane 1 remain at `0`, above the depressed plane-0 ground. This identifies plane 1 as the water surface for these probes and explains why it appeared unchanged when compared only against the flat map: both the original ground and newly created water surface are at height zero.

`S2MWaterRenderer` uses corner plane 1 for river, sea, and pitch/swamp surfaces by
default. Moats use plane 0 because their controlled probe changes no height data and
shows that they hug the ground instead of spanning a depressed bed. Both choices are
configurable because elevated-water and multi-level structure probes are still needed
to prove plane semantics in every map.

## Runtime representation

`S2MFile.Landscape` retains the decoded `Landscape`, and `S2MFile.WaterLayer` provides its water grids directly. File loading remains independent of rendering.

The water renderer creates four vertices per surface cell so each cell can retain its
own flow direction. It recognizes only the four proven water-like categories and
separates river, sea, pitch/swamp, and moat into four mesh submeshes. Unknown tag-19
values are deliberately skipped, preventing trees, traps, and buildings from being
rendered as water. Each category has an independently configurable material. Vertex
colors encode raw data for diagnostic or shader use:

| Vertex color channel | Value |
| --- | --- |
| R | Raw tag-19 landscape-feature value |
| G | Tag-27 surface material ID |
| B | Tag-7 flow direction (`0..31`) |
| A | `255` |

UV0 contains world-aligned texture coordinates. UV1 contains the two-dimensional decoded flow vector.
