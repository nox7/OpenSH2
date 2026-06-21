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
      Scenario obj = new();

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
        else if (productionTypeId == 4)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Stone;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 5)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Iron;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 6)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Pitch;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 7)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.Ale;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Small;
        }
        else if (productionTypeId == 8)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.ApplesCheeseBread;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 9)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.WoodIronStone;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 10)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.ApplesCheeseBreadAle;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 11)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.ClothApples;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 12)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.WoodBread;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 13)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.StoneCheese;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 14)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.CandlesCheese;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Medium;
        }
        else if (productionTypeId == 15)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.ApplesCheeseBread;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Large;
        }
        else if (productionTypeId == 16)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.WoodIronStone;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Large;
        }
        else if (productionTypeId == 17)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.ApplesSpears;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Large;
        }
        else if (productionTypeId == 18)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.BreadBows;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Large;
        }
        else if (productionTypeId == 19)
        {
          obj.EstateProductionTypes[i] = Enums.EstateProductionType.LeatherArmorCrossbows;
          obj.EstatePopulationLevels[i] = Enums.EstatePopulationLevel.Large;
        }
      }

      // Read 25 unknown bytes
      reader.ReadBytes(25);

      // Read 16 estate "AI building placement" values
      // 0 or 1
      // 0 = AI places buildings
      // 1 = Use map buildings
      for (int i = 0; i < 16; i++)
      {
        byte buildingPlacementType = reader.ReadByte();
        if (buildingPlacementType == 0)
        {
          obj.EstateAIBuildingPlacements[i] = Enums.EstateAIBuildingPlacement.AIPlacesBuildings;
        }
        else if (buildingPlacementType == 1)
        {
          obj.EstateAIBuildingPlacements[i] = Enums.EstateAIBuildingPlacement.UseMapBuildings;
        }
      }

      // Read unknown 4 bytes (00 00 00 00)
      reader.ReadInt32();

      // Read 4 object-terminator bytes (AF1EFFFF)
      reader.ReadInt32();

      return obj;
    }
  }
}
