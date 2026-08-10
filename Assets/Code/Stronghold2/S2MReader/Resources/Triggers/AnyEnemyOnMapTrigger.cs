using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  internal class AnyEnemyOnMapTrigger : Trigger
  {
    /// <summary>
    /// If this is true, then this trigger represents a check for "Enemies are on the map".
    /// If this is false, then this trigger is a condition for "no enemies left on map"
    /// </summary>
    public bool IsEnemiesOnMapFlag { get; set; }
  }
}
