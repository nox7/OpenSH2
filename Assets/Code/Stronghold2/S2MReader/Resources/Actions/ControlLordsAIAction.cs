using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Sets player control of specific lord.
  /// </summary>
  internal class ControlLordsAIAction : Action
  {
    public Lord Lord { get; set; }
    public bool IsPlayerControlled { get; set; }
  }
}
