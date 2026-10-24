# Composite buildings: kitchen/pantry and courthouse/dungeon

The installed game keeps separate objects and model sets for both halves. The kitchen half uses `kitchen.bdg` (`kitchen_closed.GR2`, `kitchen_open.GR2`, `kitchen_collision.GR2`); the pantry half is serialized as `ServantsQuarters` and uses `servants_quarters_pantry.bdg` (`servants_pantry.GR2`, `servants_pantry_open.GR2`, `servants_pantry_collision.GR2`).

A Kitchen reserves 7x4 cells before rotation. Its own visible model occupies 4x4, and its linked pantry occupies the adjacent 3x4 portion. The pantry Building record has a 1x1 logical footprint, which is not its model extent. The prior generic size check skipped this half. This particular larger kitchen footprint is explained by the combined building, rather than empty placement clearance.

## Saved associations

Associations are explicit saved references, not proximity matches or outer serialized object IDs. The counted 52-byte Building body has unaligned uint16 fields at offsets 19 and 21. A concrete reference matches `(uint16(body + 19) << 16) | uint16(body + 21)`. Their deeper index/generation semantics remain unverified; the observed word order and reciprocal matches are confirmed for these pairs.

| Concrete type | Counted concrete body size | Related reference offset in body |
|---|---:|---:|
| Kitchen | 66 | 62 |
| ServantsQuarters | 20 | 16 |
| CourtHouse | 26 | 18 |
| Dungeon | 8 or 12 | 4 |

The decoder checks the concrete type index, payload marker, count, body size and exact end position. It retains `SavedBuildingReference` and `LinkedBuildingReference`, then sets `LinkedBuildingId` only when both unique references resolve reciprocally to the expected type pair. Null, missing, one-sided, ambiguous and malformed associations do not authorize an attachment.

The ordinary loader permits the 1x1-versus-3x4 exception only for a verified kitchen/pantry pair. Each half renders once at its own serialized XYZ/yaw, with independent exterior/interior switching and collision. The kitchen concrete body also contains the pantry XYZ at offset 26; all four records agree with the associated pantry object. No guessed translation or generated duplicate pantry is needed.

Chapter 8 kitchen 221733464 has reference `0x00630096` and links to pantry 217294688 (`0x00780097`), which links back. Their respective saved positions are (195, 36), yaw 90, raw Y 34.84736 and (195, 32), yaw 90, raw Y 278.77887. The height difference is present in the source and is preserved.

## Confirmed kitchen pairs

| Map | Kitchen object ID | Pantry object ID | Kitchen yaw |
|---|---:|---:|---:|
| Peace Campaign.s2m | 357697656 | 573501744 | 0 |
| war_chapter8.s2m | 221733464 | 217294688 | 90 |
| war_chapter8.s2m | 861227168 | 217293288 | 180 |
| war_chapter8.s2m | 861225104 | 861267640 | 0 |

Five courthouse/dungeon pairs (one in chapter 1 and four in chapter 8) use the same saved-reference scheme. Those halves already render as separate objects; decoding the association confirms their relationship without adding duplicate geometry.

Six ServantsQuarters records have zero partner references: five across the peace campaign/Chiller map and one in chapter 8 (217296368). Their purpose is not established and they remain in the review list.

## Data preservation and validation

Original kitchen 7x4 and pantry 1x1 dimensions remain on `BuildingPlacement`, along with each saved transform and raw payload. The existing kitchen footprint already covers the combined ground area, so no terrain-mask or walking-height mutation is required. Physical pantry collision comes from its GR2 collision model, not from expanding the logical marker.

Checked all 65 maps: four reciprocal kitchen/pantry pairs and five courthouse/dungeon pairs. Every original placement field and raw payload is unchanged. All four pantries load their exterior/interior/collision assets through the batch converter; all referenced textures resolve and decode. Additional tests reject one-sided, duplicate and malformed associations and verify that the physical 3x4 pantry extent fits inside the combined 7x4 area at all four quarter turns. In-editor visual confirmation remains necessary.

Current asset totals: 1,282 ordinary placements, 203 ordinary GR2 assets, 22 reported issues across the corpus; chapter 8 loads 239 ordinary placements plus five keeps and two completed bridges (246 rendered objects/spans), with only its unlinked servants marker and deferred construction bridge reported.
