# Granny 2 assets

Stronghold 2 stores landscape props, buildings, units, animations, and some UI meshes
in RAD Granny 2 (`.gr2`) files. OpenSH2 does not redistribute those assets or RAD's
runtime. Asset conversion requires files from a locally installed copy of Stronghold 2.

## Confirmed Stronghold 2 format

`meshes/landscape/tree_1.gr2` has:

- 32-bit little-endian Granny magic `B8 67 B0 CA F8 6D B1 0F 84 72 8C 7E 5E 19 00 1E`;
- GR2 file-format version 6;
- eight sections;
- Granny/Oodle section compression type 2 for its populated data sections.

The installed `granny2.dll` reports runtime version `2.6.0.14` and is an x86 DLL. It
exports the standard file, mesh conversion, index, material-group, and texture APIs.
Because the Unity 6 editor is x64, it cannot load this DLL in-process.

The open-source [LSLib GR2 reader](https://github.com/Norbyte/lslib/blob/master/LSLib/Granny/GR2/Reader.cs)
and [format model](https://github.com/Norbyte/lslib/blob/master/LSLib/Granny/GR2/Format.cs)
are useful format references and are MIT-licensed. The reader still routes compressed
sections through its `Granny2Compressor` native support, so those two files alone are
not a managed replacement for compression type 2.

## OpenSH2 conversion architecture

`Tools/Granny2MeshConverter` builds as an x86 .NET Framework executable. It dynamically
loads `granny2.dll` from the user-selected Stronghold 2 installation, asks that matching
runtime to convert mesh vertices into PNT332 form, and writes an engine-owned
`.osh2mesh` cache. The converter validates all of the following before reading:

- `Stronghold2.exe` exists in the selected installation;
- `granny2.dll` exists in that installation;
- the input has a `.gr2` extension;
- the input resolves inside the selected installation directory.

This is a technical installed-copy requirement, not a cryptographic proof of license
ownership. OpenSH2 still must not package the original DLL, GR2 files, or DDS textures.

Build the helper once from the repository root:

```powershell
dotnet build .\Tools\Granny2MeshConverter\Granny2MeshConverter.csproj -c Release
```

The Unity-side flow is deliberately split:

1. `Granny2AssetConverter.LoadOrConvertMany` validates the installation, updates
   missing/stale `.osh2mesh` files in one converter process per asset batch, and
   loads each distinct `Granny2ModelData` once. `LoadOrConvert` wraps this for
   single-model inspection.
2. `Granny2ModelRenderer.Render` accepts only already loaded `Granny2ModelData`; it
   performs no GR2 conversion or cache-file I/O.
3. `Granny2ModelLoaderComponent` and `Granny2ModelRendererComponent` provide the same
   event-driven separation for scene use.

That component pair is intentionally a single-model inspection path. Map vegetation
instead uses `S2MVegetationAssetLoader`/`S2MVegetationLoaderComponent` to inspect the
already-loaded S2M `Forest` records and request only the distinct GR2 models the map
needs. `S2MVegetationRenderer` then instances shared generated meshes and materials
without doing file I/O.

For a test tree, configure the loader with:

```text
Game Install Path: C:/Steam/steamapps/common/Stronghold 2
Asset Relative Path: meshes/landscape/tree_1.gr2
```

Then assign that loader to a `Granny2ModelRendererComponent`. The loader caches beneath
`Application.persistentDataPath/OpenSH2/Cache/Meshes`; the renderer creates one Unity
mesh per GR2 mesh and one submesh per Granny triangle material group.

## Map loading performance

The original implementation launched a separate x86 converter process for every
uncached GR2. Extending building support increased chapter 8 to 165 distinct
building assets, so process startup dominated the first load. Rebuilding the
converter also invalidates the older caches; this explained the editor log's
176 conversion messages across buildings and vegetation.

Building loaders now gather their asset requests before loading. Ordinary
buildings, keeps, bridges, and vegetation each convert their stale requests in
a batch, sharing one Granny DLL load/process per group. A UTF-8 manifest contains
input/output path pairs. The converter returns one result per asset, so a damaged
optional model does not discard unrelated successful conversions. Temporary
manifests/results are removed after the process completes. The single-file CLI
remains supported.

The cache still checks source GR2, installed Granny DLL, and converter modification
times. A current cache starts no native process. Missing or stale assets regenerate;
batching does not bypass invalidation. Conversion logs are summarized per batch
instead of logging every asset with a Unity stack trace.

Measured on the local `war_chapter8.s2m` with the real asset loaders and native
converter, using separate empty directories for the before/after runs:

| Building asset load | Before | After |
|---|---:|---:|
| Empty cache | 63.962 s / 165 processes | 1.899 s / 3 processes |
| Current cache | 0.078 s / 0 processes | 0.083 s / 0 processes |

Both versions return 243 supported buildings/bridge spans and the same five
reported issues. All 165 generated mesh caches are byte-for-byte identical.
These measurements exclude Unity texture upload, mesh construction, physics,
terrain and vegetation rendering; they are not full Play-mode timings.
`Main.RunTests` now logs total map load time and separate stage times for map
decoding, building assets, terrain textures/geometry/materials, building rendering,
water/diagnostics, and vegetation assets/rendering.

Regression checks also cover mixed valid/invalid/missing assets, path validation,
case-insensitive deduplication, paths with spaces/Unicode, warm cache reuse,
stale-cache regeneration and temporary-file cleanup.

## Verified `tree_1.gr2` result

The converter currently extracts:

- one mesh named `tree_1`;
- 545 vertices in position/normal/UV format;
- 1,083 indices (361 triangles);
- three material groups containing 6, 274, and 81 triangles;
- material texture references resolving to `Tree009_mask.dds`, `branch_3.dds`, and
  `bark_4.dds` beside the GR2 file.

The source bounds show that these landscape assets are Z-up and approximately use
centimetres. Rendering therefore defaults to Z-up-to-Unity-Y-up conversion and a `0.01`
unit scale. Winding is reversed during that axis conversion.

The referenced tree textures use DXT3 DDS compression. Unity has no general runtime DDS
file importer, so OpenSH2 includes a small DXT1/DXT3/DXT5 top-mip decoder and creates
runtime RGBA textures. Generated foliage materials enable alpha testing and
double-sided rendering; opaque groups retain normal opaque rendering.

Tree material groups must not all use the same treatment. For example, `tree_2.gr2`
has three alpha-tested leaf groups (`17`, `18`, and `19`) and one 208-triangle
`bark.bmp` group. Its installed `bark.dds` alpha channel is not an opacity mask, so
OpenSH2 renders it as opaque. Foliage uses the included `OpenSH2/Stronghold 2 Foliage`
shader: unlit, double-sided, and alpha-tested at the original engine's `136 / 255`
reference. This avoids the incorrect lighting difference between the two sides of a
leaf card caused by using URP/Lit with culling disabled.

## Current limits

- Static mesh geometry, material groups, and one texture reference per material are
  supported.
- The converter currently uses the standard PNT332 layout. The original foliage shader
  also consumes a custom `COLOR0` channel; its exact Granny layout has not yet been
  identified, so the Unity foliage shader currently supplies neutral white vertex color.
- Skeletons, skin weights, animations, morph targets, multiple UV channels, and full
  Granny material graphs are not converted yet.
- The original exporter paths embedded in GR2 files point to Firefly build drives.
  OpenSH2 resolves their basenames against the GR2 file's installed directory.
- A future fully open-source reader can replace the adapter without changing the
  `.osh2mesh` reader or renderer.
