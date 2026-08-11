using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Gives the target lord maximum peasants in their estate.
  /// </summary>
  internal class MaxOutPeasantsAction : Action
  {
    public S2MLords Lord { get; set; }
  }
}
