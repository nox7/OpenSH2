# Team colors and crests

Research and implementation date: 2026-10-25. The findings below are implemented by `S2MLiveryContext`, `S2MLiveryOwnership`, and the actor/building renderer integrations.

## Native palette and default identities

The installed Stronghold 2 executable contains the following palette. It matches the requested colors, so approximate replacement colors are unnecessary. The identity column uses the scenario estate-marker lord convention documented in `../MapHeaderSegment/Enums/Lords4Enum.md` (relative to the S2M directory). It is a default association, not a universal interpretation of every numeric field in a map.

| Default identity | Color choice | Native RGB | Crest texture |
| --- | ---: | --- | --- |
| Player | 1 | `#FF0000` | `crests/crest1.PNG` |
| Olaf | 2 | `#FF8200` | `crests/crest2.PNG` |
| Lord Barclay | 3 | `#00A900` | `crests/crest3.PNG` |
| The Hawk | 4 | `#00D3D3` | `crests/crest4.PNG` |
| The Bull | 5 | `#2D2DFF` | `crests/crest5.PNG` |
| Lady Seren | 6 | `#F353E4` | `crests/crest8.PNG` |
| Edwin | 7 | `#F5F500` | `crests/crest7.PNG` |
| The King | 8 | `#6A1489` | `crests/crest9.PNG` |
| Sir William | 9 | `#8D0000` | `crests/crest6.PNG` |
| Sir Grey | 10 | `#727272` | `crests/crest10.PNG` |

Choice zero is black (`#000000`), with `crest0.PNG`. It must not become the player's default merely because a particular serialized enumeration calls the player zero. Additional white-knight and black-knight crest textures exist separately. The ten listed colors are native color choices; a saved game can remap player choices, so they are not an immutable personality-to-RGB rule.

Primary binary evidence: installed `Stronghold2.exe`, SHA-256 `f3ccdf9bb41158202dffc000e5f2036a82f4c1ce8c5c89462f6140000f32d34b`. Its 32-entry BGRA palette starts at file offset `0x68D190`, virtual address `0x00A8DD90`. Function `0x00408110` resolves choices below 11 through a per-player indirection at object offset `0xCE358`, then indexes this palette, with a 32-entry bounds check. Function `0x00420470` loads `crests/crest%d.png` and explicitly substitutes 6→8, 8→9, 9→6. Thus using `crest{color}.png` without those exceptions is incorrect. All eleven numbered PNGs are 128×128.

Run `python -B Notes/tools/probe_livery_assets.py "C:/Steam/steamapps/common/Stronghold 2"` to reproduce the palette, hash, crest availability, and human definition settings. The palette offset is specific to the verified executable; the probe refuses an unknown hash.

## Which outfit parts receive color?

Use the original material classification and triangle groups, not a white-pixel detector. White skin details, metal, eyes, and unrelated cloth must remain unchanged.

Primary asset sources, relative to the installation:

- `materials/stronghold2.materials.xml`: `Unit_TeamColour`, normal-mapped variants, and `Unit_TeamCrest` identify the relevant effects.
- `materials/Units.materialmappings.xml`: mappings are scoped to source model and source texture. Knight, swordsman, and warrior-monk shields use crest materials. `horse_knight.gr2` also has an explicit crest mapping for its decorative covering. Light-cavalry flags have explicit team-color mappings.
- Human `.uni` definitions contain two integers after the animation-slot list. In the reference implementation, the first selects the default team-colored triangle group when there is no explicit material mapping. Installed archer, lord, William, knight, Olaf, queen, hawk, and bull definitions all have first setting 1. This group interpretation is supported by the reference project; the integer values themselves can be independently reproduced by the probe. Animals have a different definition layout; do not apply the human trailing-field layout to `.aml` files.

The usual effect, `shaders/Units/TeamColour.fx`, enables team coloring in `PhongShader_20.fx`. The vertex color is multiplied by `TeamColour`, then modulates the diffuse texture. Original grayscale cloth shading and texture details survive. The color is applied to selected material groups, rather than the whole unit. This covers tunics and capes without requiring separate recolored textures.

