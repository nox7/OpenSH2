using Assets.Code.Stronghold2.S2MReader.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class S2MFile
  {
    public string Author { get; set; }
    public MapType MapType { get; set; }
    public bool Balanced { get; set; }
    public string LastSave { get; set; }
    public int MaxPlayers { get; set; }
    public int Version { get; set; }
  }
}
