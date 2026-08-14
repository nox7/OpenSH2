using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class CustomTriggerWilliamReachKeepReader : TriggerReader
  {
    public CustomTriggerWilliamReachKeepReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      CustomTriggerWilliamReachKeep obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, true);
      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
