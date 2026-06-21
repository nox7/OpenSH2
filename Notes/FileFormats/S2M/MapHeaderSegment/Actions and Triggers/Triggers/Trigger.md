All triggers start with their object Id, object registration type id, then 4 words (16 bytes), then a null byte, then the payload delimiter sequence, the object registration type id again, then the actual payload.
+ +4 bytes = unknown (00s observed)
+ +4 bytes = unknown (00s observed)
+ +4 bytes = unknown (01 observed)
+ +4 bytes = unknown (01 observed)
+ +1 byte = null byte
+ +4 byte = Trigger payload delimiter. Exactly: A5 C1 FF FF
+ +4 bytes = object registration Id
+ Continue parsing bytes according to the specific trigger type for that object's payload format until AF 1E FF FF byte sequence is hit. That will be the end of the trigger