# HeightLayer

`HeightLayer` is present once in both tested campaign maps. Its total payload is 3,407,932 bytes in both files. It stores one primary float height per cell followed by twelve additional float samples per cell.

## Payload prefix and primary height plane

| Relative offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 20 | Five unknown `int32` values |
| 20 | 4 | Data payload marker `A5 C1 FF FF` |
| 24 | 4 | Segment-local type/registration value |
| 28 | 4 | Unknown (`1` in both samples) |
| 32 | 4 | Primary block byte length including its count (`262148`, or `0x00040004`) |
| 36 | 4 | Float value count (`65536`) |
| 40 | 262,144 | 65,536 little-endian IEEE-754 `float32` height values |
| 262,184 | 4 | Additional-block tag (`2`) |
| 262,188 | 4 | Additional-block byte length (`3145740`, including count and footer) |
| 262,192 | 4 | Cell count (`65536`) |
| 262,196 | 3,145,728 | 65,536 records containing twelve `float32` values each |
| 3,407,924 | 8 | Zero footer |
| 3,407,932 | 4 | Object trailer `AF 1E FF FF` (not part of payload length) |

The primary plane is 256×256 in file order. For `war_chapter1.s2m`, observed values range from -640 to 10,240. For `war_chapter8.s2m`, they range from -640 to about 8,387.627. These magnitudes and spatial continuity support the height interpretation.

The twelve additional values are **interleaved by cell**, not stored as twelve consecutive planes. In other words, each serialized cell has a twelve-float record. Code can present these as twelve logical channels by striding through the record array, but the file itself is cell-major.

All twelve channels have extremely similar ranges, zero rates, and unique-value counts. Their values also use the same height-like range as the primary data. This makes it unlikely that they are unrelated semantic masks such as water, vegetation, or estate ownership. They are more plausibly local terrain samples used to build the rendered terrain geometry, although the exact spatial meaning of each of the twelve slots remains unknown.

## Logical map size versus serialized grid

Maps whose header says `mapsize = 128` still serialize 65,536 primary values and 65,536 twelve-sample records, just like maps whose header says 256. Radar and estate grids are also 256×256 in those samples. Therefore, `mapsize` appears to describe the usable map extent or map class, not the serialized backing-grid dimensions. No 128×128 `HeightLayer` was observed in the supplied maps.

## Other terrain features

Other features are represented elsewhere in S2Game rather than as these twelve channels. Confirmed separate object types include `EstateLayer`, `ChannelLayer`, `Grass`, `Shrubs`, `Rocks`, `NaturalStone`, `Waves`, `Floaters`, `FlyMgr`, and `S2FlyHandler`. Water, foliage, rocks, and flying-object semantics should be investigated in those objects before assigning them to fields within `HeightLayer`.
