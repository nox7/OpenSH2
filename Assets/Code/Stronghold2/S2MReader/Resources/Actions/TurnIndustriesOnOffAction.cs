using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// For the particular target, turns industries on or off (all building industries).
  /// </summary>
  internal class TurnIndustriesOnOffAction : Action
  {
    /// <summary>
    /// 0 = All estates on map
    /// 1 = Marked estate (by flag color)
    /// 2 = Specific lord's estate
    /// </summary>
    public int EstateSetting { get; set; }
    public FlagColor FlagColor { get; set; }
    public int FlagNumber { get; set; }
    public Lord Lord { get; set; }
    public GoodsBooleanList Industries { get; set; }
  }
}
