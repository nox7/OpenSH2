using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// TODO Investigate if it stops invasion AGAINST the lord, or made BY the lord listed
  /// </summary>
  internal class StopInvasionsAction : Action
  {
    public Lord Lord { get; set; }
    /// <summary>
    /// 0 = Stop repeating invasions only
    /// 1 = Stop all invasions
    /// </summary>
    public int InvasionType { get; set; }
  }
}
