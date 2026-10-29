# HeightLayer

`HeightLayer` is present once in each controlled terrain map and the tested campaign maps. The usual payload is 3,407,932 bytes and contains a 256x256 primary plane followed by three separately counted planes of four corner heights per cell. The header is variable length: `war_chapter5.s2m` has a 3,407,996-byte payload with 64 extra header bytes.

## Binary layout

| Relative offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 4 | Header block tag (`3`) |
| 4 | 4 | Header payload byte length (usually `12`; chapter 5 uses `76`) |
| 8 | variable | Header payload; its internal records remain undecoded |
| 20 | 4 | Data payload marker `A5 C1 FF FF` |
| 24 | 4 | Segment-local type/registration value |
| 28 | 4 | Unknown (`1` in tested maps) |
| 32 | 4 | Primary block byte length including its count (`262148`, or `0x00040004`) |
| 36 | 4 | Primary float count (`65536`) |
| 40 | 262,144 | 65,536 little-endian `float32` primary heights |
| 262,184 | 4 | Corner-height block tag (`2`) |
| 262,188 | 4 | Corner-height block length (`3145740`) |
| 262,192 | 4 | Plane 0 cell count (`65536`) |
| 262,196 | 1,048,576 | Plane 0: four `float32` corner heights per cell |
| 1,310,772 | 4 | Plane 1 cell count (`65536`) |
| 1,310,776 | 1,048,576 | Plane 1: four `float32` values per cell |
| 2,359,352 | 4 | Plane 2 cell count (`65536`) |
| 2,359,356 | 1,048,576 | Plane 2: four `float32` values per cell |
| 3,407,932 | 4 | Object trailer `AF 1E FF FF` (not part of the payload) |

Offsets from the data marker onward in this table assume the usual 12-byte header
payload. The marker is at `8 + headerLength`; in chapter 5 it is at offset 84,
and subsequent fields shift by 64 bytes. Its height count and three corner planes
use the same layout. The reader skips the declared header payload rather than a
fixed number of integers, and rejects negative or out-of-bounds header lengths.
Chapter 5's stored 256x256 grids are then cropped to its playable 128x128 bounds
by the existing map-size normalization.

`Notes/tools/HeightLayerHeaderValidation.cs` checks both header sizes, malformed
lengths, all twelve campaign height/material dimensions, and chapter 5's building
height mask, texture loading and terrain/water/building geometry.

The corner-height block length is exactly:

```text
3 * (sizeof(int32 count) + 65536 cells * 4 corners * sizeof(float32))
= 3 * (4 + 1,048,576)
= 3,145,740 bytes
```

There is no eight-byte footer. The earlier twelve-floats-per-cell interpretation accidentally treated the count fields at the starts of planes 1 and 2 as float samples.

## Primary plane and coordinate order

The primary plane is 256x256 in row-major order:

```text
cellIndex = z * 256 + x
height = Heights[cellIndex]
```

The controlled editor probes changed these primary cells:

| Probe | Changed primary values |
| --- | --- |
| `terrain-one-raise` | `(8,248) = 1706` |
| `terrain-two-raises` | `(8,248) = 3413` |
| `terrain-plateau` | Six cells at `1706` |
| `terrain-ramp` | Six cells at `1706`, `3413`, `5120`, `6826`, `8533`, and `10240` |
| `terrain-smooth` | Thirteen baked values from about `43.44` through `3873.04` |

The standard specific-height levels divide the range from 0 through 10,240 into sixths. The small one-unit differences are consistent with integer rounding.

## Plane 0 corner ordering

For an ordinary cell `(x,z)`, plane 0 stores these four corners:

| Corner slot | Grid vertex |
| ---: | --- |
| 0 | `(x, z + 1)` |
| 1 | `(x + 1, z + 1)` |
| 2 | `(x, z)` |
| 3 | `(x + 1, z)` |

The single-raise probe proves the shared-corner behavior. Raising primary vertex `(8,248)` changes exactly these four plane-0 entries:

```text
cell (8,247), corner 0
cell (7,247), corner 1
cell (8,248), corner 2
cell (7,248), corner 3
```

All four become `1706`. Two raises make all four `3413`. Smooth, ramp, and plateau probes preserve the same relationship.

