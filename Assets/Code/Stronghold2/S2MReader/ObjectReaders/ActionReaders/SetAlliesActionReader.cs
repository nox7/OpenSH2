using Assets.Code.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class SetAlliesActionReader : ObjectReader
  {
    public SetAlliesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SetAlliesAction obj = new();

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
      reader.ReadInt32();
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

      // Read 4 object-terminator bytes (AF1EFFFF)
      var terminator = reader.ReadInt32();
      if (terminator != S2MReaderUtils.TrailerMarker)
      {
        throw new InvalidDataException($"Invalid object terminator. Got {terminator:X8}");
      }

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
