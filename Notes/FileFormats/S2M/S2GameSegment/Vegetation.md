# Forest vegetation and Unity rendering

`S2MFile.Forest` is the loaded, renderer-independent representation of the placed
landscape records in the S2Game `Forest` object. The empty `Walls` object immediately
before it omits the optional zero header field, an object-stream edge case that the
reader must preserve rather than consuming its trailer. The initial implementation recognizes
families 0, 1, and 2 as the three controlled tree tools. Other family values are kept in
memory but deliberately not rendered until controlled probes identify them.

Each controlled tree map was made with three separate clicks using a 1x1 brush and
contains three 65-byte records. The current evidence therefore indicates one Forest
placement record per individual tree click.

## Demand-driven asset loading

Loading every GR2 file at startup is unnecessary and would consume memory for models a
map never uses. `S2MVegetationAssetLoader` groups placements by resolved asset path,
loads or converts each distinct GR2 once, and retains a model cache for later map loads.
It returns `S2MVegetationData`; `S2MVegetationRenderer` performs no file I/O.

The current catalog resolves these installed-game paths:

| Forest family | Variant values | GR2 files |
| ---: | --- | --- |
| 0 | 0, 1, 2, 3 | `tree_1`, `tree_1b`, `tree_1c`, `tree_1d` |
| 1 | 0/2, 1/3 | `tree_2`, `tree_2b` |
| 2 | 0, 1, 2 | `tree_3`, `tree_3b`, `tree_3c` |

Family 1 is explicitly provisional: its records use four variant values while the
installation supplies only two corresponding geometry files. The current catalog maps
even values to the base mesh and odd values to `tree_2b` so all controlled placements
remain visible. A focused visual probe can later confirm whether this field also
selects texture/material state.

## Unity components

For an Inspector-driven scene, use this dependency chain:

1. `S2MMapLoader` loads the map and owns `LoadedMap`.
2. `S2MVegetationLoaderComponent` references that map loader and creates
   `LoadedVegetation` from the legally installed Stronghold 2 assets.
3. `S2MVegetationRendererComponent` references the vegetation loader and creates the
   scene objects.

The renderer shares one generated Unity mesh and material set across every placement
of a given GR2 variant. It does not duplicate mesh or texture memory for every tree.
`Granny2ModelLoaderComponent` remains useful as a single-model inspection/debug tool;
it is not the game-wide asset manager.

The placement conversion currently uses 1/1024 map units for position, the record yaw
around Unity Y, and the appearance scale at record offset 24. Flip settings are exposed
to match the terrain renderer if a map orientation is reversed.
