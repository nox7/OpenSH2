using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Unsure as what this does.
  /// TODO Investigate
  /// </summary>
  internal class ControlGateHousesAction : Action
  {
    public Lord Lord { get; set; }
    /// <summary>
    /// 0 = Closed
    /// 1 = Open
    /// </summary>
    public bool GateHouseState { get; set; }
    public FlagColor GateHouseLocationFlagColor { get; set; }
    public int GateHouseLocationFlagNumber { get; set; }
  }
}
