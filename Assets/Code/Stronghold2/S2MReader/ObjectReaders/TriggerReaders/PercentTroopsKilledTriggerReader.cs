using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class PercentTroopsKilledTriggerReader : TriggerReader
  {
    public PercentTroopsKilledTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      PercentTroopsKilledTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Lord = reader.ReadInt32() switch
      {
        -1 => S2MLords.AllLords,
        0 => S2MLords.Player,
        1 => S2MLords.Olaf, // 1 is unused in this trigger. Default to Olaf.
        2 => S2MLords.Olaf,
        3 => S2MLords.LordBarclay,
        4 => S2MLords.TheHawk,
        5 => S2MLords.TheBull,
        6 => S2MLords.LadySeren,
        7 => S2MLords.Edwin,
        8 => S2MLords.TheKing,
        9 => S2MLords.SirWilliam,
        10 => S2MLords.SirGrey,
        _ => S2MLords.Olaf
      };

      obj.Percentage = reader.ReadInt32();

      reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
