using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set the rat production level in the player's estate
  /// </summary>
  internal class RatProductionAction : Action
  {
    public IntensityLevel Level { get; set; }
  }
}
