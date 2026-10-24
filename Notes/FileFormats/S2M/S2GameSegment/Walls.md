# Wall placement candidate: Map block 14

## Finding

The strongest placement source is **tag 14 inside the S2Game `Map` object's
payload**, not the object named `Walls`. This is a packed 256x256 cell grid with
24 bytes per cell. Drawing it produces castle perimeter lines and raised surfaces
that meet the serialized gatehouse positions. Shape and flag fields appear to
distinguish wall geometry and wooden fortifications.

This identifies a placement source, although some fields remain undecoded.
The first Unity implementation is described in [Wall rendering and assets](./WallRendering.md).

The investigation used the map currently selected by `Assets/Code/Main.cs`:
`C:\Users\garet\Documents\Stronghold 2\Maps\war_chapter8.s2m`.
The installed Steam map with the same name is different. Counts and absolute
inflated offsets must not be copied between those files.

## Map payload layout

All integers and floats are little endian. After locating the `Map` object by
type, read its leading `int32` reference-block byte count and skip that many bytes.
Then walk `(int32 tag, int32 byteCount, byte[byteCount] data)` blocks until the
object payload ends. Unknown tags must be skipped by length. Tag 14 supplies the
wall records; infer the backing side from `sqrt(byteCount / 24)` and validate that
it is an exact square. Do not use fixed file, segment, or Map-payload offsets.

The chapter 8 sample contains tags 2 (65,536 bytes), 13 (262,144 bytes),
14 (1,572,864 bytes), and 16 (131,072 bytes). Tags 13 and 16 are zero there.
These lengths describe that sample, not a requirement to seek past those grids.

## Cell record

| Record offset | Size | Observation / interpretation |
| ---: | ---: | --- |
| 0, 4, 8, 12 | Four float32 | Candidate upper-surface corner heights, in raw map units |
| 16 | uint8 | Candidate wall shape: observed 0 through 5; 1 is the common straight/full form |
| 17 | uint8 | Flags: observed 0, 2, 4; bit `0x02` strongly correlates with wooden fortifications |
| 18 | uint8 | Direction/connection-mask candidate; mostly powers of two and combinations |
| 19 | uint8 | Another direction/connection-mask candidate; exact distinction unknown |
| 20 | uint8 | Sparse auxiliary value; undecoded |
| 21, 22 | Two uint8 | Zero in this chapter 8 sample; preserve them |
| 23 | uint8 | Ownership/estate candidate; coherent values across each castle enclosure |

The grid uses the same row order as `HeightLayer`, rather than Landscape's
transposed serialization:

```text
index = z * 256 + x
recordOffsetInWallLayer = index * 24
```

Overlaying the common Building positions divided by 1024 with this ordering makes
the wall lines meet gatehouses. Transposing X/Z loses that correspondence.
Divide corner heights by 1024 for the current Unity height calibration.

**The wall's four raw height fields do not have HeightLayer plane zero's corner
order.** The wall record stores the -Z pair first:

| Raw float field | Grid vertex | HeightLayer geometric corner |
| ---: | --- | ---: |
| 0 | `(x,z)` | 2 |
| 1 | `(x+1,z)` | 3 |
| 2 | `(x,z+1)` | 0 |
| 3 | `(x+1,z+1)` | 1 |

`WallCell.Height0..Height3` preserve those raw fields. `GetHeight(corner)` accepts
HeightLayer geometric corner numbering and applies the permutation `2,3,0,1`.
Using the identity mapping reverses slopes inside Z-aligned cells, creating
artificial sawtooth steps at their joins; X-aligned slopes can still look correct.

Do not classify every nonzero record as a wall. Some shape-zero records contain
only heights, and others contain flags without heights. These appear to include
raised building/platform surfaces or auxiliary cells. Nor should byte 16 alone be
called the material: the same shape values occur in both flag groups.

Bit `0x04` is **not** established as a stone-material enum. It occurs on a subset
of candidate stone cells; staircase or other surface behavior is a possibility.
Owner encoding and damage state remain unknown; later topology evidence for
shapes and edge masks is described below.

