# RadarMap segment

The RadarMap segment has an inflated size of 327,820 bytes in both `war_chapter1.s2m` and `war_chapter8.s2m`. It contains exactly two objects followed by `AD DE FF FF`.

| Object | Start | Payload start | End after trailer | Payload bytes |
| --- | ---: | ---: | ---: | ---: |
| `RadarMap` | 8 | 36 | 262,200 | 262,160 |
| `EstateLayer` | 262,200 | 262,236 | 327,816 | 65,576 |

Offsets are relative to the inflated segment and are identical in both samples.

## RadarMap payload

| Relative payload offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 4 | Pixel byte count (`262144`) |
| 4 | 262,144 | 256×256 pixels at four bytes per pixel; exact channel order is not confirmed |
| 262,148 | 4 | Unknown (`1`) |
| 262,152 | 4 | Unknown (`4`) |
| 262,156 | 4 | Object Id of the following `EstateLayer` |
| 262,160 | 4 | Object trailer `AF 1E FF FF` |

## EstateLayer payload

| Relative payload offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 20 | Five unknown `int32` values |
| 20 | 4 | Data payload marker `A5 C1 FF FF` |
| 24 | 4 | Segment-local type index (`2`) |
| 28 | 4 | Unknown (`0`) |
| 32 | 4 | Block byte length (`65540`: count plus cells) |
| 36 | 4 | Cell count (`65536`) |
| 40 | 65,536 | One estate identifier byte per 256×256 cell |
| 65,576 | 4 | Object trailer `AF 1E FF FF` |

Chapter 1 begins largely with estate value `1`; chapter 8 shows multiple values including `2`, `9`, and `11`. This supports an estate/ownership-region interpretation rather than image color data.
