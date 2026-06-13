using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class EstateMarkers : S2Object
  {
    public List<EstateMarkerFlag> Markers { get; set; } = new();
  }
}
