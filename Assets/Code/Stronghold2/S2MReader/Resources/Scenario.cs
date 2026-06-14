using Assets.Code.Stronghold2.S2MReader.Enums;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class Scenario : S2Object
  {
    public List<int> MissionObjectIds { get; set; } = new();
    public int StartingYear { get; set; }
    public int StartingMonth { get; set; }
    public Lords4Enum[] EstateOwnership { get; set; } = new Lords4Enum[16];
    public EstateProductionType[] EstateProductionTypes { get; set; } = new EstateProductionType[16];
    public EstatePopulationLevel[] EstatePopulationLevels { get; set; } = new EstatePopulationLevel[16];
    public EstateAIBuildingPlacement[] EstateAIBuildingPlacements { get; set; } = new EstateAIBuildingPlacement[16];
  }
}
