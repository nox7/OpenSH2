namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set's the number of idle peasants in the estate marked by the flag
  /// </summary>
  internal class SetCampfirePeasantsAction : Action
  {
    public int NumPeasants { get; set; }
    public FlagColor FlagColor { get; set; }
    public int FlagNumber { get; set; }
  }
}
