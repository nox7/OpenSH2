This is read as "No enemy left on map" in the map editor. Assumedly, this means that the map editor will always set 0 on the boolean flag's 4-bytes below.

- +4 bytes = Boolean flag
	- 0 = "No enemy left on map" version
	- 1 = "Enemies are on map" version