using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set's the player's rank
  /// </summary>
  internal class SetRankAction : Action
  {
    public Rank Rank { get; set; }
  }
}
