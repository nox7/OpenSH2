using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class WolfSpawnRateActionReader : ActionReader
  {
    public WolfSpawnRateActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      WolfSpawnRateAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Level = (IntensityLevel)reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
