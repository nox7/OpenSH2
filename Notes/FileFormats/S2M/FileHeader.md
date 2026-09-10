# File header

The uncompressed file header is two option tables. Options are not all mandatory; for example, `war_chapter1.s2m` has no `mapsize` entry while `war_chapter8.s2m` does. Readers must use the counts and names rather than assuming a fixed field order.

## String option table

- +4 bytes: number of string options (`int32`)
- Repeat that many times:
  - +4 bytes: ASCII key byte length
  - +length bytes: ASCII key
  - +4 bytes: UTF-16LE value character count
  - +(count × 2) bytes: UTF-16LE value

Known keys are `author` and `type`. Known `type` values include `warcampaign`, `peacecampaign`, `kingmaker`, and `freebuild`.

The first value was previously described as an "author presence flag." It is actually the string-option count. A value of 1 happens to omit `author`; a value of 2 commonly includes both `author` and `type`.

## Integer option table

- +4 bytes: number of integer options (`int32`)
- Repeat that many times:
  - +4 bytes: ASCII key byte length
  - +length bytes: ASCII key
  - +4 bytes: value (`int32`)

Known keys are `balanced`, `lastsave`, `mapsize`, `maxplayers`, and `version`. Treat unknown keys as valid options and preserve them. `mapsize` is optional and can be inferred from a square terrain plane when absent.
