using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// When the define lord takes _at least_ the defined damage percentage.
  /// </summary>
  internal class LordDamagedTrigger : Trigger
  {
    public S2MLords Lord { get; set; }
    public int DamagePercentage { get; set; }
  }
}
