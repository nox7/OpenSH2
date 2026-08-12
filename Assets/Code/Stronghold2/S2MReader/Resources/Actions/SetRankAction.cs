using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set's the player's rank
  /// </summary>
  internal class SetRankAction : Action
  {
    public S2MRank Rank { get; set; }
  }
}
