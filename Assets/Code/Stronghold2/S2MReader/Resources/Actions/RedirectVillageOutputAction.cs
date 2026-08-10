namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Redirects the delivery outputs of a village to another estate identified by the target flag.
  /// </summary>
  internal class RedirectVillageOutputAction : Action
  {
    public FlagColor SourceEstateFlagColor { get; set; }
    public int SourceEstateFlagNumber { get; set; }
    public FlagColor TargetEstateFlagColor { get; set; }
    public int TargetEstateFlagNumber { get; set; }
  }
}
