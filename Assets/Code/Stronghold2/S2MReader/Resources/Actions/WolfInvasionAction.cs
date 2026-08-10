namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Sends wolves to invase the target flag spawning at the spawn flag.
  /// </summary>
  internal class WolfInvasionAction : Action
  {
    public FlagColor SpawnFlagColor { get; set; }
    public int SpawnFlagNumber { get; set; }
    public FlagColor TargetFlagColor { get; set; }
    public int TargetFlagNumber { get; set; }
    public int NumberOfWolves { get; set; }
  }
}
