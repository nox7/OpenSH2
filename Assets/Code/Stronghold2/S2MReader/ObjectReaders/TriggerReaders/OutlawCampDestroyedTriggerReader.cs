using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class OutlawCampDestroyedTriggerReader : TriggerReader
  {
    public OutlawCampDestroyedTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      OutlawCampDestroyedTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      reader.ReadInt32(); // Unknown, always 0

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
