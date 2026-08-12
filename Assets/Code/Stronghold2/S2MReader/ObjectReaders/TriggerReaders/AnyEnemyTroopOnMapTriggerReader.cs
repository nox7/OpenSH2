using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class AnyEnemyTroopOnMapTriggerReader : TriggerReader
  {
    public AnyEnemyTroopOnMapTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      AnyEnemyTroopOnMapTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      int booleanFlag = reader.ReadInt32();
      obj.AreEnemyTroopsOnMapFlag = booleanFlag == 1;

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
