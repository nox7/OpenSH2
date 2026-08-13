using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When any of the player's troops reach the defined lord
  /// </summary>
  internal class RescueLordTrigger : Trigger
  {
    public S2MLords Lord { get; set; }
  }
}
