using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class TurnIndustriesOnOffActionReader : ActionReader
  {
    public TurnIndustriesOnOffActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      TurnIndustriesOnOffAction obj = new();

      ReadActionHeader(reader);

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.EstateSetting = reader.ReadInt32();
      obj.FlagColor = (FlagColor)reader.ReadInt32();
      obj.FlagNumber = reader.ReadInt32();

      obj.Lord = reader.ReadInt32() switch
      {
        1 => S2MLords.Player,
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

      // BEGIN resource bits
      reader.ReadByte();
      obj.Industries.Wood = reader.ReadBoolean();
      obj.Industries.Stone = reader.ReadBoolean();
      obj.Industries.Iron = reader.ReadBoolean();
      obj.Industries.Wheat = reader.ReadBoolean();
      obj.Industries.Flour = reader.ReadBoolean();
      obj.Industries.Hops = reader.ReadBoolean();
      obj.Industries.Ale = reader.ReadBoolean();
      obj.Industries.Grapes = reader.ReadBoolean();
      obj.Industries.Pitch = reader.ReadBoolean();
      obj.Industries.Candles = reader.ReadBoolean();
      obj.Industries.Wool = reader.ReadBoolean();
      obj.Industries.Cloth = reader.ReadBoolean();
      reader.ReadByte();
      obj.Industries.Eels = reader.ReadBoolean();
      obj.Industries.Geese = reader.ReadBoolean();
      reader.ReadByte();
      obj.Industries.Pigs = reader.ReadBoolean();
      obj.Industries.Vegetables = reader.ReadBoolean();
      obj.Industries.Wine = reader.ReadBoolean();
      reader.ReadByte();
      reader.ReadByte();
      obj.Industries.Apples = reader.ReadBoolean();
      obj.Industries.Bread = reader.ReadBoolean();
      obj.Industries.Cheese = reader.ReadBoolean();
      obj.Industries.Meat = reader.ReadBoolean();
      reader.ReadByte();
      reader.ReadByte();
      reader.ReadByte();
      reader.ReadByte();
      obj.Industries.Bows = reader.ReadBoolean();
      obj.Industries.Crossbows = reader.ReadBoolean();
      obj.Industries.Swords = reader.ReadBoolean();
      obj.Industries.Maces = reader.ReadBoolean();
      obj.Industries.Pikes = reader.ReadBoolean();
      obj.Industries.Spears = reader.ReadBoolean();
      obj.Industries.MetalArmor = reader.ReadBoolean();
      obj.Industries.LeatherArmor = reader.ReadBoolean();
      reader.ReadBytes(7);
      // END resource bits

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
