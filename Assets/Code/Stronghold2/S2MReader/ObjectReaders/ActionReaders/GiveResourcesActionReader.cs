using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class GiveResourcesActionReader : ActionReader
  {
    public GiveResourcesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      GiveResourcesAction obj = new();

      ReadActionHeader(reader);

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

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
      obj.Goods.Wood = reader.ReadInt32();
      obj.Goods.Stone = reader.ReadInt32();
      obj.Goods.Iron = reader.ReadInt32();
      obj.Goods.Wheat = reader.ReadInt32();
      obj.Goods.Flour = reader.ReadInt32();
      obj.Goods.Hops = reader.ReadInt32();
      obj.Goods.Ale = reader.ReadInt32();
      obj.Goods.Grapes = reader.ReadInt32();
      obj.Goods.Pitch = reader.ReadInt32();
      obj.Goods.Candles = reader.ReadInt32();
      obj.Goods.Wool = reader.ReadInt32();
      obj.Goods.Cloth = reader.ReadInt32();
      reader.ReadInt32();
      obj.Goods.Eels = reader.ReadInt32();
      obj.Goods.Geese = reader.ReadInt32();
      reader.ReadInt32();
      obj.Goods.Pigs = reader.ReadInt32();
      obj.Goods.Vegetables = reader.ReadInt32();
      obj.Goods.Grapes = reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      obj.Goods.Apples = reader.ReadInt32();
      obj.Goods.Bread = reader.ReadInt32();
      obj.Goods.Cheese = reader.ReadInt32();
      obj.Goods.Meat = reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      obj.Goods.Bows = reader.ReadInt32();
      obj.Goods.Crossbows = reader.ReadInt32();
      obj.Goods.Swords = reader.ReadInt32();
      obj.Goods.Maces = reader.ReadInt32();
      obj.Goods.Pikes = reader.ReadInt32();
      obj.Goods.Spears = reader.ReadInt32();
      obj.Goods.MetalArmor = reader.ReadInt32();
      obj.Goods.LeatherArmor = reader.ReadInt32();
      // END Reading all resources

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.Gold = reader.ReadInt32();
      obj.Duration = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
