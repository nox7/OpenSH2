using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  internal class BreachInWallTrigger : Trigger
  {
    /// <summary>
    /// The lord that owns the land whose wall is breached.
    /// 
    /// Assumedly "breach" means when their castle is no longer enclosed.
    /// </summary>
    public S2MLords Lord { get; set; }
  }
}
