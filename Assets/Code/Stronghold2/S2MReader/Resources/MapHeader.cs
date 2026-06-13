using Assets.Code.Stronghold2.S2MReader.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class MapHeader : S2Object
  {
    public int EstateMarkersObjectId { get; set; }
    public int ScenarioObjectId { get; set; }
    public string MapFileName { get; set; }
    public MapType MapType { get; set; }
  }
}
