using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggers when a specific lord kills the target lord
  /// </summary>
  internal class SpecificLordKillsLordXTrigger : Trigger
  {
    public Lord Killer { get; set; }
    public Lord Target { get; set; }
  }
}
