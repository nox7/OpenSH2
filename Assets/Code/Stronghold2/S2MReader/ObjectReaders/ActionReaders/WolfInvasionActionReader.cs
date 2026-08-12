using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class WolfInvasionActionReader : ActionReader
  {
    public WolfInvasionActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      WolfInvasionAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.SpawnFlagColor = (FlagColor)reader.ReadInt32();
      obj.SpawnFlagNumber = reader.ReadInt32();
      obj.TargetFlagColor = (FlagColor)reader.ReadInt32();
      obj.TargetFlagNumber = reader.ReadInt32();
      obj.NumberOfWolves = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