There is an important LOD exception: `Units/UberLow.fx` defines `_PHONG_SHADER_DIFFUSEMAP_ALPHA_IS_TEAMCOLOUR_AREA`. That path blends white and team RGB using diffuse alpha, multiplies the diffuse RGB by that result, and forces the resulting alpha to one. The mask is not transparency. Do not impose this alpha-mask rule on normal-detail models, or use ordinary transparent blending for these LODs.

`Units/TeamCrest.fx` samples `Sampler_CrestMap` with team coloring disabled. A crest is a texture replacement, not a white region to tint. Preserve the crest artwork's own colors.

## Building crests

`materials/Buildings.materialmappings.xml` marks crest surfaces on keeps, barracks, armouries, solars, gatehouses, jousting structures, and flags, among others. `Building_TeamCrest` selects `Buildings/LightMapWithTeamCrest.fx`; `FlagTeamCrest` selects `Buildings/TeamCrest.fx`. The former retains building lightmaps while sampling the selected crest. Separate building/flag team-color materials also exist.

The yellow “REPLACE WITH CREST” texture is the authored placeholder for this substitution. Resolve the owning estate's crest and replace only the mapped crest surface. Preserve other surfaces, lighting, UVs, and flag animation. PNG/DDS texture orientation should be checked in Unity against existing source UV handling before shipping.

## Ownership and serialization

The repository already documents several incompatible lord-number conventions in `MapHeaderSegment/Enums/Lords1Enum.md` through `Lords4Enum.md`. The current `S2MLords` enum uses no-lord=0, player=1, Olaf=2. Other fields use player=0 or Olaf=0. Estate number, player number, AI personality code, team/alliance, color choice, and crest choice are different concepts. Never fix this by globally adding one to every ID.

The following serialization interpretation comes from examining the SH2-Reforged reference project and must be validated against representative initial maps and saves before using it as a production decoder:

- Base `Entity` stores an estate ID. Resolve estate→player/identity using the relevant scenario or saved-player field convention.
- `S2Actor` field 0 contains a packed active livery word at byte offset 25; field 2 begins with an auxiliary color word. Reference names are `Field219`→runtime `Field612` and `Field317`→`Field263`.
- Packed livery uses low 16 bits for color and high 16 bits for crest. Actor crest values 0 or `0xFFFF` fall back to the color choice. This allows a crest and clothing color to differ.
- `S2Estate` field 8 begins with crest override (`Field858`), then color override (`Field576`). A value of -1 defaults to the estate's player choice; crest defaults to the resolved color choice. Buildings use estate livery.
- `S2Game` field 7 stores eleven color-order integers. Saved-player number, home estate, and personality code are distinct fields. Initial campaign maps can require startup ownership assignment rather than already containing final active livery.

These findings do not establish that every actor in `war_chapter8.s2m` already has a final valid livery word. Pending/unassigned data must resolve through scenario startup ownership, not produce palette entry 65535 or a black player. Explicit saved overrides should be a separately configurable policy from the requested default lord palette; default player red should resolve by local-player identity.

Reference sources examined: `Source/OpenSH2.Formats/MaterialLibrary.cs`, `Source/OpenSH2.Formats/Maps/MapStart.cs`, `Source/OpenSH2.Simulation/Kingdom/Setup/GameStart.cs`, and the original-asset livery, game-view, entity-save, and estate-save readers under SH2-Reforged. `GameStart.LordCodes` is `{0,-1,8,9,7,6,3,5,2,1,4}`, further demonstrating that personality codes are not color slots. No reference source code was copied. Native palette, crest remapping, effects, and installed definition values were checked independently.

## Integration recommendation

