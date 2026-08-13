using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class RescueLordTriggerReader : TriggerReader
  {
    public RescueLordTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      RescueLordTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Lord = reader.ReadInt32() switch
      {
        0 => S2MLords.Olaf,
        1 => S2MLords.LordBarclay,
        2 => S2MLords.TheHawk,
        3 => S2MLords.TheBull,
        4 => S2MLords.LadySeren,
        5 => S2MLords.Edwin,
        6 => S2MLords.TheKing,
        7 => S2MLords.SirWilliam,
        8 => S2MLords.SirGrey,
        _ => S2MLords.Olaf
      };

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
