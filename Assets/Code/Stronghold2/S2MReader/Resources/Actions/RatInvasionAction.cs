
namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Spawn X rats in the estate identified by the flag
  /// </summary>
  internal class RatInvasionAction : Action
  {
    public FlagColor EstateFlagColor { get; set; }
    public int EstateFlagNumber { get; set; }
    public int NumRats { get; set; }
  }
}
