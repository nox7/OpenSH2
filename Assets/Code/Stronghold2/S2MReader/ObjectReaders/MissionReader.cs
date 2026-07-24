using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class MissionReader : ObjectReader
  {
    public MissionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      Mission obj = new();

      // Read all mission object Ids for this scenario
      obj.ScenarioEventObjectIds = S2MReaderUtils.ReadListOfInts(reader, false);

      // Read unknown "CC"
      reader.ReadInt32();

      // Read unknown "C8"
      reader.ReadInt32();

      // Read unknown "01" byte
      reader.ReadByte();

      obj.BuildingAvailability.Stockpile = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Sawpit = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WheatFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.StoneQuarry = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Mill = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Bakery = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Granary = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.FletchersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.PoleturnersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Armory = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Inn = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.HopsFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Brewery = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.BlacksmithsWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.IronMine = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Treasury = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.OxTether = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Hovel = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.LookoutTower = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ManorHouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SmallKeep = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.StrongKeep = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.RoyalKeep = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Barracks = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Vineyard = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.LordsKitchen = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.VintnersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.EelPond = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ServantsQuarters = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SmallGatehouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.MainGatehouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.LargeGatehouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WoodenGatehouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Stairwell = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Bastion = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.RoundTower = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SquareTower = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.GreatTower = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.PigFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Ladder = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.DairyFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.AppleFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.HuntersPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WoodenPlatform = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.VegetableGarden = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Well = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WaterPot = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.OilSmelter = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Courthouse = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Dungeon = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TorturersGuild = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.GuardPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Gallows = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.BurningPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ExecutionersBlock = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Gibbet = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.StretchingRack = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Stocks = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.FloggingPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TortureWheel = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.HumiliationMask = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.BrandingChair = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.BedChamber = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Market = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.MercenaryPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.FalconersPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.PitchRig = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.EngineersGuild = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.GongPit = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SiegeCamp = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.BeeHive = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ChandlersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Statue = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ArmorersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WeaversWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SheepFarm = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TannersWorkshop = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WarHoundCage = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Apothecary = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Church = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Stables = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.EstateFlagpole = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.CarterPost = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Monastery = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.MusiciansGuild = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.ManTrap = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.KillingPit = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SallyPort = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TunnelEntrance = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.Jousting = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TravelingFair = GetFeatureAvailabilityFromByte(reader);

      // Read 04 unknown bytes (All 01s, probably reserved or now-removed buildings)
      reader.ReadBytes(94);

      obj.BuildingAvailability.TowerAndWallMountedBrazier = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WallMountedStoneTipper = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.RockBasketsForWalls = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.PlacePitchDitch = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WallMountedRollingLogs = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.PlaceMoats = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.WoodenWalls = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SingleThicknessWall = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.DoubleThicknessWall = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TripleThicknessWall = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.RoundTowerWithHoarding = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.SquareTowerWithHoarding = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TowerMountedMangonel = GetFeatureAvailabilityFromByte(reader);
      obj.BuildingAvailability.TowerMountedBallista = GetFeatureAvailabilityFromByte(reader);

      // Skip 14 unknown bytes
      reader.ReadBytes(14);

      // Read the tradeable resource availability for 29 resources.
      obj.TradeAvailability.Wood = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Stone = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Iron = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Wheat = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Flour = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Hops = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Ale = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Grapes = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Pitch = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Candles = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Wool = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Cloth = GetFeatureAvailabilityFromByte(reader, true);
      reader.ReadBytes(2); // unknown tradeable item
      obj.TradeAvailability.Eel = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Geese = GetFeatureAvailabilityFromByte(reader, true);
      reader.ReadBytes(2); // unknown tradeable item
      obj.TradeAvailability.Pigs = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Vegetables = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Wine = GetFeatureAvailabilityFromByte(reader, true);
      reader.ReadBytes(2); // unknown tradeable item
      reader.ReadBytes(2); // unknown tradeable item
      obj.TradeAvailability.Apples = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Bread = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Cheese = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Meat = GetFeatureAvailabilityFromByte(reader, true);
      reader.ReadBytes(2); // unknown tradeable item
      reader.ReadBytes(2); // unknown tradeable item
      reader.ReadBytes(2); // unknown tradeable item
      reader.ReadBytes(2); // unknown tradeable item
      obj.TradeAvailability.Bows = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Crossbows = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Swords = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Maces = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Pikes = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.Spears = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.MetalArmor = GetFeatureAvailabilityFromByte(reader, true);
      obj.TradeAvailability.LeatherArmor = GetFeatureAvailabilityFromByte(reader, true);

      // Skip unknown 14 bytes
      reader.ReadBytes(14);

      // Skip unknown 4 bytes
      reader.ReadInt32();

      // Skip unknown 4 bytes
      reader.ReadInt32();

      obj.StartingGold = reader.ReadInt32();

      // Skip unknown 4 bytes
      reader.ReadInt32();

      obj.StartingPopularity = reader.ReadInt32();

      // Skip 4 sets of unknown 4 bytes
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      // Read the starting amounts of resources. 4 bytes each
      obj.StartingResources.Wood = reader.ReadInt32();
      obj.StartingResources.Stone = reader.ReadInt32();
      obj.StartingResources.Iron = reader.ReadInt32();
      obj.StartingResources.Wheat = reader.ReadInt32();
      obj.StartingResources.Flour = reader.ReadInt32();
      obj.StartingResources.Hops = reader.ReadInt32();
      obj.StartingResources.Ale = reader.ReadInt32();
      obj.StartingResources.Grapes = reader.ReadInt32();
      obj.StartingResources.Pitch = reader.ReadInt32();
      obj.StartingResources.Candles = reader.ReadInt32();
      obj.StartingResources.Wool = reader.ReadInt32();
      obj.StartingResources.Cloth = reader.ReadInt32();
      reader.ReadInt32(); // unknown resource
      obj.StartingResources.Eel = reader.ReadInt32();
      obj.StartingResources.Geese = reader.ReadInt32();
      reader.ReadInt32(); // unknown resource
      obj.StartingResources.Pigs = reader.ReadInt32();
      obj.StartingResources.Vegetables = reader.ReadInt32();
      obj.StartingResources.Wine = reader.ReadInt32();
      reader.ReadInt32(); // unknown resource
      reader.ReadInt32(); // unknown resource
      obj.StartingResources.Apples = reader.ReadInt32();
      obj.StartingResources.Bread = reader.ReadInt32();
      obj.StartingResources.Cheese = reader.ReadInt32();
      obj.StartingResources.Meat = reader.ReadInt32();
      reader.ReadInt32(); // unknown resource
      reader.ReadInt32(); // unknown resource
      reader.ReadInt32(); // unknown resource
      reader.ReadInt32(); // unknown resource
      obj.StartingResources.Bows = reader.ReadInt32();
      obj.StartingResources.Crossbows = reader.ReadInt32();
      obj.StartingResources.Swords = reader.ReadInt32();
      obj.StartingResources.Maces = reader.ReadInt32();
      obj.StartingResources.Pikes = reader.ReadInt32();
      obj.StartingResources.Spears = reader.ReadInt32();
      obj.StartingResources.MetalArmor = reader.ReadInt32();
      obj.StartingResources.LeatherArmor = reader.ReadInt32();

      // Skip unknown 51 bytes
      reader.ReadBytes(51);

      // Read 4 object-terminator bytes (AF1EFFFF)
      reader.ReadInt32();

      return obj;
    }

    /// <summary>
    /// Gets the availabiltiy from a byte. Because tradeable resources are stored as a byte but duplicate their availability as the following byte
    /// (i.e., "03" will be "03 03" in the save file) the option to skip the next byte is provided.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="skipNextByte"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    private MissionFeatureAvailability GetFeatureAvailabilityFromByte(BinaryReader reader, bool skipNextByte = false)
    {
      byte flag = reader.ReadByte();

      if (skipNextByte)
      {
        reader.ReadByte();
      }

      if (flag == 0)
      {
        return MissionFeatureAvailability.Disabled;
      }
      else if (flag == 1)
      {
        return MissionFeatureAvailability.Enabled;
      }
      else if (flag == 2)
      {
        return MissionFeatureAvailability.Requires1Quest;
      }
      else if (flag == 3)
      {
        return MissionFeatureAvailability.Requires2Quests;
      }
      else if (flag == 4)
      {
        return MissionFeatureAvailability.Requires3Quests;
      }
      else
      {
        throw new InvalidDataException($"Unexpected feature availability flag: {flag}");
      }
    }
  }
}
