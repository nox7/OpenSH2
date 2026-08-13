using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggers when a specific lord kills the target lord
  /// </summary>
  internal class SpecificLordKillsLordXTrigger : Trigger
  {
    public S2MLords Killer { get; set; }
    public S2MLords Target { get; set; }
  }
}
