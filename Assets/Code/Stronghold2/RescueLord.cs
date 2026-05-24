using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2
{
  /// <summary>
  /// Represents the numerical values of the lords used in Stronghold 2 binary file formats for the RescueLord trigger.
  /// </summary>
  enum RescueLord
  {
    Olaf = 0,
    LordBarclay = 1,
    TheHawk = 2,
    TheBull = 3,
    LadySeren = 4,
    Edwin = 5,
    TheKing = 6,
    SirWilliam = 7,
    SirGrey = 8,
    // The editor never allows us to select The Queen or The Bishop; but they're probably present as further enum values?
  }
}
