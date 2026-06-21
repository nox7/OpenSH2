Assumedly, this is when any players' troops reach the lord defined in the trigger.
- Parse [[Trigger]] first
- +4 bytes = unknown
- +4 bytes = unknown (04, seems to be the byte-size of the data that comes after this 4 byte sequence)
- +4 bytes = Lord
	- [[Lords2Enum]]
- +4 bytes = End object trailer marker. AF 1E FF FF