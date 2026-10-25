# Placed actors and runtime animation

`Main` now loads `S2MFile.Actors` after scenery. `S2MActorAssetLoader.LoadAsync` does disk reads and native conversion on a worker; `S2MActorRenderer.Render` creates Unity resources on the main thread. The optional `S2MActorRendererComponent` subscribes to `S2MMapLoader.MapLoaded`, discards superseded asynchronous results, and replaces its previous actor renderer. Use either Main's integration or the component for a given map, to avoid duplicate populations.

This is presentation of actors already serialized in the map. It does not create population, simulate movement, execute saved action queues, or add AI, navigation, combat, colliders, or recruitment. Animals follow the same loading/rendering path as people.

## Save data

The inherited Entity field 0 has a 59-byte packed body. Its on-map byte is at body offset 12, and position XYZ at 21/25/29 (raw object payload offsets 20 and 29/33/37). Entity field 10000 contains animation ID, rate, time and loop flag. Actor fields 0 and 2 contain variable-length path/action arrays; heading is read after these arrays, rather than guessed from a fixed offset. Actor's own on-map flag takes precedence. Inherited blocks are traversed by their declared lengths and their type registrations, which are local to the game segment. A 59-byte body alone does not identify an actor: a framed Actor section is required. Raw map objects remain available for further decoding.

Map X/Z divide by 1024. Saved Y uses the terrain height scale, including elevated placements; it is not replaced with sampled ground height. Terrain axis flips and parent map scaling are respected.

## Asset selection and customization

`S2MActorCatalog` discovers `.uni` and `.aml` definitions under `meshes/units`. Normalized class names resolve automatically, with a small alias table for differences such as `Dog` / `hunters_dog` and `Assassin` / `assasin`. Fixed-width names end at the **first** null, because unused bytes can contain previous exporter names.

Human `.uni` slots select an action in the skeleton family's `AnimList.anl` table. Animal `.aml` entries are indexed by the animal action number, **not** their order in the file. Missing saved clips fall back to an available idle, then an available clip. Active animation attachment states choose weapons, shields and props; attachment point numbers resolve to skeleton bone names. Explicit custom bone names are supported. The native cache retains both skinning composites and bone world matrices, so rigid props can follow bones without CPU transform hierarchies.

Main exposes `Unit Model Scale` beside its other model scale controls. This multiplies actor rendering scale (including equipment and vehicle parts) without moving saved placement anchors; apply changes by reloading the map. It composes with `Actor Render Settings.ModelScale` and definition overrides.

Main also exposes `Actor Render Settings`. Its `Overrides` array maps a saved `ObjectType` to a definition, model, animation, scale and local transform. Paths are relative to the installed game and checked against traversal outside it. `DisableDefinitionAttachments` replaces default equipment; `Attachments` adds custom props using either `Point` or `BoneName`. `Parts` adds independently posed model/definition parts to the same saved actor. Explicit model-only parts use their embedded animation, or their bind pose when no animation exists. Overrides replace built-in mappings for their class.

Horse carts have a default assembly of a seated rider, horse, cart and wheels. Carts and siege engines currently hold a placed pose. The ballista/fire-ballista fallback uses the combined ballista mesh. Full siege crew/aim/articulation, cart load variants and terrain-following vehicle articulation are future work. Saved lord appearance variants, animal age/coat variants, and player/team recoloring are not decoded yet; override the definition to choose another appearance. Unknown actor classes are reported once per distinct issue and skipped without guessing a model.

## Animation and scale

The existing x86 `Tools/Granny2MeshConverter` loads the user's installed Granny DLL. `--actor-batch` accepts UTF-8 groups of four lines: absolute model GR2, absolute clip GR2, output cache, integer sample rate. The result file has one line per group, empty for success or base64 UTF-8 error text. Rebuild the converter with:

```powershell
dotnet build Tools/Granny2MeshConverter -c Release
```

No native runtime is loaded into Unity. `.osh2actor` version 2 contains duration, palette size, frame count, skeleton bone names, four normalized weights/indices per drawable vertex, then sampled skin composite matrices and attachment world matrices per frame. Skin indices are remapped from mesh-local bindings to the model skeleton using Granny's mesh binding API. Rigid, unanimated files receive an identity skin palette.

Caches live under `Application.persistentDataPath/OpenSH2/Cache/Actors`. Keys include installation/source paths, source/clip timestamps, converter and Granny DLL timestamps, cache version and sample rate. Repeated models are converted in a batch and loaded once within the asset request. Concurrent actor requests serialize conversion to avoid competing cache writes. Completed caches survive native-process failure; absent or invalid caches produce issues rather than invalid GPU data.

The default animation sample rate is 15 Hz. The vertex shader interpolates adjacent matrices at the rendered frame rate; increasing sampling improves fast rotations at the cost of cache size and GPU memory. Root motion is visual only. `SetPlayback(id, seconds, rate, loop)` changes playback of the already selected clips for every part of that actor; it does not load or switch to another clip. Select different clips through overrides and reload. `SetTransform` changes the actor's map-local transform and moves its parts together. Held assemblies can be driven explicitly through these APIs later.

## Population cost and ownership

Each distinct model/clip/equipment group creates shared meshes, materials and an RGBAFloat pose texture, with no individual actor GameObjects, bones or Animators. Materials and poses are shared by all spatial batches in the group. Frame submission uses preallocated transform/property arrays; there are no CPU bone updates. Spatial grouping uses `CellsPerBatch` (16 by default), splitting again at Unity's 1,023-instance limit. Unity culls each instanced draw as a group; it does not cull individual instances inside it. Choose smaller spatial cells for tighter culling or larger cells for fewer submissions. Mesh bounds include all sampled animation poses and attached props. No distance LOD or occlusion system is claimed here.

The URP shader skins in forward, shadow, depth and depth/normal passes, supports diffuse/alpha-test materials, clustered main-light color/shadows and ambient lighting. Actors write their posed depth/normals for PC SSAO and the map atmosphere; fog is applied once by the shared atmosphere pass. Its Resources material enables instancing so player builds retain instancing variants even though game geometry is created at runtime. This renderer requires instancing and RGBAFloat vertex textures. Generated resources and shared diffuse texture references are released when the actor root is destroyed. Disable the actor root/component to stop frame submission.

## Validation

`Notes/tools/ActorRenderValidation.cs` runs in an isolated Unity project containing the current Code directory. Invoke `-batchmode -executeMethod ActorRenderValidation.Run`. It checks Chapter 8's 792 actor records, all 278 archers, animals and their action mapping, native matrix motion, skin data, equipment, shader compilation/rendering, playback, and resource counts. A 5,000-archer dense crowd checks the instancing limit and managed allocations. Its CPU submission timings are editor measurements, not GPU or complete-game benchmarks.

The supplied SH2-Reforged tree was consulted for serialized-field and game-definition layout information; its implementation was not copied into this project.