1. Add a shared livery resolver with semantic lord/local-player identity, native default palette, saved override support, and configurable fallback colors/crests.
2. Preserve `.uni` team-group settings and classify source model/texture/group through the original XML mappings. Explicit mappings take precedence over the default group.
3. Add per-instance team RGB to the existing GPU actor draw data. Enable tint only on classified groups; keep shared meshes, animation poses, and materials. Skinning and selection objects need no architectural change.
4. Resolve crest surfaces using a texture array with a per-instance slice, or batch by the small number of shared crest materials. The native numbered crests have matching dimensions. Avoid unique textures/materials per NPC.
5. Apply the same classification/resolver to building and attachment renderers. Horses, shields, capes, and flags must inherit their owning actor/estate's resolved livery.
6. Validate normal and lowest LOD units, mounted knights, all ten identities, saved overrides, player-zero/player-one conventions, and building crests in Unity. Check fog, sunlight, shadows, alpha, and crest orientation.

## Implemented behavior and customization

`Main` exposes **Livery Settings**. Palette overrides replace native RGB values and optionally supply a readable 128×128 crest for a choice. Estate overrides change the fallback lord assignment. The switches control saved actor/estate liveries, saved color-order remapping, local-player red, and native PNG orientation. Native PNG rows are flipped to match the top-first DDS loader and original GR2 UVs; custom crest textures should already use this orientation. Settings are consumed when the map context is created; changing them requires recreating/reloading that context.

`S2MActorCatalog` now retains the `.uni` team-group integer. Custom actor parts can set `TeamGroup`: -2 inherits the definition, -1 disables fallback tinting, and nonnegative values select a group. Explicit original XML material mappings still take precedence. All original source meshes and converted skinning/animation caches remain shared.

The actor shader carries one per-instance RGB/crest vector alongside playback data. It supports normal group tinting, lowest-LOD alpha masks, and a shared 14-slice crest texture array (eleven numbered choices, white/black knight, and neutral cloth). Rider, horse, and equipment receive the same resolved data. `S2MActorRenderer.SetLivery(id, value)` updates every loaded part and animation variant without material allocation. `S2MLivery.Color` is in the active rendering color space; values returned by the resolver are ready for this API. Visibility compaction preserves the order of playback and livery vectors together. Batches are capped at 256 instances to match the shader constant-buffer capacity.

Buildings replace only mapped crest/color materials and cache variants per source material/livery. Animated building parts refresh their material clock targets after variant assignment, preserving ambient animation. Generated textures, array, and material variants are owned by the map context and released with it. Legacy actor `CrestTexture`/`CrestColor` settings remain a fallback for standalone rendering without a map livery context.

Static buildings use the `StaticOriginalModel` resource material, whose shader reuses the actor shader's opaque lighting, shadow, depth, and normal passes with GPU skinning disabled. This keeps sunlight and atmosphere behavior consistent across original assets and avoids the incorrect color output observed with runtime-created standard Lit materials in the isolated deferred-rendering preview. Both static and animated materials retain instancing; no per-building texture copies are created.

## Chapter 8 verification

`war_chapter8.s2m` has 792 decoded actors. Its 278 archers carry these valid saved color choices: 1→21, 3→79, 4→24, 7→13, 9→110, 10→31. Its 60 knights also have matching saved color and pending-player choices. These choices must not be replaced by the lord associated with the same-numbered territory.

The initial game segment has no saved players and its estate player references are unassigned. Scenario field 3 provides 21 estate-to-lord entries, including estate 1→player, 2→Hawk, 3→Edwin, 8→Grey, 10→William, and 11→Barclay. Building footprints are sampled in the estate grid, then resolved through this table. The five keeps resolve to estate/crest-choice pairs 10/9, 2/4, 3/7, 11/3, and 1/1. For example, estate 3 buildings are yellow/Edwin, while actor color choice 3 is green/Barclay.

`Notes/tools/LiveryValidation.cs` exercises the installed map, independent serialized fixtures for local player #0 and Olaf #1, separate packed color/crest words, unset crest fallback, native definition groups, rendered unit colors and mounted equipment, building placeholder removal, and resource cleanup. A dense 5,000-archer fixture with all ten colors shares three materials and uses 20 spatial draw batches; it does not create per-unit GameObjects. Validation images and reports are written under ignored `Temp/LiveryValidation`.
