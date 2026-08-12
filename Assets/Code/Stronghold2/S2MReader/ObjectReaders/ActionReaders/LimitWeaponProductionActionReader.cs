using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class LimitWeaponProductionActionReader : ActionReader
  {
    public LimitWeaponProductionActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      LimitWeaponProductionAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.BowsEnabled = reader.ReadBoolean();
      obj.CrossbowsEnabled = reader.ReadBoolean();
      obj.SpearsEnabled = reader.ReadBoolean();
      obj.PikesEnabled = reader.ReadBoolean();
      obj.MacesEnabled = reader.ReadBoolean();
      obj.SwordsEnabled = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
