# Foliage, grass and rock rendering

The existing `S2MVegetationAssetLoader` / `S2MVegetationRenderer` path now renders
all identified active landscape selectors in the current Documents copy of
`war_chapter8.s2m`. `Main.RunTests` already calls this path; no scene setup or
Inspector wiring changes are required. Inspector-driven loader/renderer components
use the same implementation.

## Coverage

| Layer | Active placements rendered |
| --- | ---: |
| Forest families 0–13 | 1,010 in this map |
| Shrubs | 51 |
| Decorative rocks | 638 |
| Natural stone | 176 |
| Grass sprites | 2,430 |
| Hops, saved stage 0 | 12 |
| Total | 4,317 |

There are zero unsupported active selectors in the test map. Two Forest records
have inactive graphical variant 255. Another 153 AppleTree and 42 Wheat actors
have saved stage -1 (uninitialized/inactive). These 197 records are counted
separately rather than rendered as arbitrary live assets. This does not simulate
farm growth or initialize crops on behalf of the gameplay system.

The original Forest code checks its graphical variant byte as a signed char
(for example executable VA 0x5C0B30). This resolves the audit's previously unknown
variant-255 entries as inactive. Generic unknown selectors still generate explicit
issues, which Main reports as warnings.

The catalog includes the extended tree families, all nine shrub groups, all
identified decorative/flat/iron rock groups, and all twelve natural-stone models.
Asset names/selectors are listed in the [original inventory](FoliageRenderingAudit.md).
Later tree families without `.low.gr2` files use full-detail meshes when low detail
is requested. No names are synthesized for nonexistent low-detail models.

## Decoding and loading

`SceneryLayerReader` decodes Grass/Shrubs/Rocks/NaturalStone into `SceneryLayer`
and `SceneryInstance`. It validates the leading length/count against fixed record
sizes and validates finite transforms. Original payloads and individual records
are retained. Disabled entries and entries outside playable bounds are skipped.

Models are grouped by resolved GR2 path and converted in one batch using the
existing Granny converter/cache. Every placement of a model shares its Unity
mesh, materials and reference-counted textures. Rocks use opaque materials;
foliage uses the existing alpha-tested foliage material path. Natural stone has
no serialized Y and is placed at a triangulated ground-grid height.

Natural stone is temporarily hidden under occupied building footprints and returns
when coverage is released. See [Natural stone under buildings](./NaturalStoneOcclusion.md)
for reversible occupancy, building lifetime handling and validation.

Natural stone uses `OpenSH2/Stronghold 2 Natural Stone` rather than URP Lit.
All twelve imported models reference `raw_stone.bmp`, resolved to the installed
`meshes/landscape/raw_stone.dds`. Its alpha is zero throughout: the original shader
uses this as zero gloss, not transparency. The stone remains opaque.

Opaque shader overrides use Unity-aware `== null` / `!= null` checks. An empty
serialized Shader reference can have a managed wrapper with no native object;
C# `??` treats that wrapper as non-null and prevents fallback selection. Both the
natural-stone selection and generic opaque fallback handle this state. The Unity
validation harnesses explicitly exercise missing-native-object Shader wrappers.

The original `Landscape/Default.fx` enables the Phong shader's unit defaults.
Its diffuse response includes `0.6 * (1 - NdotV)^2` rim lighting and an ambient-cube
contribution scaled by 0.7. The new material restores that non-PBR diffuse/rim
response, using Unity's SH ambient in place of the original ambient cube, while
retaining the scene's sun, shadows, depth and depth-normal passes. Ordinary rocks,
buildings, foliage and the atmosphere are unaffected. This is not an exact port
of the original ambient/ground-shadow maps.

Warm afternoon lighting still warms the stone: the current scene saves atmosphere
time 15.7, with an orange sun gradient, while the original screenshot is in neutral
daylight. Matching that screenshot's lighting requires a comparable daylight time;
the material does not cancel sunset colours or emit light in darkness.

`Main` exposes **Natural Stone Texture Brightness**, default **2**. **1** uses the
original texture brightness; larger values lighten its diffuse RGB before scene
lighting is applied. This is independent of model scale and time of day, and
only affects natural stone. The texture file, opacity, shadows and other materials
are unchanged. Reload the map after adjusting it, as with the model-scale controls.
The standalone vegetation renderer defaults to 1 unless the caller supplies a value.

