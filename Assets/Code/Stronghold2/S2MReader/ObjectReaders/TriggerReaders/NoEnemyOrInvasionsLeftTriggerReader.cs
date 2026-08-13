using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class NoEnemyOrInvasionsLeftTriggerReader : TriggerReader
  {
    public NoEnemyOrInvasionsLeftTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      NoEnemyOrInvasionsLeftTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.AreEnemiesOnMapCheck = reader.ReadInt32() == 1;

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
