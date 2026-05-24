using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2
{
  /// <summary>
  /// Represents the numerical values of the lords used in Stronghold 2 binary file formats for the PercentTroopsLord trigger.
  /// </summary>
  enum PercentTroopsLord
  {
    AllLords = -1,
    Player = 0,
    Olaf = 1,
    LordBarclay = 2,
    TheHawk = 3,
    TheBull = 4,
    LadySeren = 5,
    Edwin = 6,
    TheKing = 7,
    SirWilliam = 8,
    SirGrey = 9,
    // The editor never allows us to select The Queen or The Bishop; but they're probably present as further enum values?
  }
}
