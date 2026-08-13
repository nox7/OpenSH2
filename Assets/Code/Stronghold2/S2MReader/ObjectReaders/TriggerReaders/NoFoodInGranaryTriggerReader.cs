using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class NoFoodInGranaryTriggerReader : TriggerReader
  {
    public NoFoodInGranaryTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      NoFoodInGranaryTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.FlagColor = (FlagColor)reader.ReadInt32();
      obj.FlagNumber= reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
