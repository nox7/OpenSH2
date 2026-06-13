using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ScenarioReader : ObjectReader
  {
    public ScenarioReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      MapHeader obj = new();

      // Read first 4 bytes. 04 00 00 00. Unknown purpose
      reader.ReadInt32();

      // Read Id of EstateMarkers object
      obj.EstateMarkersObjectId = reader.ReadInt32();

      // Read type index of estate markers
      reader.ReadInt32();

      // Read 04 00 00 00. Unknown purpose
      reader.ReadInt32();

      // Read Id of Scenario object that starts the map
      obj.ScenarioObjectId = reader.ReadInt32();

      // Read type index of scenario object
      reader.ReadInt32();

      // Read number of bytes that make up map file name (we don't need this)
      reader.ReadInt32();

      // Read the map file name as a UTF-16 string
      obj.MapFileName = S2MReaderUtils.ReadUtf16String(reader);

      // Read unknown single byte after map file name
      reader.ReadByte();

      // Read map type
      int mapType = reader.ReadInt32();

      if (mapType == 0)
      {
        obj.MapType = MapType.Kingmaker;
      }
      else if (mapType == 2)
      {
        obj.MapType = MapType.WarCampaign;
      }
      else if (mapType == 3)
      {
        obj.MapType = MapType.PeaceCampaign;
      }
      else if (mapType == 3)
      {
        obj.MapType = MapType.FreeBuild;
      }

      // Read object end trailer bytes
      reader.ReadInt32();

      return obj;
    }
  }
}
