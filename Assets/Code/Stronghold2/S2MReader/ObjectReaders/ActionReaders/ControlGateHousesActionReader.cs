using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class ControlGateHousesActionReader : ActionReader
  {
    public ControlGateHousesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      ControlGateHousesAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Lord = reader.ReadInt32() switch
      {
        2 => Enums.S2MLords.Olaf,
        3 => Enums.S2MLords.LordBarclay,
        4 => Enums.S2MLords.TheHawk,
        5 => Enums.S2MLords.TheBull,
        6 => Enums.S2MLords.LadySeren,
        7 => Enums.S2MLords.Edwin,
        8 => Enums.S2MLords.TheKing,
        9 => Enums.S2MLords.SirWilliam,
        10 => Enums.S2MLords.SirGrey,
        _ => Enums.S2MLords.Olaf
      };

      obj.GatehouseOpen = reader.ReadBoolean();
      obj.GateHouseLocationFlagColor = (FlagColor)reader.ReadInt32();
      obj.GateHouseLocationFlagNumber = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
