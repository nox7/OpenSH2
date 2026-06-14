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
      Scenario obj = new Scenario();

      // First 4 bytes are unknown. Always seems to be 0C (12)
      reader.ReadInt32();

      // Read all mission object Ids for this scenario
      obj.MissionObjectIds = S2MReaderUtils.ReadListOfInts(reader);

      // Read unknown "16"
      reader.ReadInt32();

      // Read 3 sets of 00s
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      // Read unknown 2-byte value. Seems to always be (01 01)
      reader.ReadInt16();

      // Read starting year
      obj.StartingYear = reader.ReadInt32();

      // Read starting month
      obj.StartingMonth = reader.ReadInt32();

      // Read unknown 02 00 00 00
      reader.ReadInt32();

      // Read unknown 30 bytes
      reader.ReadBytes(30);

      // Read 16 estate ownership Lord Ids
      for (int i = 0; i < 16; i++)
      {
        int lordId = reader.ReadInt32();
        if (lordId == 0)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.NoLord;
        }
        else if (lordId == 1)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.Player;
        }
        else if (lordId == 2)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.Olaf;
        }
        else if (lordId == 3)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.LordBarclay;
        }
        else if (lordId == 4)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.TheHawk;
        }
        else if (lordId == 5)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.TheBull;
        }
        else if (lordId == 6)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.LadySeren;
        }
        else if (lordId == 7)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.Edwin;
        }
        else if (lordId == 8)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.TheKing;
        }
        else if (lordId == 9)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.SirWilliam;
        }
        else if (lordId == 10)
        {
          obj.EstateOwnership[i] = Enums.Lords4Enum.SirGrey;
        }
      }

      // Read 28 unknown bytes
      reader.ReadBytes(28);

      // Read 16 estate production types
      for (int i = 0; i < 16; i++)
      {
        int productionTypeId = reader.ReadInt32();
        if (productionTypeId == 0)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Apples;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 1)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Cheese;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 2)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Bread;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 3)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Wood;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
      }

      return obj;
    }
  }
}
