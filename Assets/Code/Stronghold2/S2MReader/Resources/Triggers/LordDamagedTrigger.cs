using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When the define lord takes _at least_ the defined damage percentage.
  /// </summary>
  internal class LordDamagedTrigger : Trigger
  {
    public Lord Lord { get; set; }
    public int DamagePercentage { get; set; }
  }
}
