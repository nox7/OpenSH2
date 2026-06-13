using Assets.Code.Stronghold2.S2MReader.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class EstateMarkerFlag
  {
    public int X { get; set; }
    public int Y { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public EstateType Type { get; set; }
  }
}
