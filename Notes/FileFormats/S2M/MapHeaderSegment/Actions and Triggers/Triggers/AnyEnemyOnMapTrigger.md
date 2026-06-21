This is read as "No enemy left on map" in the map editor. Assumedly, this means that the map editor will always set 0 on the boolean flag 4 bytes below.
- Parse [[Trigger]] first
- +4 bytes = unknown
- +4 bytes = unknown
- +4 bytes = Boolean flag
	- 0 = "No enemy left on map" version
	- 1 = "Enemies are on map" version
- +4 bytes = End object trailer marker. AF 1E FF FF