# Iron deposits: saved terrain features and rock cover

Iron locations are stored in **Landscape tag 19**, as byte value **35 / 0x23**.
The reader already transposes this into the row-major
`WaterLayer.LandscapeFeatureValues` grid and crops it to playable bounds. It is
independent of tag 27's base terrain material. The reference checkout's map-art
metadata also identifies feature 35 as iron; no reference implementation is copied.

Installed `landtex.txt` lists GROUND1 textures starting at material 8. Its entry 35
is `iron_raw.BMP`; the runtime asset is `terrain/iron_raw.DDS`. OpenSH2 now derives
effective material 0x23 on iron cells, after ordinary slope/forest appearance,
while retaining explicit cliff topology. Source materials, features and heights
are not modified. The texture catalog and terrain shader append iron at array
slice 27 (28 slices total), preserving existing slice indices and edge blending.

## Rocks

There are twelve installed `meshes/landscape/rock_iron_1..12.gr2` models, using
`meshes/landscape/rock_iron.dds`. Serialized Rocks family 8 can also place these
models. However, chapter 8 contains 30 iron terrain cells and **no** family-8
placements, so loading the Rocks list alone does not supply its visible deposits.
Chapter 10 contains 25 iron cells and 16 explicitly saved family-8 placements.

The vegetation loader now fills iron cells without saved iron-rock anchors with
one deterministic model placement at cell center. Coordinate hashing chooses the
variant and quarter-turn without touching simulation randomness. This is an
independent procedural reconstruction; the original renderer's exact scatter and
variant sequence have not been recovered, so individual silhouettes are not
claimed to match the original pixel for pixel. Explicit saved rocks retain their
positions, variants and scaling.

Generated ore uses `SceneryKind.IronOre` (appended to preserve existing enum
values), samples ground height, and uses the same mesh-unit conversion and rock
calibration as saved rocks. It follows Game Scale, map flips and Rock Model Scale;
it does not inherit tree scaling or sway. Models and textures are loaded only
when needed and shared by asset. Opaque materials keep rock texture alpha from
being treated as a leaf cutout.

Both generated ore and saved family-8 rocks participate in reversible building
coverage, sharing the existing natural-stone visibility controller. Building
removal restores the models without deleting source data. The iron ground
texture remains part of the terrain.

## Validation

`Notes/tools/IronDepositValidation.cs` checks all twelve campaign feature/material
grids (317 iron cells total), actual chapter 8/10 model loading, stable placement,
deduplication with saved family-8 cells, texture-array slice assignment, installed
DDS loading, opaque shaders, and building coverage/removal. Ore-free chapter 5
does not load iron models. The test also captures a URP preview of chapter 8's
deposits; results live in ignored `Temp/IronDepositProbe`.
