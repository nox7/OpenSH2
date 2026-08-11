using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class ActionReader : ObjectReader
  {
    public ActionReader(S2Object obj) : base(obj)
    {

    }

    protected void ReadActionHeader(BinaryReader reader)
    {
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadByte();

      // Data payload marker
      var payloadMarker = reader.ReadInt32();
      if (payloadMarker != S2MReaderUtils.DataPayloadMarker)
      {
        throw new InvalidDataException($"Expected data payload marker {S2MReaderUtils.DataPayloadMarker}, but got {payloadMarker}");
      }

      reader.ReadInt32();
    }
  }
}
