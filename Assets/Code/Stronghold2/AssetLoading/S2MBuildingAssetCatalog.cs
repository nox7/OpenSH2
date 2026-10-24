using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Explicit S2M type-to-BDG mappings, audited against the installed assets.</summary>
  public static class S2MBuildingAssetCatalog
  {
    private static readonly Dictionary<string, string> Descriptors = new(StringComparer.Ordinal)
    {
      ["Apothecary"] = "apothecary", ["AppleFarm"] = "farm_apples",
      ["ArmourerWorkshop"] = "armourer", ["Armoury"] = "armoury",
      ["Bakery"] = "bakery_workshop", ["BeeHive"] = "beehive",
      ["BlacksmithWorkshop"] = "blacksmith_workshop", ["Brewery"] = "brewery",
      ["CarterPost"] = "cart_post", ["ChandlerWorkshop"] = "chandler_workshop",
      ["Church"] = "church", ["CourtHouse"] = "court_house",
      ["DairyFarm"] = "farm_dairy", ["Dungeon"] = "dungeon",
      ["EngineersGuild"] = "engineers_guild", ["FalconersPost"] = "falcon_post",
      ["FishPond"] = "fishpond", ["FletcherWorkshop"] = "fletcher_workshop",
      ["GardenersHut"] = "gardeners_hut", ["GongPit"] = "gong_farm",
      ["Granary"] = "granary", ["HopFarm"] = "farm_hops",
      ["HuntersPost"] = "hunters_post", ["Inn"] = "inn", ["IronMine"] = "iron_mine",
      ["Jousting"] = "jousting", ["KillingPit"] = "killing_pits", ["Kitchen"] = "kitchen",
      ["LargeGateHouse"] = "gatehouse_large", ["ManTrap"] = "mantrap",
      ["Market"] = "market", ["MediumGateHouse"] = "gatehouse_medium", ["Mill"] = "windmill",
      ["Monastery"] = "monastery", ["MusiciansGuild"] = "minstrels_guild",
      ["OutlawCamp"] = "bandit_camp", ["OxTether"] = "ox_tether",
      ["PigFarm"] = "farm_pigs", ["PitchRig"] = "pitch_rig",
      ["PoleturnerWorkshop"] = "poleturner_hut", ["SawPit"] = "saw_pit",
      ["ServantsQuarters"] = "servants_quarters_pantry", ["SheepFarm"] = "farm_sheep",
      ["Signpost"] = "signpost", ["SmallGateHouse"] = "gatehouse_small",
      ["Solar"] = "queens_solar", ["Stables"] = "stable", ["StockPile"] = "stockpile",
      ["StoneBarracks"] = "barrack_stone", ["StoneQuarry"] = "quarry",
      ["TannerWorkshop"] = "tanner_workshop", ["TortureBlock"] = "torture_block",
      ["TortureBrandingChair"] = "torture_branding", ["TortureBurningPost"] = "torture_burning",
      ["TortureFloggingPost"] = "torture_flogging", ["TortureGallows"] = "torture_gallows",
      ["TortureGibbet"] = "torture_gibbet", ["TortureMask"] = "torture_mask",
      ["TortureRack"] = "torture_streching", ["TorturersGuild"] = "torturer_guild",
      ["TortureStocks"] = "torture_stocks", ["TortureWheel"] = "torture_wheel",
      ["TowerGreat"] = "tower_great", ["TowerLookout"] = "tower_lookout",
      ["TowerRoundShort"] = "tower_round_short", ["TowerRoundTall"] = "tower_round_tall",
      ["TowerSquare"] = "tower_square", ["TowerStairwell"] = "tower_stairwell",
      ["Treasury"] = "treasury", ["WarHoundCage"] = "wardog_cage", ["WatchPost"] = "watchpost",
      ["WaterPot"] = "water_pot", ["WeaverWorkshop"] = "workshop_weaver", ["Well"] = "well",
      ["WheatFarm"] = "farm_wheat", ["WoodenGateHouse"] = "gatehouse_wooden",
      ["WoodenPlatform"] = "tower_wooden"
    };

    public static bool TryGetDescriptor(BuildingPlacement placement, byte[] payload,
      out string relativePath, out string reason)
    {
      relativePath = null;
      reason = null;
      if (Descriptors.TryGetValue(placement.Type, out string stem))
      {
        relativePath = $"meshes/buildings/{stem}.bdg";
        return true;
      }
      if (placement.Type == "Hovel")
      {
        // Final counted 28-byte Hovel body: appearance selector at body offset 4.
        if (payload != null)
        {
          for (int offset = payload.Length - 44; offset >= 0; offset--)
          {
            if (BitConverter.ToInt32(payload, offset) != unchecked((int)0xFFFFC1A5)
              || BitConverter.ToInt32(payload, offset + 4) != placement.TypeIndex
              || BitConverter.ToInt32(payload, offset + 8) != 0
              || BitConverter.ToInt32(payload, offset + 12) != 28
              || offset + 44 != payload.Length) continue;
            int variant = BitConverter.ToInt32(payload, offset + 20);
            if (variant >= 0 && variant < 6)
            {
              relativePath = $"meshes/buildings/hovel{variant + 1}.bdg";
              return true;
            }
            reason = $"Unrecognized Hovel appearance selector {variant}; expected 0..5.";
            return false;
          }
        }
        reason = "Hovel appearance block is not decoded; candidates are hovel1.bdg through hovel6.bdg.";
      }
      else if (placement.Type == "Statue")
        reason = "Ambiguous model variant: statue_horse.bdg or statue_lord.bdg; the selector is not yet mapped.";
      else if (placement.Type.StartsWith("Constructing", StringComparison.Ordinal))
        reason = "Construction progress/stage selection is not decoded; completed meshes would misrepresent this object.";
      else reason = "No verified descriptor mapping for this building type.";
      return false;
    }

    public static IReadOnlyList<string> GetStaticParts(string type) => type switch
    {
      "WoodenGateHouse" => new[] { "gatehouse_wooden_doors.GR2" },
      "SmallGateHouse" => new[] { "gatehouse_small_foundation.GR2", "gatehouse_small_doors.GR2" },
      "MediumGateHouse" => new[] { "gatehouse_medium_foundation.GR2", "gatehouse_medium_porticulis.GR2" },
      "LargeGateHouse" => new[] { "gatehouse_large_foundation.GR2", "gatehouse_large_bridge.GR2",
        "gatehouse_large_mechanism.GR2", "gatehouse_large_chain.GR2", "gatehouse_large_porticulis.GR2" },
      "Mill" => new[] { "windmill_wings.GR2", "windmill_shaft.GR2" },
      "PitchRig" => new[] { "pitch_rig_scooping.GR2" },
      "DairyFarm" => new[] { "farm_dairy_parts.GR2" },
      _ => Array.Empty<string>()
    };
  }
}
