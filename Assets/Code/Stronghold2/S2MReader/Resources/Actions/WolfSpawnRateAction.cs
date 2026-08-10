using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Sets the map's wolf spawn level
  /// </summary>
  internal class WolfSpawnRateAction : Action
  {
    public IntensityLevel Level { get; set; }
  }
}
