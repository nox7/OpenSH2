# Missing foliage and rocks: war_chapter8

Audit date: 2026-10-24. This report records the baseline before implementation.
The missing landscape rendering has since been implemented; see
[Foliage rendering](FoliageRendering.md) for current coverage and validation.
The original audit/export coverage columns retain the families-0–2 baseline.

## Map and current coverage

`Assets/Code/Main.cs:112` loads the Documents copy:
`C:\Users\garet\Documents\Stronghold 2\Maps\war_chapter8.s2m`.
Its SHA-256 is `1f5d01e111d3070cc9d30d41466e0a05a12d5795dfeda3ab77d3af1c2bb3db45`.

The installed campaign map is a different file. Its SHA-256 is
`800895546e6afdbecb2a90984f43fec0534c0e9acf9583148b4c5d760ea05510`.
The Grass, Shrubs, Rocks and NaturalStone payloads are identical between these
copies. Their Forest records differ at the variant field of two records:
the Documents copy has two family-1 / variant-255 entries, replacing one
variant-1 and one variant-3 entry. Other map objects also differ, so use the
Documents audit for the current Unity test.

| Layer | Saved records | Covered by current catalog | Missing coverage |
| --- | ---: | ---: | ---: |
| Forest | 1,012 | 857 | 153 ordinary placements plus 2 unresolved variant-255 records |
| Grass | 2,430 | 0 | All |
| Shrubs | 51 | 0 | All |
| Rocks | 638 | 0 | All |
| NaturalStone | 176 | 0 | All |
| Total | 4,307 | 857 | 3,450 |

These are saved record counts and catalog coverage, not a measured visible-object
count. Original-game visibility, lifecycle states and culling can reduce what is
actually drawn. The installed copy has 859 covered Forest entries and 153 missing
ordinary Forest entries instead.

The runtime `ForestReader` decodes all Forest families, but
`S2MVegetationAssetCatalog.TryGetTreeAsset` accepts only families 0–2 and variants
0–3. `S2MVegetationAssetLoader` skips everything else. Grass, Shrubs, Rocks and
NaturalStone reach the generic `RawS2Object` fallback in `S2MReader`; no typed
placement loader or renderer consumes them. Their bytes are retained, but their
models/sprites are never submitted to Unity.

## Forest families

All asset names below are relative to `meshes/landscape/`, with `.gr2` appended.
Variant counts are written as `variant: count`. Names identify installed assets;
they do not imply botanical species where the source provides none.

| Family | Assets in variant order | Current-map variants/counts | Status |
| ---: | --- | --- | --- |
| 0 | forest_tree_1, forest_tree_2, forest_tree_3, forest_tree_4 | 0:45, 1:51, 2:51, 3:68 | Covered |
| 1 | winter_tree_1, winter_tree_2, winter_tree_3, winter_tree_4 | 0:160, 1:160, 2:169, 3:148; 255:2 | Ordinary variants covered; 255 unresolved |
| 2 | tree_1, tree_1b, tree_1c, tree_1d | 0:2, 1:2, 2:1 | Covered |
| 3 | tree_2, tree_2b | 0:1, 1:2 | Missing: 3 |
| 4 | tree_3, tree_3b, tree_3c | 0:1, 1:2 | Missing: 3 |
| 5 | tree_4, tree_4b | 0:3, 1:2 | Missing: 5 |
| 6 | tree_5 | 0:2 | Missing: 2 |
| 7 | tree_6, tree_6b, tree_6c | 1:4 | Missing: 4 |
| 8 | tree_7, tree_7b, tree_7c, tree_7d | 0:17, 1:10, 2:16, 3:18 | Missing: 61 |
| 9 | tree_8 | 0:15 | Missing: 15; hazel-textured asset |
| 10 | marsh_tree_1 through marsh_tree_7 | None | Available; no placements here |
| 11 | tree_10 | 0:3 | Missing: 3; gorse texture |
| 12 | tree_11 | 0:57 | Missing: 57; lavender texture |
| 13 | tree_12, tree_12b | None | Available; willow textures; no placements here |

The missing Forest placements therefore include small vegetation/bushes, especially
gorse and lavender, as well as tree meshes. Do not classify every Forest record
as a large tree. `tree_9.gr2` exists on disk but the recovered loader deliberately
skips that numbered group in favor of the marsh assets at family 10.

