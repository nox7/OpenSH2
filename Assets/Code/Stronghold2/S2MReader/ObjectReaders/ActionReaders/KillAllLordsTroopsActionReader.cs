using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class KillAllLordsTroopsActionReader : ActionReader
  {
    public KillAllLordsTroopsActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      KillAllLordsTroopsAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Lord = reader.ReadInt32() switch
      {
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

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
