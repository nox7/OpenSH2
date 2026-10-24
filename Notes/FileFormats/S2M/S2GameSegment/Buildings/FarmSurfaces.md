# Building-owned farm ground surfaces

## Saved selectors and installed textures

Landscape tag 19 contains cultivated-ground selectors as well as the previously
identified water, forest and structure categories:

| Saved feature | Effective surface | Installed asset | Sampled owners |
|---|---|---|---|
| `0x25` (37) | Tilled, striped soil | `terrain/crop_1.dds` | WheatFarm, HopFarm |
| `0x26` (38) | Earth with patchy grass | `terrain/crop_2.dds` | PigFarm, DairyFarm, AppleFarm |

These IDs also match the respective GROUND1 positions in `landtex.txt` (zero-based
indices 29/30 plus 8). Visual inspection of the installed DDS files identifies
`crop_1` as the furrowed texture in the original-game wheat-farm screenshot. The
ordinary tag-27 ground IDs remain varied beneath these cells; the cultivated
appearance is an overlay, not a replacement of the saved base terrain grid.
HeightLayer does not supply these texture selectors.

All 65 local maps were inspected. Their cultivated markers form 283 separate
four-connected components, each intersecting exactly one farm's recorded footprint:

| Farm type | Patches | Saved cells per patch |
|---|---:|---:|
| WheatFarm | 48 | 16 |
| HopFarm | 7 | 15 |
| AppleFarm | 168 | 13 |
| DairyFarm | 55 | 18 |
| PigFarm | 5 | Four patches of 17, one of 18 |

Total coverage is 4,133 cells. The two SheepFarm placements have no cultivated
markers and receive no invented soil patch. Plot dimensions and shapes are read
from the markers rather than hardcoded from these sampled sizes.

## A chapter-8 wheat plot

WheatFarm 216263800 is anchored at (50,22), with a 2x1 building footprint. Its
16-cell tilled patch extends over X=49..51 and Z=17..22. The saved shape is:

```text
###
###
###
###
#..
###
```

The final row contains the shelter; the preceding row retains two unmarked
access cells. This explains why changing only the hut's placement footprint
would miss most of the wheat field. The 14 Wheat actors belonging to this farm
have reciprocal parent references matching its saved building reference; every
crop actor's cell is marked `0x25`. The other two cells are the shelter footprint.
All 42 chapter-8 Wheat actors and all 12 Hops actors match their respective farm
references and cultivated markers. Those crop actors are not rendered by this
change.

Chapter 8 contains three wheat farms (48 tilled cells), one hop farm (15 tilled
cells), eleven apple farms (143 mixed-earth cells), four dairy farms (72 cells),
and four pig farms (68 cells): 23 patches / 346 cells in total.

## Other appearance records

Landscape tag 20 has a 28-byte record pool. Several live rectangles match doubled
building coordinates and carry distinct selectors: wheat shelters use selector 1,
apple/hop shelters selector 3. A controlled before/after map adds two rectangles
for its apple farm and bakery without changing tag-27 base materials. The exact
rectangle/selector semantics are not sufficiently decoded to drive texture choice.
The implementation therefore uses the independently matching tag-19 plot masks
and installed GROUND1 crop textures, not a guessed tag-20 selector mapping.

Farm concrete payloads also contain crop references and candidate field positions;
their complete schemas remain incomplete. No height or generic reserved-footprint
replacement is needed to obtain the saved plot shapes.

## Implementation and ownership

`S2MBuildingSurfacePatchLoader` finds connected components of equal `0x25`/`0x26`
markers and associates them with original farm footprints. Only farms included
in successfully loaded building batches activate a patch. No nearest-building
heuristic, plot rectangle size, or fixed paint radius is used. Unsupported owners
retain their underlying terrain. Unowned components are ignored; a component
that touches multiple farms is reported and left unmodified rather than silently
assigning it to a neighbor. No such ambiguity occurs in the sampled corpus.

Each `S2MBuildingSurfacePatch` retains its owner object ID, material, orientation,
and exact cell indices. `S2MTerrainSurfaceOverrides` rasterizes a collection of
these patches into O(1) per-cell material/orientation lookups. The source material,
feature, orientation and height arrays remain untouched.

The building data supplies the same overrides to terrain asset loading and mesh
generation. `S2MTerrainSurfaceMaterialGrid.Create` clones ordinary materials,
applies normal slope/forest rules, then applies the active farm overlays. Explicit
cliff selectors retain their geometry. The terrain texture array appends slots
25/26 for `crop_1`/`crop_2`, preserving existing ground and cliff slots; the shader
maps effective IDs 37/38 to those appended slots. Existing neighboring-material
blending applies at the plot boundary.

Plot orientation follows its building's quarter-turn instead of inheriting the
random per-cell base rotations, keeping furrows continuous across the field.
Neighbor blend samples use the same derived orientations. All sampled farms have
yaw 0; synthetic checks also cover the other three quarter turns. Soil changes
materials only, so existing terrain cutouts, geometry and colliders are unaffected.

## Future removal

Destruction mechanics are not implemented. To remove a farm's appearance later,
rebuild the overrides from retained patches excluding that object's ID, then
regenerate the affected terrain appearance. For example:

```csharp
var remaining = oldOverrides.Patches.Where(p => p.ObjectId != removedBuildingId);
var newOverrides = new S2MTerrainSurfaceOverrides(mapWidth, mapHeight, remaining);
```

Using `Create(map, primaryHeightCells, newOverrides)` restores the ordinary
rendered materials for the removed patch, including existing slope/forest rules.
Source ownership must not be reinterpreted as a surviving neighboring farm's plot.
New farm placement will need to create its own patch when gameplay placement is
implemented; this loader reconstructs the patches already stored in S2M.

## Validation

All 65 maps passed asset loading and surface generation: 283 patches / 4,133 cells,
with unchanged building counts, model assets and reported unresolved buildings.
Both new DDS textures decode successfully. Chapter-8 checks cover all 346 material
cells, actual generated vertex material/orientation data, exact single-farm and
all-farm reversion, preserved source grids, unavailable farm assets, coherent
neighbor rotations, ambiguous ownership rejection and explicit cliff preservation.
The existing terrain-hole/bridge/keep regression checks pass. Unity C# compilation
passes; native shader appearance and plot edges still require in-editor visual
verification.