Two family-1 / variant-255 records in the Documents copy have no ordinary model
lookup. They need a separate state investigation; do not wrap/clamp 255 to a live
tree variant. No association with chopped trees or stumps is established here.
Chopped-tree and stump assets exist, but that alone does not identify these records.

## Shrubs

These are separate from Forest. All 51 records have variant 0 and enabled byte 1.
`materials/Trees.materialmappings.xml` identifies their alpha foliage textures.

| Family | Model | Texture(s) named by material mappings | Count |
| ---: | --- | --- | ---: |
| 0 | shrub_1.gr2 | shrub_1b | 14 |
| 1 | shrub_2.gr2 | shrub_1c, bracken_2 | 18 |
| 2 | shrub_3.gr2 | bracken_2 | 15 |
| 3 | shrub_4.gr2 | plnt43l | 4 |
| 4 | shrub_5.gr2 | gras12l | 0 |
| 5 | shrub_6.gr2 | leaf16l | 0 |
| 6 | shrub_7.gr2 | plnt06l | 0 |
| 7 | shrub_8.gr2 | plnt45l | 0 |
| 8 | shrub_9.gr2 | grass_reeds | 0 |

The original uses `DoubleSidedTree` materials for these models. Flat alpha foliage
inside a mesh is distinct from the independent Grass sprite system.

## Decorative rocks

All 638 Rocks records are enabled. Family and variant are individual bytes, not a
single uint16 selector. The lookup is **not** `rock_(variant+1)`.

| Family | Variant -> model (count) |
| ---: | --- |
| 0 | 0 -> rock_4 (20), 1 -> rock_5 (20), 2 -> rock_6 (22), 3 -> rock_9 (19), 4 -> rock_12 (21) |
| 1 | 0 -> rock_2 (43), 1 -> rock_8 (29), 2 -> rock_14 (40) |
| 2 | 0 -> rock_1 (95), 1 -> rock_3 (80) |
| 3 | 0 -> rock_7 (89), 1 -> rock_10 (78), 2 -> rock_13 (82) |

Other recovered Rocks families are 4 = `rock_flat_s_1..6`, 5 = `rock_flat_m_1..6`,
6 = `rock_flat_l_1..6`, 7 = `rock_flat_xl_1..6`, and 8 = `rock_iron_1..12`.
None of these families is placed in this map's Rocks object. Although the
installation contains `rock_1..46`, file presence alone does not make every model
an active map placement or establish its selector.

## Natural stone

This separate object contains 176 enabled records, all twelve model selectors:

| Selector | Model | Count |
| ---: | --- | ---: |
| 0 | rock_natural_stone_A1 | 11 |
| 1 | rock_natural_stone_A2 | 22 |
| 2 | rock_natural_stone_B1 | 14 |
| 3 | rock_natural_stone_B2 | 19 |
| 4 | rock_natural_stone_C1 | 12 |
| 5 | rock_natural_stone_C2 | 18 |
| 6 | rock_natural_stone_D1 | 9 |
| 7 | rock_natural_stone_D2 | 13 |
| 8 | rock_natural_stone_E1 | 14 |
| 9 | rock_natural_stone_E2 | 16 |
| 10 | rock_natural_stone_F1 | 14 |
| 11 | rock_natural_stone_F2 | 14 |

These are model placements, separate from terrain `rock_1.dds` painting and
quarry building components. NaturalStone stores X/Z and yaw, without a saved
Y float like Forest/Rocks; the original renderer queries terrain height.

## Grass sprites / billboards

The map contains an explicit Grass object with **2,430** placements, independent
of the terrain material IDs. The original Grass loader loads
`terrain\grass_bb.tga`, supplied by the installation as `terrain/grass_bb.DDS`.
This is a 256×256 alpha atlas with green grass tufts and dry/flowering plants.
Grass draws through the original sprite/billboard submission path rather than
loading a GR2 per placement. Unity currently implements neither this layer nor
its billboard submission. Exact camera-facing constraints and original-game
visual matching still need validation when implementing it.

| Atlas selector byte | Placements |
| ---: | ---: |
| 0 | 353 |
| 1 | 358 |
| 2 | 380 |
| 3 | 350 |
| 4 | 379 |
| 5 | 369 |
| 6 | 17 |
| 7 | 17 |
| 8 | 96 |
| 9 | 38 |
| 10 | 73 |

