using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When an enemy lord acquires a certain amount of honor, this trigger is activated.
  /// </summary>
  internal class EnemyHonourAcquiredTrigger : Trigger
  {
    public int HonorAmount { get; set; }
    public Lord Lord { get; set; }
  }
}
