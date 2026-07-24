using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class SpecificEnemyLordDiesTriggerReader : ObjectReader
  {
    public SpecificEnemyLordDiesTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SpecificEnemyLordDiesTrigger obj = new();

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

      reader.ReadInt32();
      reader.ReadInt32();

      // Lord
      int lordId = reader.ReadInt32();

      if (lordId == 2)
      {
        obj.Lord = Code.Enums.Lord.Olaf;
      }
      else if (lordId == 3)
      {
        obj.Lord = Code.Enums.Lord.LordBarclay;
      }
      else if (lordId == 4)
      {
        obj.Lord = Code.Enums.Lord.TheHawk;
      }
      else if (lordId == 5)
      {
        obj.Lord = Code.Enums.Lord.TheBull;
      }
      else if (lordId == 6)
      {
        obj.Lord = Code.Enums.Lord.LadySeren;
      }
      else if (lordId == 7)
      {
        obj.Lord = Code.Enums.Lord.Edwin;
      }
      else if (lordId == 8)
      {
        obj.Lord = Code.Enums.Lord.TheKing;
      }
      else if (lordId == 9)
      {
        obj.Lord = Code.Enums.Lord.SirWilliam;
      }
      else if (lordId == 10)
      {
        obj.Lord = Code.Enums.Lord.SirGrey;
      }

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
