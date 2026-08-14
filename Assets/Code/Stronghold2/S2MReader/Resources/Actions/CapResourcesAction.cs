using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// TODO Investigate this
  /// Not sure? Either applies a cap and resource will not increasee above the value, or it brings all resources to the defined values.
  /// </summary>
  internal class CapResourcesAction : Action
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
    /// <summary>
    /// Uses -1 to represent "no cap"
    /// </summary>
    public GoodsAmountList GoodsCaps { get; set; } = new();
    public int GoldCap { get; set; }
    public int DurationOfCap { get; set; }
  }
}
