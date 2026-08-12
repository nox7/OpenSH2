All derivative action payloads will be parsed after this and after their potendataect payload marker (A5 C1 FF FF).

- First, parse an [Object](../../../Object.md).
- +4 bytes = Unknown
- +4 bytes = Unknown
- +1 byte = null padding
- +4 bytes = Data payload marker (A5 C1 FF FF)
- +4 bytes = Data payload index (can be ignored)

At this point, you must know which actions have data payloads, as that determines what you will parse next.

If the action is known to have data:
- +4 bytes = Unknown
- +4 bytes = Byte-length of the data payload until the object trailer marker
- Parse that specific object's byte data (found in the same directory as this file)
- +4 bytes = Object trailer market (AF1EFFFF)

If the action is known to have **no data**, then you will simply consume the object trailer marker
- +4 bytes = Object trailer market (AF1EFFFF)
