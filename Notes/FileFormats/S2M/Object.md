All data payloads are represented by an "Object". All objects have basic metadata that needs parsing before parsing their specific payloads. 

As you encounter objects, you should have a dictionary registry prepared to accept entries. After every object id will be a 4 byte number. If that number already exists in your registry then add that object into the registry. If it is a new number, then create a new entry in the registry and the file will tell you what type of object it is after the registration index.

For example:
```csharp
Dictionary<int, SH2Object> SH2Objects = []; // Object index to actual object
Dictionary<int, string> SH2Types = []; // Object index to type name. E.g. 02 = "EstateMarkers" or 03 = "Scenario"

// Keys in both dictionaries should map to the same object. 02 in SH2Objects should return an object that matches the same string type of 02 in SH2Types. If 02 is an EstateMarkers object then 02 in SH2Types should give "EstateMarkers"
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