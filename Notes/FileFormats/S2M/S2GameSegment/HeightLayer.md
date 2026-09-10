# HeightLayer

`HeightLayer` is present once in each controlled terrain map and the tested campaign maps. Its payload is 3,407,932 bytes and contains a 256x256 primary plane followed by three separately counted planes of four corner heights per cell.

## Binary layout

| Relative offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 20 | Five unknown `int32` values |
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

They are sparse and non-zero in campaign maps. `war_chapter1` has 8,517 non-zero values in plane 1 and 4,085 in plane 2; `war_chapter8` has 8,251 and 1,899 respectively. Their purpose remains unknown. Possible relationships to structures, walkable levels, water, or another simulation surface require controlled probes; they should not be blended into the ground height.

`HeightLayer.CornerHeightPlanes[plane]` preserves the three planes in file order. Use `GetCornerHeight(plane, cellIndex, corner)` for checked access.

## Logical map size versus serialized grid

Maps whose header says `mapsize = 128` still serialize the same 65,536 primary values and three 65,536-cell corner planes. Radar and estate grids are also 256x256 in those samples. `mapsize` therefore describes the usable map extent or map class rather than the serialized backing-grid dimensions.

## Other terrain features

Other features are represented elsewhere in S2Game. Confirmed separate object types include `EstateLayer`, `ChannelLayer`, `Grass`, `Shrubs`, `Rocks`, `NaturalStone`, `Waves`, `Floaters`, `FlyMgr`, and `S2FlyHandler`. Water, foliage, rocks, flying objects, and estate painting should be decoded from those objects rather than assigned to the `HeightLayer` planes.
