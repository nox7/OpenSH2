using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggered when the specific lord dies
  /// </summary>
  internal class SpecificEnemyLordDiesTrigger : Trigger
  {
    public S2MLords Lord { get; set; }
  }
}
