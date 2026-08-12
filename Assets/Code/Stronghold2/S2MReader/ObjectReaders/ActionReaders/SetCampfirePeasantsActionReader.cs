using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class SetCampfirePeasantsActionReader : ActionReader
  {
    public SetCampfirePeasantsActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SetCampfirePeasantsAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.NumPeasants = reader.ReadInt32();
      obj.FlagColor = (FlagColor)reader.ReadInt32();
      obj.FlagNumber = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
