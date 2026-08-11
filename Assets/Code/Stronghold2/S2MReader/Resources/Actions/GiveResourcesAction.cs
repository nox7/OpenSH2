using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Give resources to estate.
  /// </summary>
  internal class GiveResourcesAction : Action
  {
    /// <summary>
    /// 0 = All estates on map
    /// 1 = Marked estate (by flag color and number)
    /// 2 = Specific lord's estate
    /// </summary>
    public int EstateSetting { get; set; }
    public FlagColor EstateFlagColor { get; set; }
    public int EstateFlagNumber { get; set; }
    public S2MLords SpecificLord { get; set; }
    public GoodsAmountList Goods { get; set; }
    public int Gold { get; set; }
    public int Duration { get; set; }
  }
}
