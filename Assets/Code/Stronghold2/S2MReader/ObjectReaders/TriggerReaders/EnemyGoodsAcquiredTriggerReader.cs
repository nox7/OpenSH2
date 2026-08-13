using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class EnemyGoodsAcquiredTriggerReader : TriggerReader
  {
    public EnemyGoodsAcquiredTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      EnemyGoodsAcquiredTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // BEGIN reading goods bytes
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
      obj.Goods.Wine = reader.ReadInt32();
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
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      // END reading goods bytes


      obj.Lord = reader.ReadInt32() switch
      {
        -1 => S2MLords.AllLords,
        0 => S2MLords.Player,
        1 => S2MLords.Olaf, // 1 is unused in this trigger. Default to Olaf.
        2 => S2MLords.Olaf,
        3 => S2MLords.LordBarclay,
        4 => S2MLords.TheHawk,
        5 => S2MLords.TheBull,
        6 => S2MLords.LadySeren,
        7 => S2MLords.Edwin,
        8 => S2MLords.TheKing,
        9 => S2MLords.SirWilliam,
        10 => S2MLords.SirGrey,
        _ => S2MLords.Olaf
      };

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
