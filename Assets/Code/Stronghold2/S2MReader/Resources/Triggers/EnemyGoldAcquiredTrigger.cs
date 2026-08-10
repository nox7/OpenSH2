using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When an enemy lord acquires a certain amount of gold, this trigger is activated.
  /// </summary>
  internal class EnemyGoldAcquiredTrigger : Trigger
  {
    public int GoldAmount { get; set; }
    public Lord Lord { get; set; }
  }
}