Plane 0 is not merely redundant in complex campaign maps. Most neighboring corner copies agree with the primary grid, but a small number deliberately differ: 1,340 of 260,100 tested interior corner comparisons in `war_chapter1`, and 2,210 in `war_chapter8`. These per-cell overrides may be part of the original renderer's steep or detailed terrain geometry. A Unity heightfield cannot retain different heights for coincident per-cell vertices; an exact renderer will need a custom cell mesh while this behavior is investigated.

## Planes 1 and 2

Planes 1 and 2 remain completely zero in all controlled terrain probes, including the raised, smoothed, plateau, and ramp maps. Therefore they are not smoothing data and are not alternate versions of the terrain brush result.

They are sparse and non-zero in campaign maps. `war_chapter1` has 8,517 non-zero values in plane 1 and 4,085 in plane 2; `war_chapter8` has 8,251 and 1,899 respectively.

The controlled water probes identify one use for plane 1. Painting water on height-zero ground depresses plane 0 to `-640` (with a `-256` sea shoreline), while the matching plane-1 corners remain at `0` above it. The water renderer therefore uses plane 1 for river, sea, and pitch/swamp surfaces. The moat probe does not change heights, so moats instead use ground plane 0. Plane 1 is not exclusively water: maps without decoded water can also contain non-zero values, so it is more generally an alternate or elevated surface used by multiple landscape systems. Plane 2 remains unknown. Neither plane should be blended into the base ground height.

`HeightLayer.CornerHeightPlanes[plane]` preserves the three planes in file order. Use `GetCornerHeight(plane, cellIndex, corner)` for checked access.

## Building ground versus walking surfaces

There is no decoded universal renderable/walkable flag. In building footprints,
the primary `Heights` grid retains underlying ground, while corner plane 0 can
contain raised building floors, stairs, and platforms. Outside those footprints,
plane 0 also carries real terrain details and waterbed corners, so replacing it
globally with the primary grid would be incorrect.

Chapter 8 has three `StoneBarracks` objects with 6x5 rotated footprints. All their
primary ground samples are 0; their plane-0 corners reach 3427.1875 or 4298.4 raw
units. Those elevations belong to the absent building surfaces, not visible earth.
Rendering them as terrain leaves elevated patches without ground underneath.

The shared inherited Building block supplies a model transform and rotated
footprint independently of whether a concrete building's assets/gameplay fields
have been decoded. `S2MFile.BuildingPlacements` retains this common metadata for
246 objects in chapter 8, including the barracks. Concrete unsupported objects
remain `RawS2Object` with complete payloads.

`S2MBuildingAssetLoader` derives `PrimaryHeightCells` from these placed-building
footprints plus supported bridge spans. Terrain geometry and slope materials use
the primary grid there, even when the building has no visual model yet. This
selection is generated render data, not a Boolean field recovered from S2M.
Original height arrays and raw bytes are unchanged. Future navigation must use
the preserved surface data and still-undecoded connectivity/occupancy rules;
the generated Unity terrain chunks are not a complete walkability model.

### Underground building geometry

Gong pits, saw pits and the eel pond do not contain a lowered excavation in the
decoded HeightLayer samples at their positions. All 77 target placements with a
decoded HeightLayer have primary and plane-0 heights at placement level across
their footprints. A separate Landscape tag-18 byte grid marks the corresponding
pit cells, including only two cells of larger four-cell saw-pit reservations.
This is a high-confidence terrain cutout candidate rather than a height plane.
The renderer now omits saved cutout cells only inside loaded building footprints;
source heights and walking surfaces remain unchanged.
The controlled bakery placement changes one tag-18 cell while leaving Landscape
heights and all four HeightLayer planes unchanged. See
[Building terrain cutouts](Buildings/TerrainCutouts.md) for measurements, limits
and the distinction between saved suppression data and `PrimaryHeightCells`.

## Logical map size versus serialized grid

Maps whose header says `mapsize = 128` still serialize the same 65,536 primary values and three 65,536-cell corner planes. Radar and estate grids are also 256x256 in those samples. `mapsize` therefore describes the usable map extent or map class rather than the serialized backing-grid dimensions.

## Other terrain features

Other features are represented elsewhere in S2Game. Confirmed separate object types include `EstateLayer`, `ChannelLayer`, `Grass`, `Shrubs`, `Rocks`, `NaturalStone`, `Waves`, `Floaters`, `FlyMgr`, and `S2FlyHandler`. Water, foliage, rocks, flying objects, and estate painting should be decoded from those objects rather than assigned to the `HeightLayer` planes.
