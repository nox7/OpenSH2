namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Marks a mission's quest as failed.
  /// </summary>
  internal class QuestFailedAction : Action
  {
    /// <summary>
    /// 0 = Quest A
    /// 1 = Quest B
    /// 2 = Quest C
    /// </summary>
    public int Quest { get; set; }
  }
}
