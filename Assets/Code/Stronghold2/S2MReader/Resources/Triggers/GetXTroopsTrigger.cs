using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  internal class GetXTroopsTrigger : Trigger
  {
    /// <summary>
    /// TODO. Find out troop types? 
    /// Archers = 4
    /// </summary>
    public int TroopType { get; set; }
    public int NumberOfTroops { get; set; }
  }
}