The tree-model calibration multiplier remains restricted to Forest and AppleTree
models. It does not shrink rocks, shrubs, grass or hops. The parent map-world scale
still affects all layers together. Scenery follows vegetation flip and position
settings. Grass has separate size and alpha-cutoff settings.

`Main` exposes additional model multipliers beside `Tree Model Scale`:

| Main Inspector field | Category |
| --- | --- |
| Vegetation Model Scale | Shrubs |
| Rock Model Scale | Decorative rocks |
| Natural Stone Model Scale | Natural stone deposits |
| Grass Model Scale | Camera-facing grass cards |
| Crop Model Scale | Apple trees, hops and wheat |

These default to 1 and apply when the map loads, preserving existing sizes until
adjusted. All Forest records, including the gorse/lavender families, continue to
use Tree Model Scale. Apple trees retain that multiplier as well as Crop Model
Scale. Model multipliers compose with the existing asset/appearance scale; Grass
Model Scale composes with Grass Size. Placement coordinates, stone ground heights,
grass anchors and wheat cluster positions stay fixed. Grass culling bounds expand
with its cards. Invalid nonpositive or nonfinite category scales are rejected
before any scene objects are generated.

## Grass

Grass uses `terrain/grass_bb.dds`, a 4×4 atlas. The selector is record byte 24;
the integer at offset 12 is not used as the appearance selector. Each placement
is a bottom-anchored card, using the matching quarter-width/quarter-height tile.
The original submission's +1/8 U field is a bottom-center coordinate, not the
right edge of a tile. Treating it as an edge incorrectly crops each tuft in half.

Cards are grouped into 16×16-cell chunks. Each mesh stores center positions and
corner offsets; the URP shader expands them along the active camera's horizontal
right vector and world up. The same expansion and alpha rejection are used in
forward, depth-only and depth-normal passes. Mesh bounds include card extents
for culling. Cards receive lighting/shadows but do not cast shadows themselves.
This supplies upright camera-facing grass without a GameObject/update per tuft.

## Saved crop stages

`PlantActorReader` reads the position from the packed actor prefix and finds the
bounded final state payload, allowing optional inherited type-name registration.
AppleTree, Hops and Wheat retain their raw payloads. Their final blocks are 20,
16 and 52 bytes respectively. The leading four bytes are a reference; stage is
the following int32. Wheat also retains eight saved cluster offsets.

Apple stages 0–4 select sapling, bare, blossom, leaf, and apples models. Original
stage 5 reuses bare, and higher nonnegative stages reuse apples. The source render
routine is at VA 0x5DB6F0 and the state serializer at 0x5DBB20. Hops stages 0–6
select `hops_stage_1..7`; its original renderer is at 0x5DCC10. Source placement
offsets (+1024 X for apple trees, +512 X/Z for hops) are preserved.

Wheat stages 0–5 use the original six-entry UV table at VA 0xAB8820 and
`meshes/buildings/farm_plants.transparent.dds`. Static crossed alpha cards use the
four saved cluster positions. This is a static approximation of the original
procedural crop rendering, not its wind/animation system. The serialized stage
mapping is confirmed by the original serializer at VA 0x5DDA35.

## Validation

`Notes/tools/FoliageRenderValidation.cs` is an isolated Unity editor batch harness.
It loads the exact map referenced by Main, checks typed-layer and active/inactive
counts, converts the map's actual models, creates all scene objects, checks textures
and shader errors, validates low-detail fallbacks and truncated-array rejection,
and captures URP previews. Positive apple, hops and wheat stages are also tested
using synthetic actor states because the map saves apple/wheat at stage -1.

Unity 6000.4.7f1 validation passed with 4,317 active placements, 1,887 model
instances, 2,430 grass cards, 197 inactive records and zero unsupported entries.
All 18 positive crop-stage fixtures passed. Model/grass/crop previews were inspected.
Runtime C# compilation against the installed Unity references also passed.

The test found and fixed an editor cleanup issue: generated model resources now
use `ExecuteAlways` so destroying editor previews releases meshes, materials and
shared texture references. The harness checks that generated grass materials are
released after destroying the scene roots.

Temporary harness project: `Temp/WallValidationProject`. Results and previews:
`Temp/FoliageProbe`. These are ignored by Git; installed game assets are not copied
into tracked files. Tree and bush sway is now implemented; see [Tree sway](./TreeSway.md)
for asset inspection, GPU rendering, controls and validation. Original-game pixel
matching, crop wind, growth/harvest simulation, tree chopping and scenery collision
are separate work.
