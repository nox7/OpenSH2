namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Marks a mission's quest as complete.
  /// </summary>
  internal class QuestAction : Action
  {
    /// <summary>
    /// 0 = Quest A
    /// 1 = Quest B
    /// 2 = Quest C
    /// </summary>
    public int Quest { get; set; }
  }
}
