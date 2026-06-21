This is read as "No enemy or invasions left on map" in the map editor, however it has a boolean flag byte that can be set - so we assume it can also be a "not" variant where it triggers with any enemy or invasions left on map.
- Parse [[Trigger]] first
- +4 bytes = unknown
- +4 bytes = unknown
- +4 bytes = Boolean flag
	- 0 = "No enemy or invasions left" version
	- 1 = "Any enemy or invasion left" version
- +4 bytes = End object trailer marker. AF 1E FF FF