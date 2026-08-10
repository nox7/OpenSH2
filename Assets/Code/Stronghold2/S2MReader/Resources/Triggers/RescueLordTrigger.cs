using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When any of the player's troops reach the defined lord
  /// </summary>
  internal class RescueLordTrigger : Trigger
  {
    public Lord Lord { get; set; }
  }
}
