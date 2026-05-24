using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2
{
  /// <summary>
  /// Represents the numerical values of the lords used in Stronghold 2 binary file formats.
  /// </summary>
  enum Lords
  {
    Player = 1,
    Olaf = 2,
    LordBarclay = 3,
    TheHawk = 4,
    TheBull = 5,
    LadySeren = 6,
    Edwin = 7,
    TheKing = 8,
    SirWilliam = 9,
    SirGrey = 10,
    // The editor never allows us to select The Queen or The Bishop; but they're probably present as further enum values?
  }
}
