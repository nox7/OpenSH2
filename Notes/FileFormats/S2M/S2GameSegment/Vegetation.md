# Forest vegetation and Unity rendering

`S2MFile.Forest` is the loaded, renderer-independent representation of the placed
landscape records in the S2Game `Forest` object. The empty `Walls` object immediately
before it omits the optional zero header field, an object-stream edge case that the
reader must preserve rather than consuming its trailer. The initial implementation recognizes
families 0, 1, and 2 as the three controlled tree tools. Other family values are kept in
memory but deliberately not rendered until controlled probes identify them.

Each controlled tree map was made with individual 1x1-brush clicks and contains one
65-byte record per click. `terrain-forest-1`, `terrain-forest-2`, and
`terrain-forest-3` each contain ten records, respectively using Family `0`, `1`, and
`2`.

## Family and graphical variant probe

All three controlled maps use the same placement coordinates. The editor randomizes
the value at record offset `50` (`Variant`) independently for each placement:

| Map/editor option | Family | Observed Variant values | Count by Variant `0/1/2/3` |
| --- | ---: | --- | --- |
| `terrain-forest-1` | 0 | 0, 1, 2, 3 | 3 / 4 / 2 / 1 |
| `terrain-forest-2` | 1 | 0, 1, 3 | 3 / 4 / 0 / 3 |
| `terrain-forest-3` | 2 | 0, 1, 2, 3 | 3 / 1 / 3 / 3 |

The three editor screenshots show distinct silhouettes within a Family. Consequently,
`Variant` is a real appearance selector, not merely an unused ID. The Family 1 sample
does not happen to roll Variant 2, but this is insufficient evidence that it is
invalid.

The exact `Family + Variant -> GR2 path` table is still undecoded. It cannot be a
selection among models embedded in `forest_tree_N.gr2`: native Granny inspection shows
each `forest_tree_1` through `forest_tree_4` file contains exactly one Granny model
and one mesh. A material-list search also identifies `forest_canopy_1` through `4`,
but these are 8-vertex, 4-triangle crossed-billboard meshes using `foest_bb*.tga`;
they are distant-LOD assets, not the close tree variants. By contrast, each
`forest_tree_N` is a 163--199 vertex tree containing bark and broadleaf materials.

The remaining likely implementation is a separate game-side lookup table selecting
one installed GR2 path for each Family/Variant pair. Candidate source groups include
the numbered `forest_tree_N` set and the `tree_1`/`tree_1b`/... sets. Do not assign
them by name alone; confirm the table visually or locate the original engine table.

## Demand-driven asset loading

Loading every GR2 file at startup is unnecessary and would consume memory for models a
map never uses. `S2MVegetationAssetLoader` groups placements by resolved asset path,
loads or converts each distinct GR2 once, and retains a model cache for later map loads.
It returns `S2MVegetationData`; `S2MVegetationRenderer` performs no file I/O.

The current catalog resolves these installed-game paths:

| Forest family | Variant values | GR2 files |
| ---: | --- | --- |
| 0 | 0, 1, 2, 3 | `forest_tree_1`, `forest_tree_2`, `forest_tree_3`, `forest_tree_4` |
| 1 | 0, 1, 2, 3 | `winter_tree_1`, `winter_tree_2`, `winter_tree_3`, `winter_tree_4` |
| 2 | 0, 1, 2, 3 | `tree_1`, `tree_1b`, `tree_1c`, `tree_1d` |

All twelve associations were visually confirmed against the three controlled editor
maps. They are stored as an internal `ForestTreeAssets[family][variant]` table in
`S2MVegetationAssetCatalog`, not as Inspector configuration. The earlier
`tree_1`/`tree_2`/`tree_3` mapping was a mistaken assumption based on their names.
The installation contains one full-detail and one `.low` model for each
known mapping above. The S2M graphical-variant field selects the corresponding path
from the internal table.

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

`AssetScaleMultiplier` is a uniform final scale for every model instance. Set it in
`S2MVegetationRendererComponent`'s nested settings (or `Main`'s test setting) to
calibrate GR2 model size without changing decoded placement coordinates.
