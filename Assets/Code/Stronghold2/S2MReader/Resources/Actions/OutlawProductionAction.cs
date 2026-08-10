using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class OutlawProductionAction : Action
  {
    public IntensityLevel Level { get; set; }
    public int MaximumOutlaws { get; set; }
    /// <summary>
    /// 0 = All of the map
    /// 1 = Own estate (where the camp is place)
    /// 2 = Neighboring estate
    /// 3 = Own and neighboring estate
    /// 4 = Player/Human estate only
    /// </summary>
    public int Location { get; set; }
  }
}
