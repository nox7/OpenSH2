using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class PauseSiegesAction : Action
  {
    public FlagColor SiegesNearFlagColor { get; set; }
    public int SiegesNearFlagNumber { get; set; }
    /// <summary>
    /// Lord who own's the siege
    /// </summary>
    public S2MLords Lord { get; set; }
    /// <summary>
    /// If this is false, then sieges will resume.
    /// If this is true, then sieges will be paused.
    /// </summary>
    public bool ShouldPauseSieges { get; set; }
  }
}
