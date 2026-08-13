using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggers when any lord that isn't the player kills the defined lord
  /// </summary>
  internal class OtherLordsKillsLordXTrigger : Trigger
  {
    public S2MLords TargetLord { get; set; }
  }
}
