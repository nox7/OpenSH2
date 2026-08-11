using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class AITroopsRetreatActionReader : ActionReader
  {
    public AITroopsRetreatActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      AITroopsRetreatAction obj = new();

      ReadActionHeader(reader);

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.RetreatPointFlagColor = (FlagColor)reader.ReadInt32();
      obj.RetreatPointFlagNumber = reader.ReadInt32();

      int lordId = reader.ReadInt32();

      if (lordId == 2)
      {
        obj.Lord = Enums.S2MLords.Olaf;
      }
      else if (lordId == 3)
      {
        obj.Lord = Enums.S2MLords.LordBarclay;
      }
      else if (lordId == 4)
      {
        obj.Lord = Enums.S2MLords.TheHawk;
      }
      else if (lordId == 5)
      {
        obj.Lord = Enums.S2MLords.TheBull;
      }
      else if (lordId == 6)
      {
        obj.Lord = Enums.S2MLords.LadySeren;
      }
      else if (lordId == 7)
      {
        obj.Lord = Enums.S2MLords.Edwin;
      }
      else if (lordId == 8)
      {
        obj.Lord = Enums.S2MLords.TheKing;
      }
      else if (lordId == 9)
      {
        obj.Lord = Enums.S2MLords.SirWilliam;
      }
      else if (lordId == 10)
      {
        obj.Lord = Enums.S2MLords.SirGrey;
      }

      reader.ReadInt32();
      reader.ReadInt32();
      obj.WillLeaveMap = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
