using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class InvasionAction : Action
  {
    public FlagColor InvasionPointFlagColor { get; set; }
    public int InvasionPointFlagNumber { get; set; }
    /// <summary>
    /// Lord who owns the army
    /// </summary>
    public Lord Lord { get; set; }
    public UnitAmountList Troops  { get; set; }
    public FlagColor TargetPointFlagColor { get; set; }
    public int TargetPointFlagNumber { get; set; }
    /// <summary>
    /// If null, then there is no target lord
    /// </summary>
    public Lord? TargetLord { get; set; } = null;
    /// <summary>
    /// 0 = Movement army
    /// 1 = Siege army
    /// 2 = Defensive army
    /// 3 = Attacking army
    /// </summary>
    public int ArmyType { get; set; }
    public bool DoesLeaveMap { get; set; }
    /// <summary>
    /// If AreInvasionWarningsEnabled is true, then this applies:
    /// False = Early warnings
    /// True = Full warnings
    /// </summary>
    public bool DoesReceiveFullWarnings { get; set; }
    /// <summary>
    /// 0 = Attack player
    /// 1 = Reinforce player
    /// </summary>
    public int AttackOrReinforcePlayer { get; set; }
    public bool IncludeLordInInvasion { get; set; }
    public bool AreInvasionWarningsEnabled { get; set; }
  }
}
