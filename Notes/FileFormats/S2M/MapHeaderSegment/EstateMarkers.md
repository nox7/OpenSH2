Estate startup markers, decoded by `EstateMarkersReader`.

After the object header, all fields are little-endian:

| Bytes | Meaning |
| --- | --- |
| 4 | Counted field byte length: 388 in the tested maps |
| 4 | Slot count: 32 |
| 12 × count | Marker records, indexed by estate ID (including slot zero) |
| 4 | Object trailer, read separately |

Each 12-byte record contains:

| Offset | Type | Meaning |
| --- | --- | --- |
| 0 | int32 | Packed location, retained without interpreting its packing |
| 4 | int16 | X in map cells |
| 6 | int16 | Z in map cells (historically named Y in the reader) |
| 8 | int32 | Kind: 0 inactive, 1 village/estate, 2 castle/kingdom |

Coordinates alone do not activate a marker. In installed `war_chapter1.s2m`,
slot 1 is a castle marker at (151,29), and slot 2 has coordinates (231,211)
but kind 0. Barclay's large keep is already placed in estate 1.

Installed `war_chapter8.s2m` has castle markers in estates 1, 2, 3, 10 and 11;
all five already have keeps. Village markers occupy estates 4, 5, 6, 7, 8 and 9.
Estate 8 belongs to Sir Grey; the other five village estates are unowned.

## Runtime startup

`S2MStartupBuildings.Apply` runs after decoding and before loading models,
terrain overlays, navigation, storage and labor. It does not change the S2M file.

- Active castle markers generate a keep only if their estate has none. The
  default medium keep origin is marker (-2,+1), matching the entrance-marker
  relationship visible in Chapter 8. Nearby sites and four turns are searched
  when the preferred footprint is unavailable. Generated keeps also receive the
  existing campfire/population behavior.
- Village markers receive any missing flagpole and stockpile. The marker is the
  preferred flagpole origin; it is not a stockpile marker. The preferred stockpile
  origin is marker (-4,0), followed by a bounded closest-site search in the same
  estate. Granaries are left as authored; future AI construction handles missing ones.
- Native placement margins reserve one tile on all sides of a flagpole, and one
  tile at the front/back of a stockpile. These placement aprons cannot overlap.
  All village poles are placed before stockpiles. Aprons remain walkable;
  they are not navigation obstacles or agent clearance. Kingdom stores stay as authored.

### Remaining original-game discrepancy

`war-chapter8-after-sim.sh2` uses the same three zlib/object streams as S2M.
`Notes/tools/probe_startup_layout.py` exports its added buildings to
`war_chapter8-startup-reference.json`. The source file is never changed.

Sir Grey's native stockpile is at (209,164), yaw 0, footprint 7×5; the pole is at
(220,158), yaw 0, footprint 1×1. The marker remains at (213,164). Thus the marker
is a preferred anchor, not the pole's guaranteed final position. The mast BDG
is 1×1; the reference placement border is one tile on each side (3×3 total).

The saved pole's building slot/reference serial are 57/375, versus 86/382 for
the stockpile. Poles also precede most new stockpiles in the object stream.
This supports creation of the pole before the stockpile, but does not establish
when future building sites were reserved. A stockpile-first trial puts Grey's
stockpile inside, but still permits an interior pole and displaces the five other
poles from the markers where the original places them. It was reverted.

Additional trials with square-ring scan ordering, ground-actor occupancy and an
extra gathering margin did not explain the native pole site; these were also
reverted. Both the initial and saved DesirabilityLayer grids are entirely zero.
The initial Radar and world EstateLayer agree at the tested sites. The inspected
interior candidate cells have no saved buildings or blocking LogicLayer flags.
The missing troop-proximity check was subsequently isolated using the user's
five controlled saves. See [EstateStartupTroopPlacement.md](EstateStartupTroopPlacement.md):
a six-cell horizontal exclusion circle with scan centre (candidate X+2,Z+2)
reproduces every pole position and the original Chapter 8 displacement. This
corrects the earlier unresolved diagnosis. Runtime placement now applies this
check to every on-map actor with decoded Soldier inheritance, regardless of
allegiance or navigation layer. The user independently confirmed that friendly
and allied troops have the same effect. Lords, civilian workers and animals
without Soldier inheritance do not enter this troop mask.

Pole searches retain the measured clockwise square-ring ordering. Stockpile
and keep candidates prefer squared distance, then clockwise North, East, South,
West for ties (North = negative map Z; East = positive X). Diagonal ties follow
NE, SE, SW, NW. This explicit requested policy replaces the unverified native
stockpile tie-breaking detail. The default six-cell troop radius is adjustable
in Main's Startup Building Settings; zero disables the troop check.

The troop mask is rasterized once per startup and then queried in constant time.
Search-offset lists are reused per radius and search type within that startup.
No allegiance-dependent materials, navigation obstacles, or per-troop scene
objects are needed for this check.

### Empty-cell height audit (Sir Grey)

The initial S2M contains a non-rendered raised strip in the courtyard:

- Map tag 14 at X=211, Z=159..166 has four heights of 3328, shape 0,
  flags 0 and owner byte 8. These cells do not request a rendered wall shape.
- HeightLayer corner plane 1 independently contains four heights of 3328
  at X=211, Z=161..166. It does not reproduce the first two tag-14 cells.
- Landscape ground, primary HeightLayer and corner plane 0 are zero at these
  samples. Plane 2 is also zero there.
- The same tag-14 strip remains in the provided native startup save.

This is consistent with retained elevated-surface data from an earlier structure
or wall layout, but that origin is a hypothesis. The 3328 value is also a common
stone-wall surface height. It is not evidence of a hidden building by itself.
In particular, the native pole's final cell (220,158) also has four tag-14 heights
of 3328, shape 0, flags 0 and owner 8. A rule rejecting every raised non-wall
record would reject a site the original actually accepts.

The user's editor observation was subsequently qualified: wall placement is also
refused near non-player troops. Therefore that experiment does not isolate the
height records as the cause. An enemy-proximity placement check and a hidden
surface/clearance check remain separate hypotheses; neither justifies changing
ground navigation or treating all empty courtyard cells as solid obstacles.

### Runtime integration

- Placement rejects occupied footprints, water, walls, terrain cutouts, invalid
  walking surfaces and excessive slope. Failures are reported through
  `StartupPlacementIssues`, rather than overlapping existing buildings.
- `StartupBuildingSettings` on Main controls startup, keep size, offsets, search
  radii and permitted ground height variation. Placement runs once per loaded map.
- New buildings receive unique runtime IDs and explicit estate IDs. Their logical
  navigation footprints preserve the stockpile's central one-tile aisle.

The village descriptor is `meshes/buildings/flagpole_village_centre.bdg`;
`flag_pole_support.gr2` already includes the mast. Its cloth is the separate
`flag_keep.GR2`, placed at raw offset (544,512,512) with world yaw 180 degrees.
The neutral crest slice is a shared 2×2 black/white checker; owned poles use the
existing estate ownership and crest palette, including customization overrides.

Verification: `Notes/tools/StartupBuildingsValidation.cs` exercises both installed
maps, a controlled missing-keep fixture, repeated initialization, inventory,
navigation and rendered neutral/owned flags in an isolated Unity project.
