using Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class LordDiesTriggerReader : TriggerReader
  {
    public LordDiesTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      LordDiesTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, true);
      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
