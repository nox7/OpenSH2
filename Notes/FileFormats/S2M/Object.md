All data payloads are represented by an "Object". All objects have basic metadata that needs parsing before parsing their specific payloads.

The type registry is **local to one decompressed segment**. Clear it before parsing MapHeader, RadarMap, or S2Game. Registration index 1 means `MapHeader`, `RadarMap`, and `S2Game`, respectively, in the three segments of the tested campaign maps.

As you encounter objects, you should have a dictionary registry prepared to accept entries. After every object id will be a 4 byte number. If that number already exists in your registry then add that object into the registry. If it is a new number, then create a new entry in the registry and the file will tell you what type of object it is after the registration index.

For example:
```csharp
Dictionary<int, SH2Object> SH2Objects = []; // Serialized object Id to parsed object
Dictionary<int, string> SH2Types = []; // Segment-local registration index to type name
```

Objects are read as
- +4 bytes = Object Id
- +4 bytes = Object registration index

If the object registration index **does not exist** in your dictionary as a key, then the next bytes will be the following (to tell you what type of object it is):
- +4 bytes = (LengthOfObjectTypeName)
- +(LengthOfObjectTypeName) bytes = name of object type
- +4 bytes = Object Id of parent object type (it may be the same object type Id or may just be 00s)
	- If this is the same as the Object Id we just parsed or one we've encountered at all before, then this is the end of this object inheritance tree. Begin parsing the actual object type. 
	- If this is **different** than the Object Id we just parsed **AND** one we've **never encountered before**, then register another object with the registration index of this parent object Id (the 4 bytes you just now read). Now, parse the next bytes:
		- +4 bytes = (LengthOfParentObjectTypeName)
		- +(LengthOfParentObjectTypeName) bytes = Object parent type name
	- **Note** The parent object id may be "00 00 00 00" which implies no parent object Id.

Now, begin parsing the specific object payload

The payload ends with `AF 1E FF FF`. An undecoded reader can retain every byte before that marker as a raw payload, allowing parsing to continue at the next object. The complete segment ends with `AD DE FF FF` in place of another object Id.
