using Assets.Code.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class SetAlliesActionReader : ActionReader
  {
    public SetAlliesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SetAlliesAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // Probably self, player? It's unused
      reader.ReadInt32();

      int olafSetting = reader.ReadInt32();
      int barclaySetting = reader.ReadInt32();
      int hawkSetting = reader.ReadInt32();
      int bullSetting = reader.ReadInt32();
      int serenSetting = reader.ReadInt32();
      int edwinSetting = reader.ReadInt32();
      int kingSetting = reader.ReadInt32();
      int sirWilliamSetting = reader.ReadInt32();
      int sirGreySetting = reader.ReadInt32();

      obj.Olaf = FromS2MFlag(olafSetting);
      obj.LordBarclay = FromS2MFlag(barclaySetting);
      obj.TheHawk = FromS2MFlag(hawkSetting);
      obj.TheBull = FromS2MFlag(bullSetting);
      obj.LadySeren = FromS2MFlag(serenSetting);
      obj.Edwin = FromS2MFlag(edwinSetting);
      obj.TheKing = FromS2MFlag(kingSetting);
      obj.SirWilliam = FromS2MFlag(sirWilliamSetting);
      obj.SirGrey = FromS2MFlag(sirGreySetting);

      ReadObjectTrailerMarker(reader);

      return obj;
    }

    private AllySetting FromS2MFlag(int flag) => flag switch
    {
      0 => AllySetting.Neutral,
      1 => AllySetting.Friend,
      2 => AllySetting.Enemy,
      _ => throw new InvalidDataException($"Invalid ally setting flag: {flag}"),
    };
  }
}
