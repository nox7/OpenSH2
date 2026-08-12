using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class SetRankActionReader : ActionReader
  {
    public SetRankActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SetRankAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.Rank = reader.ReadInt32() switch
      {
        0 => S2MRank.Freeman,
        1 => S2MRank.Yeoman,
        2 => S2MRank.Squire,
        3 => S2MRank.Knight,
        4 => S2MRank.KnightBachelor,
        5 => S2MRank.KnightErrant,
        6 => S2MRank.RoyalChampion,
        7 => S2MRank.Baron,
        8 => S2MRank.Earl,
        9 => S2MRank.Duke,
        _ => S2MRank.Freeman
      };

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
