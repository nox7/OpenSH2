using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ObjectReader
  {
    protected readonly S2Object Object;

    public ObjectReader(S2Object obj)
    {
      Object = obj;
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
