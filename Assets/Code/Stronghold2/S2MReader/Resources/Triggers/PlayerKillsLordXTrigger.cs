using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggered when the player kills the defined lord.
  /// </summary>
  internal class PlayerKillsLordXTrigger : Trigger
  {
    public S2MLords Lord { get; set; }
  }
}
