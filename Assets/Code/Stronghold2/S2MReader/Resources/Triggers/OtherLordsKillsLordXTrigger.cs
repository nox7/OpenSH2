using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggers when any lord that isn't the player kills the defined lord
  /// </summary>
  internal class OtherLordsKillsLordXTrigger : Trigger
  {
    public Lord TargetLord { get; set; }
  }
}
