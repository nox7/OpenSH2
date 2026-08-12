using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class StopInvasionsActionReader : ActionReader
  {
    public StopInvasionsActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      StopInvasionsAction obj = new();

      ReadActionHeader(reader);

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.Lord = reader.ReadInt32() switch
      {
        0 => S2MLords.AllLords,
        1 => S2MLords.NoLord, // Has no effect
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

      obj.InvasionType = reader.ReadByte();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
