using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class CapResourcesActionReader : ActionReader
  {
    public CapResourcesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      CapResourcesAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.EstateSetting = reader.ReadInt32();
      obj.EstateFlagColor = (FlagColor)reader.ReadInt32();
      obj.EstateFlagNumber = reader.ReadInt32();

      int lordId = reader.ReadInt32();

      if (lordId == 1)
      {
        obj.SpecificLord = S2MLords.Player;
      }
      else if (lordId == 2)
      {
        obj.SpecificLord = S2MLords.Olaf;
      }
      else if (lordId == 3)
      {
        obj.SpecificLord = S2MLords.LordBarclay;
      }
      else if (lordId == 4)
      {
        obj.SpecificLord = S2MLords.TheHawk;
      }
      else if (lordId == 5)
      {
        obj.SpecificLord = S2MLords.TheBull;
      }
      else if (lordId == 6)
      {
        obj.SpecificLord = S2MLords.LadySeren;
      }
      else if (lordId == 7)
      {
        obj.SpecificLord = S2MLords.Edwin;
      }
      else if (lordId == 8)
      {
        obj.SpecificLord = S2MLords.TheKing;
      }
      else if (lordId == 9)
      {
        obj.SpecificLord = S2MLords.SirWilliam;
      }
      else if (lordId == 10)
      {
        obj.SpecificLord = S2MLords.SirGrey;
      }

      // BEGIN Reading all resources
      reader.ReadInt32();
      obj.GoodsCaps.Wood = reader.ReadInt32();
      obj.GoodsCaps.Stone = reader.ReadInt32();
      obj.GoodsCaps.Iron = reader.ReadInt32();
      obj.GoodsCaps.Wheat = reader.ReadInt32();
      obj.GoodsCaps.Flour = reader.ReadInt32();
      obj.GoodsCaps.Hops = reader.ReadInt32();
      obj.GoodsCaps.Ale = reader.ReadInt32();
      obj.GoodsCaps.Grapes = reader.ReadInt32();
      obj.GoodsCaps.Pitch = reader.ReadInt32();
      obj.GoodsCaps.Candles = reader.ReadInt32();
      obj.GoodsCaps.Wool = reader.ReadInt32();
      obj.GoodsCaps.Cloth = reader.ReadInt32();
      reader.ReadInt32();
      obj.GoodsCaps.Eels = reader.ReadInt32();
      obj.GoodsCaps.Geese = reader.ReadInt32();
      reader.ReadInt32();
      obj.GoodsCaps.Pigs = reader.ReadInt32();
      obj.GoodsCaps.Vegetables = reader.ReadInt32();
      obj.GoodsCaps.Wine = reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      obj.GoodsCaps.Apples = reader.ReadInt32();
      obj.GoodsCaps.Bread = reader.ReadInt32();
      obj.GoodsCaps.Cheese = reader.ReadInt32();
      obj.GoodsCaps.Meat = reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      obj.GoodsCaps.Bows = reader.ReadInt32();
      obj.GoodsCaps.Crossbows = reader.ReadInt32();
      obj.GoodsCaps.Swords = reader.ReadInt32();
      obj.GoodsCaps.Maces = reader.ReadInt32();
      obj.GoodsCaps.Pikes = reader.ReadInt32();
      obj.GoodsCaps.Spears = reader.ReadInt32();
      obj.GoodsCaps.MetalArmor = reader.ReadInt32();
      obj.GoodsCaps.LeatherArmor = reader.ReadInt32();
      // END Reading all resources

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.GoldCap = reader.ReadInt32();
      obj.DurationOfCap = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
