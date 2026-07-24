using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ScenarioEventReader : ObjectReader
  {
    public ScenarioEventReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      ScenarioEvent obj = new();

      // First 4 bytes are unknown. Always seems to be 0C (12)
      reader.ReadInt32();

      obj.EventLengthInMonths = reader.ReadInt32();

      // Null byte
      reader.ReadByte();

      // Action repeat settings
      obj.ActionRepeatCount = reader.ReadInt32();
      obj.ActionRepeatDelay = reader.ReadInt32();
      obj.ActionObjectId = reader.ReadInt32();

      // Null byte
      reader.ReadByte();

      obj.EventDelay = reader.ReadInt32();

      // Unknown bytes
      reader.ReadInt32();
      reader.ReadInt32();

      // Read list of trigger object Ids
      obj.TriggerObjectIds = S2MReaderUtils.ReadListOfInts(reader, true);

      // Read 4 object-terminator bytes (AF1EFFFF)
      var terminator = reader.ReadInt32();
      if (terminator != S2MReaderUtils.TrailerMarker)
      {
        throw new InvalidDataException($"Invalid object terminator. Got {terminator:X8}");
      }

      return obj;
    }
  }
}
