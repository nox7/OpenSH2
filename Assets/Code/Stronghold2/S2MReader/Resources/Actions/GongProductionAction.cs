using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set gong production level in player's estate.
  /// </summary>
  internal class GongProductionAction : Action
  {
    public IntensityLevel Level { get; set; }
  }
}