Further topology correlation identifies shape 2 as the triangle joining corners
`2,0,1`, shape 3 as `2,1,3`, shape 4 as `2,0,3`, and shape 5 as `0,1,3` (using
HeightLayer's corner numbering). These are the four diagonal half-cells, not
damage levels. Shapes 2 through 5 in chapter 8 consistently join the appropriate
pair of neighboring full cells. The cardinal mask bits correlate as `1 = +Z`,
`2 = +X`, `4 = -Z`, `8 = -X`; bits 16,32,64,128 identify the diagonal edges of
shapes 2,3,4,5 respectively. The renderer currently uses byte 18 for top details
and byte 19 for body sides; the masks differ at building joins.

## Evidence and counts

| Map | Nonzero tag 14 records | Byte 16 nonzero | Byte 17 = 2 | Byte 17 = 4 |
| --- | ---: | ---: | ---: | ---: |
| Documents `war_chapter8` (current Unity input) | 2,120 | 1,076 | 507 | 239 |
| Steam `war_chapter8` | 2,055 | 1,001 | 485 | 212 |
| Documents `war_chapter1` | 1,396 | 1,131 | 184 | 75 |
| Documents `terrain-flat` control | 0 | 0 | 0 | 0 |

In the current chapter 8 map, 229 of the 1,076 shape-nonzero cells have bit `0x02`
set; the remaining 847 form the candidate stone group. There are also 278
shape-zero cells with bit `0x02`, so the 507 flag count is not a count of wooden
wall pieces.

All five `WoodenGateHouse` objects have tag 14 cells with bit `0x02` within five
cells of their common Building position:

| Gatehouse X | Gatehouse Z |
| ---: | ---: |
| 78 | 180 |
| 105 | 187 |
| 95 | 169 |
| 212 | 153 |
| 212 | 157 |

The stone gatehouses at `(201,45)`, `(212,17)`, `(160,221)`, `(199,217)`,
`(172,227)`, and `(188,70)` instead meet candidate wall cells without bit `0x02`.
The gatehouse at `(81,43)` has no nonzero tag 14 records within five cells, so
the test does not establish that all gatehouses must have adjoining saved walls.

Two illustrative records from the current input:

| Cell X,Z | Four heights | Byte 16 | Byte 17 | Byte 18 | Byte 19 | Byte 23 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 198,15 | 3328,3328,3328,3328 | 1 | 0 | 12 | 12 | 3 |
| 211,154 | 2048,2048,2048,2048 | 1 | 2 | 12 | 14 | 8 |

The `Walls` object is empty in all 112 maps successfully walked during the wider
probe. One additional map, Steam `war_chapter11`, could not be walked by this
marker-based diagnostic parser; its results are not included. This is a limitation
of the probe, not evidence that that map lacks walls. The 112 parsed maps all had
the expected tag 14 grid size; 26 contained nonzero candidate records.

## Reproduction and artifacts

`Notes/tools/probe_s2m_walls.py` uses only the Python standard library. It inflates
the three sequential zlib members, walks the S2Game objects using the existing
documented marker convention, and exports tag 14 candidates. It never modifies
the source maps. Like the current raw object walker, it is not a universal parser
for embedded trailer-like byte sequences.

```powershell
python Notes/tools/probe_s2m_walls.py `
  'C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m' `
  --output Temp/WallProbe/Current
```

The export contains a CSV with raw heights, coordinates, offsets, and all eight
tail bytes; JSON block offsets/counts; and an SVG occupancy preview. Use separate
output directories for different input maps with the same basename.

Investigation outputs are in the ignored `Temp/WallProbe/Current` directory,
including `war_chapter8-preview.png`, an annotated preview with Building gatehouse
positions overlaid. Orange means bit `0x02`; blue means another nonzero shape;
grey means another nonzero record. These colors illustrate the hypothesis and
are not original wall textures.

## Next confirmation

Use controlled editor maps made from the same blank baseline with a straight
stone wall, a straight wooden wall at the same cells, then corners and stairs.
Diff Map tag 14 first, retaining the related terrain/logic layers for comparison.
This should confirm material/shape/connection fields before implementing actual
wall geometry. Keep the full 24-byte record when adding a placement reader;
filtering by a guessed material enum would discard useful surface data.
