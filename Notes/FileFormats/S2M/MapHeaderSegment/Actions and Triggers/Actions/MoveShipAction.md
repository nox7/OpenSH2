All flag colors go in order of
FF FF FF FF = OFF
0 = red
1 = green
2 = blue
3 = yellow

And flag numbers are 0-based. So 0 = 1.

Ship delays are in months. The timer starts when the ship reaches the flag, so even if the game is 50% through January and the delay is 1 month, the ship will not move until 50% through February.

- +4 bytes = Unknown
- +4 bytes = Unknown
- +4 bytes = Number of bytes in data
- +4 bytes = Spawn Flag color
- +4 bytes = Spawn Flag number
- +4 bytes = Spawn ship ship delay until moving to next destination
- +4 bytes = Destination 1, flag color
- +4 bytes = Destination 1, flag number
- +4 bytes = Destination 1 ship delay until moving to next destination
- +4 bytes = Destination 2, flag color
- +4 bytes = Destination 2, flag number
- +4 bytes = Destination 2 ship delay until moving to next destination
- +4 bytes = Destination 3, flag color
- +4 bytes = Destination 3, flag number
- +4 bytes = Destination 3 ship delay until moving to next destination
- +4 bytes = Destination 4, flag color
- +4 bytes = Destination 4, flag number
- +4 bytes = Unknown/Unused
- +1 byte = Ship type
	- 0 = Viking ship
	- 1 = Trade ship
- +1 byte = Leave or turn to wreck
	- 0 = Leave map
	- 1 = Turn to wreck
- +4 bytes = object trailer marker