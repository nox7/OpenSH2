using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class AllYourTroopsDeadTriggerReader : ObjectReader
  {
    public AllYourTroopsDeadTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      AllYourTroopsDeadTrigger obj = new();

      reader.ReadInt32();
      reader.ReadInt32();
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

      // Read 4 object-terminator bytes (AF1EFFFF)
      var terminator = reader.ReadInt32();
      if (terminator != S2MReaderUtils.TrailerMarker)
      {
        throw new InvalidDataException($"Invalid object terminator. Got {terminator:X8}");
      }

      return obj;
    }
  }
}
