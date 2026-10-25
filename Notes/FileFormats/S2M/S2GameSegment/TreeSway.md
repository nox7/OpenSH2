# Tree and bush sway

Trees and Shrubs now sway as rigid models in the foliage vertex shader. Bark and
leaves receive identical rotations around the placement's origin. No branches are
animated independently. Grass billboards, rocks, natural stone and farm crops keep
their existing rendering behavior.

## Asset inspection and original behavior

The converter supports a read-only metadata inspection mode:

```powershell
OpenSH2.Granny2MeshConverter.exe --game-path "C:\Steam\steamapps\common\Stronghold 2" --inspect-animations manifest.txt
```

The manifest contains one installed GR2 path per line. Inspection uses the installed
Granny runtime and frees every file after reading its metadata; it does not convert
or modify the assets. A scan of 80 landscape tree, winter tree, forest tree, marsh
tree and shrub files (including low-detail models and the stump) found single-bone
skeletons, one bone binding per mesh, and no morph targets or track groups. Living
trees and shrubs have zero animation entries. `tree_stump.GR2` alone reports one
animation entry, with no track groups; it does not supply living-tree wind clips.

Inspection of the installed executable's Forest render routine at VA 0x5C12B0
confirms procedural whole-model sway. VA 0x5C13BB–0x5C1469 computes
`sin((seconds * 90 + phaseDegrees) * pi / 180) - 1`, wrapping at 360 degrees,
and halves the amplitude for families 2–6 and 13. The close model path uses twice
this angle and rotates around a horizontal axis (D3DX rotation Z), before applying
the world translation. The period is four seconds. The original phase source
looks up terrain-cell data at 0x776190; that data's initialization is not decoded.
The special marsh-tree branch does not apply this same sway matrix.

OpenSH2 uses the recovered period, biased sine shape and family amplitude factors.
As requested, it adds deterministic variation in both phase and horizontal direction
and applies sway to bushes and marsh trees too. A position/family hash supplies
full-cycle phases and directions without random-number allocation or load-order
dependence. This is a visual reproduction, not a claim of frame-exact matching to
the original terrain phase table. No code from the SH2-Reforged checkout is reused;
its checked-in license currently restricts reuse in other projects.

## Controls and rendering

In Main's Inspector, expand **Vegetation Render Settings > Tree Sway**:

- **Enabled** defaults to true.
- **Strength** defaults to 1: up to four degrees for ordinary trees, two degrees
  for families 2–6/13 and shrubs. Zero disables displacement.
- **Speed** defaults to 1: a four-second cycle. Zero freezes each model at its
  phase. Both numeric settings accept 0–5.

Settings are applied when vegetation is rendered, so reload the map after changing
them. Existing model scaling and the scene's lighting/time of day remain in effect.

The renderer uploads one instanced float4 per placement once: phase, amplitude,
speed and direction. Materials and meshes remain shared by asset. The shader uses
Unity's time directly, with GPU instancing variants in every pass. There are no
per-tree MonoBehaviours, Animators, CPU transform writes, mesh rebuilds, per-frame
allocations or per-frame property uploads for sway. Rotation occurs after model
scaling and rotates normals too, keeping the whole model rigid under nonuniform
scale. Each renderer's local bounds include the full swept volume for culling.

Forward lighting, shadows, depth and depth normals all use the same vertex function.
Bark stays opaque and culled; foliage retains alpha clipping and the existing
double-sided setting. Alpha and instancing shader variants are explicitly compiled
for dynamically created runtime materials.

## Validation

`Notes/tools/TreeSwayValidation.cs` runs in the isolated Unity 6000.4.7f1 project.
It passed on `war_chapter8.s2m`: 1,061 animated placements (1,010 Forest and 51
Shrubs), 1,056 distinct phases and 1,055 distinct directions. Checks cover bark/leaf
parameter agreement, opaque scenery exclusion, shared instanced materials,
expanded culling bounds, disabled mode and absence of animation update components.

Actual instanced GPU draws at times 0, 1 and 4 seconds verified changing geometry
and four-second periodicity in the depth and normal passes. All four shader passes
were exercised without compilation errors. The existing full vegetation validation
also passed: 4,317 active placements, crop stages, textures, resource cleanup and
URP previews. Runtime C# and the x86 Granny converter compile successfully. This
does not constitute a full-game FPS benchmark.

### Forward+ lighting regression

The initial sway shader omitted the `_CLUSTER_LIGHT_LOOP` variant. Both project
renderers use Forward+ (`m_RenderingMode: 2`), where URP's main-light attenuation
does not use the legacy `unity_LightData.z` flag. Multiplying that flag into the
new foliage lighting path could remove sunlight entirely. The forward pass now
compiles the Forward+ variant. Instance IDs are also transferred into fragment
shaders so instanced ambient SH coefficients and rendering-layer data use the
correct placement rather than instance zero.

The sway harness now checks instanced bark and leaves under Forward+ with the
legacy flag deliberately zero, and two instances with different ambient probes.
Sunlight and ambient checks pass alongside the existing geometry/sway checks.
The manual offscreen test sets inverted culling to match its render-texture
projection, ensuring front/back lighting tests use the intended surface normals.
