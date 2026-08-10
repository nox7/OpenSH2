using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Sets the crime-rate level in the player's estate
  /// </summary>
  internal class CrimeRateAction : Action
  {
    public IntensityLevel Level { get; set; }
  }
}
