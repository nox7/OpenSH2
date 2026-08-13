using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When an enemy lord acquires a certain amount of goods this trigger is activated.
  /// </summary>
  internal class EnemyGoodsAcquiredTrigger : Trigger
  {
    public GoodsAmountList Goods { get; set; } = new();
    public S2MLords Lord { get; set; }
  }
}
