using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class Mission : S2Object
  {
    public List<int> ScenarioEventObjectIds { get; set; } = new();
    public MissionBuildingAvailability BuildingAvailability { get; set; } = new();
    public MissionTradeAvailability TradeAvailability { get; set; } = new();
    public int StartingGold { get; set; }
    public int StartingPopularity { get; set; }
    public MissionStartingResources StartingResources { get; set; } = new();
  }
}
