# AI castle blueprints (.aic)

Reader: `Assets/Code/Stronghold2/Castles/AicCastleReader.cs`.
Models are immutable and contain native coordinates/data, not Unity objects.
These installed files use an unversioned, little-endian layout with no magic header.

| Section | Bytes |
| --- | ---: |
| Build item count N | 4 |
| Ordered build entries, two uint32 words each | 8 × N |
| General values (semantics retained as unknown) | 50 × 4 |
| Additional unknown value | 4 |
| 24 point lists, 10 encoded points each | 240 × 4 |
| 10 point groups, 20 encoded points each | 200 × 4 |
| One int32 flag value for each list point | 240 × 4 |
| Optional preserved extension bytes | Remaining bytes |

Required length is **2,928 + 8 × N bytes**. The first build-entry word holds
kind, local X, local Z and rotation code in byte order from low to high. The
second holds a 16-bit group, one parameter byte and one preserved high byte.
Unknown kinds, rotation codes and bits are not discarded. Native keep kinds are
20 wooden, 21 small, 22 medium and 23 large. All keep entries remain available;
`PrimaryKeep` returns the last keep entry, matching the reference keep-marker scan.

Points retain their signed int32 encoding. Zero denotes an unused point; the
usual coordinate encoding is `Z * 100 + X`. List flags and general values are
retained without assigning unverified behavior. Build ordering and group values
are future builder inputs, not executed commands in the reader.

The reader validates the complete required length before allocation, caps item
counts at 262,144 and files at 16 MiB, and preserves trailing bytes in longer
files. Of the 47 installed files, 43 contain the complete layout; four shipped
test files are truncated and are reported individually without aborting map load.
`test2.aic` is accepted with its extension bytes preserved.

Verified example: `AIVChap6_Olaf.aic` has 90 entries. Its first two words at
file offset 4 are `0x00283014`, `0x00000001`: wooden keep at local (48,40),
rotation 0, group 1. `AIVChap6_Edwin.aic` begins with a small keep.

## Map-scoped storage and assignment

`S2MAiRuntime.CastleBlueprints` owns an `S2MCastleBlueprints` library. It reads the
castle directory once during map loading. Filename lookup is case-insensitive,
and all kingdoms share the same parsed object for a given file. No simulation
tick or ownership update reads from disk. Unloading the AI runtime unsubscribes
the library and clears its blueprint and kingdom indices.

- `Loaded[key]`: parsed design by filename, including designs not currently used.
- `Kingdoms[estateId]`: lord identity, read-only candidates, optional selected
  design, optional rotation, assignment origin and unresolved-assignment issue.
- `Files`: original filename/path/size inventory, also retained through the older
  `S2MAiRuntime.Blueprints` property for compatibility.
- `Issues`: nonfatal missing-folder, unreadable-file and invalid-file diagnostics.
- `TrySelect(estateId, key, rotation)`: chooses an already loaded candidate for
  future placement/strategy code and updates `AiTerritory.BlueprintKey`.

Only autonomous kingdom territories receive assignment records. Human kingdoms
and village estates do not. Both active castle markers and existing keeps feed
the existing AI territory classification; a kingdom need not already have a keep.
Ownership changes rebuild the affected record, remove human-controlled entries
and reuse the cache. Unknown owners remain unresolved instead of borrowing
another lord's identity.

`CastleBlueprintSettings` on Main supplies customizable per-lord filename sets
and per-kingdom overrides (optionally scoped by map filename). Settings are copied
at map load. Lord IDs use the ownership adapter's scenario-lord convention, not
saved player numbers, estate numbers or color indices. Default sets correspond
to the installed lord design sets: Olaf's wooden family; Barclay, Hawk, Bull,
Seren, Edwin, King, William and Grey's named families. Uninstalled family members
are simply absent from candidates.

Known campaign assignments take priority over generic lord candidates:

| Chapter | Estate | Lord | Design | Quarter-turn rotation |
| --- | ---: | --- | --- | ---: |
| 4 | 11 | Bull | AIVChap4_Bull.aic | 0 |
| 4 | 12 | Grey | AIVChap4_SirGrey.aic | 0 |
| 5 | 1 | Olaf | AIVChap5_Olaf.aic | 1 |
| 6 | 2 | Olaf | AIVChap6_Olaf.aic | 0 |
| 6 | 3 | Edwin | AIVChap6_Edwin.aic | 0 |

These assignments come from the reference campaign setup, not strings in the
S2M. Automatic chapter recognition requires the canonical installed map path;
a custom map named `war_chapter6.s2m` elsewhere is not treated as the campaign.
An explicit one-based `warChapter` argument is available on runtime/library
creation for a future launch context. Configured kingdom overrides have highest
priority. Missing explicit designs produce an issue, not a silent substitute.

Where the campaign does not prescribe a design, all available lord candidates
are retained and `Selected` stays null. Choosing one needs future fit/rotation
and strategy logic; no arbitrary filename is published as the original game's
choice. A configured override or `TrySelect` can explicitly choose one now.

This is a data-loading step. It does not replace authored buildings, change
startup keep placement, activate blueprint construction or bypass map AI-building
permissions. Native placement, slot matching and construction progress are
separate future consumers of these immutable models.

Validation: `Notes/tools/AicBlueprintValidation.cs` exercises native and synthetic
files, malformed input, immutable fields, ownership changes, sharing, overrides,
selection and unload. `AicBlueprintUnityValidation.cs` loads all 12 installed war
maps, verifies every AI kingdom has candidates, confirms campaign assignments and
checks that loading blueprint data does not alter keeps or building placements.
