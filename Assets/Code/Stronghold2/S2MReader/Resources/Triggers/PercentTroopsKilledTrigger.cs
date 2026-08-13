using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Percentage of the defined lord's troops are killed.
  /// Assumedly, when the mission starts stores the percentage of troops the lord
  /// has and then routinely checks if the lord's troops have been reduced by that percentage.
  /// </summary>
  internal class PercentTroopsKilledTrigger : Trigger
  {
    public S2MLords Lord { get; set; }
    public int Percentage { get; set; }
  }
}
