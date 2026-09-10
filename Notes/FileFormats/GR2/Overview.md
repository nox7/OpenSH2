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

1. `Granny2AssetConverter.LoadOrConvert` validates the installation, updates a cached
   `.osh2mesh` file when necessary, and loads `Granny2ModelData`.
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
runtime RGBA textures. Generated materials enable alpha testing and default to
double-sided rendering for foliage cards.

## Current limits

- Static mesh geometry, material groups, and one texture reference per material are
  supported.
- Skeletons, skin weights, animations, morph targets, multiple UV channels, and full
  Granny material graphs are not converted yet.
- The original exporter paths embedded in GR2 files point to Firefly build drives.
  OpenSH2 resolves their basenames against the GR2 file's installed directory.
- A future fully open-source reader can replace the adapter without changing the
  `.osh2mesh` reader or renderer.
