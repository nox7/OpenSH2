using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class MoveShipActionReader : ActionReader
  {
    public MoveShipActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      MoveShipAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.SpawnFlagColor = (FlagColor)reader.ReadInt32();
      obj.SpawnFlagNumber = reader.ReadInt32();
      obj.SpawnDelayToNextDestination = reader.ReadInt32();

      obj.Destination1FlagColor = (FlagColor)reader.ReadInt32();
      obj.Destination1FlagNumber = reader.ReadInt32();
      obj.Destination1DelayToNextDestination = reader.ReadInt32();

      obj.Destination2FlagColor = (FlagColor)reader.ReadInt32();
      obj.Destination2FlagNumber = reader.ReadInt32();
      obj.Destination2DelayToNextDestination = reader.ReadInt32();

      obj.Destination3FlagColor = (FlagColor)reader.ReadInt32();
      obj.Destination3FlagNumber = reader.ReadInt32();
      obj.Destination3DelayToNextDestination = reader.ReadInt32();

      obj.Destination4FlagColor = (FlagColor)reader.ReadInt32();
      obj.Destination4FlagNumber = reader.ReadInt32();

      reader.ReadInt32();

      obj.ShipType = reader.ReadByte();
      obj.DoesLeaveMap = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
