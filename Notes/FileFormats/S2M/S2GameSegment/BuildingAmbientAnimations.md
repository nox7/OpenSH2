# Ambient building animation

Continuous decorative motion can run before a gameplay simulation exists. The default ambient catalog currently enables only the verified windmill wings and shaft, using the animations embedded in `meshes/buildings/windmill_wings.GR2` and `windmill_shaft.GR2`. These replace their previous static renderings; the mill body, placement, collision and footprint remain unchanged.

Pitch scooping, dairy machinery, gates, construction, production, worker actions and other interactions are not automatically looped. Their timing and conditions belong to future simulation state. Finding an animation in a GR2 file alone does not make it an always-active animation.

## Loading and rendering

`S2MBuildingAnimationCatalog` defines ambient part models and their optional separate clips. `S2MPlacedBuildingAssetLoader` includes their geometry in the normal building asset batch, then requests their animation data from `Granny2AnimationAssetLoader`. The sampler/cache implementation is shared with units. Existing actor caches and the version-2 native animation format remain compatible. Default building animation caches live under `Application.persistentDataPath/OpenSH2/Cache/Animations`; an explicit building cache directory uses its `Animations` subdirectory.

`Granny2AnimationRendering` supplies pose textures, skin channels and bounds covering the complete sampled animation to both actors and buildings. Building parts use the existing animated shader, including skinned shadow, depth and depth/normal passes, so the map atmosphere and PC SSAO see the animated geometry. Each part batch owns its generated meshes, materials and pose texture; these resources are shared across its instances and released with the map. Each part type has one clock, with no individual skeletons, Animators or frame-update components per building. Parts retain the existing building-root ownership even after their render instances are reparented.

Missing or unsupported optional animation falls back to static part geometry and reports an issue. Geometry conversion failure still follows the existing optional-part handling. New static or animated parts must use the building model's authored origin and coordinate convention.

## Settings and future simulation integration

Main exposes `Building Render Settings > Ambient Animations`:

- `Enabled` enables animated loading/rendering. Changing it after load freezes existing clocks; disabling it before loading uses static geometry.
- `Playback Speed` scales the shared clock and can be changed while playing. Zero pauses it.
- `Samples Per Second` controls native sampling/cache size (default 15); the shader interpolates samples at render frame rate. Reload to change this setting.
- `Randomize Phase` gives each building a stable time offset in seconds. The same building ID produces the same offset for wings and shaft, preserving their timing.
- `Overrides` adds ambient part models or replaces default entries by object type and model path. Set an entry's `Enabled` false to leave that part static. Empty `Animation Path` selects the model's embedded animation. Reload after changing definitions.

Pass the same `S2MBuildingAnimationSettings` to `S2MBuildingAssetLoader.Load(..., animationSettings: settings)` and `S2MBuildingRenderSettings.AmbientAnimations` when using the APIs independently of Main. Only registered parts are sampled; ordinary static buildings incur no animation conversion or update cost.

`S2MBuildingAmbientAnimation.SetPlaying(buildingId, playing)` pauses/resumes an individual building's instances without a pose jump. A simulation can call it on the map's relevant part controllers when production state becomes available. `Advance(seconds)` and `SetTime(seconds)` also allow deterministic external clock control; disable the component's automatic `LateUpdate` when driving it externally. This is a presentation hook, not a production simulation.

## Validation

`Notes/tools/BuildingAnimationValidation.cs` loads the actual Chapter 8 mill in an isolated Unity project and checks both authored loops, visible before/after image changes, per-building pause/resume continuity, zero frame allocations, static fallback when disabled, and GPU resource cleanup. `ActorRenderValidation` checks the shared pipeline against all 792 Chapter 8 actors and a 5,000-actor crowd.
