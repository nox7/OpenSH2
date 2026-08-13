using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class MultipleLordsDeadTriggerReader : TriggerReader
  {
    public MultipleLordsDeadTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      MultipleLordsDeadTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      reader.ReadByte();
      obj.CheckIfOlafIsDead = reader.ReadBoolean();
      obj.CheckIfBarclayIsDead = reader.ReadBoolean();
      obj.CheckIfTheHawkIsDead = reader.ReadBoolean();
      obj.CheckIfTheBullIsDead = reader.ReadBoolean();
      obj.CheckIfLadySerenIsDead = reader.ReadBoolean();
      obj.CheckIfEdwinIsDead = reader.ReadBoolean();
      obj.CheckIfTheKingIsDead = reader.ReadBoolean();
      obj.CheckIfSirWilliamIsDead = reader.ReadBoolean();
      obj.CheckIfSirGreyIsDead = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
