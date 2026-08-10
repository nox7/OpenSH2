using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Sets the disease-production level in the player's estate
  /// </summary>
  internal class DiseaseProductionAction : Action
  {
    public IntensityLevel Level { get; set; }
  }
}
