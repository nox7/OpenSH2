namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Spawns bears at the flag.
  /// </summary>
  internal class BearAttackAction : Action
  {
    public FlagColor SpawnPointFlagColor { get; set; }
    public int SpawnPointFlagNumber { get; set; }
    public int NumberOfBears { get; set; }
  }
}
