using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class RedirectVillageOutputActionReader : ActionReader
  {
    public RedirectVillageOutputActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      RedirectVillageOutputAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.SourceEstateFlagColor = (FlagColor)reader.ReadInt32();
      obj.SourceEstateFlagNumber = reader.ReadInt32();
      obj.TargetEstateFlagColor = (FlagColor)reader.ReadInt32();
      obj.TargetEstateFlagNumber = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