The renderer has 16 UV cases, with selectors 0–10 used here. It reads the atlas
selector at record offset 24. The uint32 at offset 12 is a different, unresolved
field (values 1–8 here); using it as the sprite selector would select the wrong
appearance. All 2,430 records have enabled byte 1.

`billboard.fx` and `billboard_alpha_reject.fx` exist in `shaders/Landscape`.
Their vertex shaders merely transform supplied positions: the filenames alone
do not prove that facing is computed in those shaders. The sprite geometry and
camera-facing behavior belong to the original submission/rendering system.

## Other unrendered plant actors in the current map

The Documents copy also contains 153 `AppleTree`, 42 `Wheat`, and 12 `Hops`
actor objects. These do not belong to Forest, Grass or Shrubs, and are outside
the totals above. Current building rendering does not load these crop actors.
Their stage/state-to-model mapping remains to be decoded. Available building
models include `apple_tree`, `apple_tree_blossom`, `apple_tree_bare`,
`apple_tree_apples`, `apple_tree_sapling`, and numbered crop stages. Rendering
the farm's building and terrain surface does not render these actors.

## Binary evidence and reproduction

Selectors above were cross-checked against asset initialization and indexing in
the installed `Stronghold2.exe`, SHA-256
`f3ccdf9bb41158202dffc000e5f2036a82f4c1ce8c5c89462f6140000f32d34b`.
Addresses below are virtual addresses in that specific executable, not offsets
in an S2M file. They are research evidence, not runtime dependencies.

| Code address | Evidence |
| --- | --- |
| 0x5C0260–0x5C07A5 | Forest asset initialization; 32-byte family stride, four ordinary variants; marsh replacement at family 10 |
| 0x5C3D60 | Grass texture loader references terrain/grass_bb.tga |
| 0x5C3FD6–0x5C4174 | Grass atlas-byte selection, UV cases and sprite submission to 0x75E1F0 |
| 0x5C4C10 onward | NaturalStone asset array starts at 0x12BF474; A1,A2,...F1,F2 |
| 0x5C505B | NaturalStone selector indexes that array |
| 0x5C58A0 onward | Rocks asset initialization, including non-sequential ordinary rock filenames |
| 0x5C63FF–0x5C640D | Rocks family*12 + variant lookup |
| 0x5C86B8–0x5C87ED | Shrubs asset initialization |
| 0x5C8A5B–0x5C8A66 | Shrubs family*4 + variant lookup |

The leading two uint32 fields in each array are following-byte length and count.
Records start at payload offset 8:

| Object | Record bytes | Selector offset within record | Enabled byte |
| --- | ---: | --- | --- |
| Forest | 65 | uint16 family 48; uint16 variant 50 | State tail not decoded here |
| Rocks | 31 | byte family 28; byte variant 29 | 30 |
| Grass | 27 | byte atlas selector 24; byte 25 zero here | 26 |
| Shrubs | 27 | byte family 24; byte variant 25 | 26 |
| NaturalStone | 23 | uint16 model selector 12 | 22 |

`Notes/tools/audit_s2m_foliage.py` validates array framing, counts each selector,
checks resolved assets exist when given `--game-path`, and exports JSON/CSV.
It does not claim to fully decode placement state, grass UVs or lifecycle fields.

```powershell
python Notes/tools/audit_s2m_foliage.py `
  'C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m' `
  --game-path 'C:/Steam/steamapps/common/Stronghold 2' `
  --output Notes/FileFormats/S2M/S2GameSegment/FoliageAudit/war_chapter8-current
```

Saved exports: [current map summary](FoliageAudit/war_chapter8-current.json),
[current map records](FoliageAudit/war_chapter8-current.csv), and
[installed map summary](FoliageAudit/war_chapter8-installed.json).
Every resolved asset used by both audited maps exists in the local installation.

Implementation order: extend the Forest catalog with the confirmed ordinary
families; add typed readers and shared-model rendering for Shrubs/Rocks/
NaturalStone; add an atlas-based Grass billboard renderer; then handle crop actors
and special Forest states. Missing `.low.gr2` files must be handled explicitly:
several later vegetation families have full-detail assets only.
