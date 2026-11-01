# Estate startup placement: controlled troop experiments

Inputs supplied by the user: `estate-spawn-test.s2m` and the five incremental
`estate-spawn-test-sim*.sh2` saves. The map is the final editor state; each save
provides that experiment's actual troop population. Using all final-map troops
for every earlier experiment would invalidate the comparison.

## Results

Estate 8's marker stays at **(234,103)** throughout. All five saved Map tag-14
wall/surface grids have exactly the same SHA-256 hash; their saved desirability
grids are all zero. The troop populations change as follows:

| Save suffix | Newly present troops | Original flagpole | Original stockpile |
| --- | --- | --- | --- |
| sim | Archer (238.5,104.5) | (231,100) | (230,103) |
| sim-2 | Archer (227.5,103.5) | (231,106) | (230,99) |
| sim-3 | Archer (232.5,110.5) | (231,99) | (230,103) |
| sim-4 | Archers (228.5,96.5), (230.5,96.5) | (237,109) | (230,102) |
| sim-final | Spearman (233.5,111.5) | (241,96) | (230,103) |

All these soldiers stand at raw Y=2048, navigation layer 1. The Lord at
(229.5,101.5) is present throughout and is not included in the qualifying combat
troop set. Treating him identically to these soldiers would not fit the results.

## Flagpole rule that reproduces every result

1. Try the estate marker, then increasing square rings, starting each ring at
   its negative-X/negative-Z corner and proceeding clockwise.
2. Require the pole's one-cell footprint plus its one-cell border to be free:
   a 3×3 placement rectangle. Check estate ownership, static buildings and walls.
3. Apply the construction troop-proximity check. For candidate origin (x,z),
   the tested point is **(x+2,z+2)** and the exclusion radius is **6 map cells**.
   Reject when a qualifying soldier's horizontal squared distance is **<36**.
4. Accept the first candidate passing both geometry and troop checks.

The offset is significant: it is not the visible mast centre (x+.5,z+.5).
It matches the reference construction query's use of the 3×3 clearance
dimensions to offset its scan centre. This is factual parameter research;
the audit implementation is independent and imports no reference source.

For example, sim's pole origin (231,100) gives scan centre (233,102). The archer
added in sim-2 at (227.5,103.5) is at squared distance 32.5, so that former site
is rejected. The first remaining acceptable site becomes (231,106).
Subsequent added troops reject the previous site's scan point at squared
distances 6.5, 26.5 and 30.5 respectively, all below 36.

The check projects wall troops onto the X/Z plane. Navigation layer and elevation
do not shield construction underneath/nearby. Including the soldiers' two-cell
height in the distance produces a different sim-2 result, so a 3D-distance rule
does not fit these fixtures.

The same flag model independently predicts **(220,158)** in the original
`war_chapter8.s2m`, matching `war-chapter8-after-sim.sh2`. No special cases for
Grey, either map, or those coordinates are needed. Hidden surface heights are
not needed to explain these placement changes.

## Stockpile follows the pole

All seven village poles precede the new stockpiles in these controlled saves.
Reserve the accepted pole's 3×3 area, then seek a stockpile near marker (-4,0).
Its footprint is 7×5, with a tile at the front and back, for a 7×7 placement area.

A closest-feasible-site search reproduces all five stockpile positions, and also
the original Chapter 8 stockpile at **(209,164)**. In sim-2 the pole at (231,106)
overlaps the preferred stockpile placement area; moving the stockpile to
(230,99) clears it. In sim-4 a one-cell shift to (230,102) clears the pole at
(237,109). The other three accept the preferred (230,103) directly.

This stockpile model does **not** apply the pole's troop-proximity rejection.
Applying a general larger-building enemy circle would reject stockpiles the
original accepts. The evidence supports a different startup construction path
for stockpiles, rather than applying one proximity rule to all building types.
Exact stockpile tie-breaking outside these fixtures remains unproven.

## Limits and reproducibility

The reference calls this a construction enemies-near rule, but the user later
tested friendly and allied troops and confirmed the same placement effect.
The runtime therefore applies the measured geometry regardless of allegiance.
It classifies troops through decoded Soldier inheritance rather than colours,
model names, or navigation layer, and retains the Lord/civilian distinction.

Runtime stockpile ties use the user-requested North, East, South, West policy
(North = negative Z). The pole's measured square-ring scan is preserved. This
implementation is checked by `Notes/tools/StartupPlacementRulesValidation.cs`
against all five original saves and the Chapter 8 coordinates, along with
allegiance independence, exclusion boundaries, inactive actors and 8,000 troops.

`Notes/tools/audit_startup_troops.py` reads original binaries and exports
`estate-spawn-troop-audit.json`, including troops, exact predictions, previous-site
rejectors, wall hashes and counterfactuals without the troop check/with 3D distance.
It exits unsuccessfully if any predicted pole or stockpile differs from a save.
It is an offline research tool, not a runtime map override. No original map/save
or runtime placement code was changed for this investigation.
