All derivative action payloads will assumedly be parsed after this and after their potential object payload marker (A5 C1 FF FF) if they have one.

Start parsing actions by parsing this object first, then the potential object payload data for that specific action type.

- +4 bytes = unknown (01 00 00 00 observed) 
- +1 null byte
- If next 4 bytes is A5 C1 FF FF, then begin parsing object payload as outlined by that specific action type
	- If the action has no payload data, then read a single 4 byte throwaway data, then read an object trailer marker.
- Otherwise, next 4 bytes may just be the object-trailer marker AF 1E FF FF and this action is over and has no data.
