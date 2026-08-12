This layer contains information about the terrain elevation of the map. It has 13 planes of data. Plane 0 seems to have the most relevant height map data.

- +4 bytes = unknown
- +4 bytes = unknown
- +4 bytes = unknown
- +4 bytes = unknown (Possible size of all bytes in all planes combined?)
- +4 bytes = unknown
- +4 bytes = unknown
- +4 bytes = A5 C1 FF FF Data trailer flag
- +4 bytes = Object registration Id
- (For 256 map size only):
	- +4 bytes x 256 x 256 (262,144 bytes in total)
		- Parse every 4 bytes as a float value
		- Repeat this 13 times for a total of 13 layers