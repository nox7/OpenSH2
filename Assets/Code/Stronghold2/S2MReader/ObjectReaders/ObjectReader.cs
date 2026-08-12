using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ObjectReader
  {
    protected readonly S2Object Object;

    public ObjectReader(S2Object obj)
    {
      Object = obj;
    }

    protected void ReadDataPayloadMarker(BinaryReader reader, bool isEmpty)
    {
      // Data payload marker
      var payloadMarker = reader.ReadInt32();
      if (payloadMarker != S2MReaderUtils.DataPayloadMarker)
      {
        throw new InvalidDataException($"Expected data payload marker {S2MReaderUtils.DataPayloadMarker}, but got {payloadMarker}");
      }

      reader.ReadInt32();

      if (!isEmpty)
      {
        reader.ReadInt32();
        reader.ReadInt32(); // This is the length of bytes in the data payload, but we ignore it
      }
    }

    public void ReadObjectTrailerMarker(BinaryReader reader)
    {
      // Read 4 object-terminator bytes (AF1EFFFF)
      var terminator = reader.ReadInt32();
      if (terminator != S2MReaderUtils.TrailerMarker)
      {
        throw new InvalidDataException($"Invalid object terminator. Got {terminator:X8}");
      }
    }

    public virtual S2Object Read(BinaryReader reader)
    {
      throw new NotImplementedException();
    }
  }
}
