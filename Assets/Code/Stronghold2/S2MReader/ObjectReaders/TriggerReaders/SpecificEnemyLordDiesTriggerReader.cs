using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class SpecificEnemyLordDiesTriggerReader : TriggerReader
  {
    public SpecificEnemyLordDiesTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SpecificEnemyLordDiesTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

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

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
