using System;
using System.Collections.Generic;
namespace Assets.Code.Simulation.Economy
{
  // Explicit zero-based indices in GoodsList.md. Unknown entries deliberately have no enum member.
  public enum Goods { Wood=0,Stone=1,Iron=2,Wheat=3,Flour=4,Hops=5,Ale=6,Grapes=7,Pitch=8,Candles=9,Wool=10,Cloth=11,
    Eels=13,Geese=14,Pigs=16,Vegetables=17,Wine=18,Apples=21,Bread=22,Cheese=23,Meat=24,
    Bows=29,Crossbows=30,Swords=31,Maces=32,Pikes=33,Spears=34,MetalArmor=35,LeatherArmor=36 }
  public enum StorageKind { Stockpile,Pantry,Granary,Armory }
  [Serializable] public sealed class PalletCapacityOverride { public Goods Goods;public int Capacity; }
  [Serializable] public sealed class StorageSettings
  {
    public int StockpilePallets=30;
    public PalletCapacityOverride[] PalletCapacities=Array.Empty<PalletCapacityOverride>();
  }
  public sealed class GoodsDefinition
  {
    public Goods Goods { get; }
    public StorageKind Storage { get; }
    public int PalletCapacity { get; }
    internal GoodsDefinition(Goods goods,StorageKind storage,int capacity){Goods=goods;Storage=storage;PalletCapacity=capacity;}
  }
  public sealed class GoodsCatalog
  {
    private readonly GoodsDefinition[] entries=new GoodsDefinition[37];
    private readonly List<GoodsDefinition> all=new();
    public IReadOnlyList<GoodsDefinition> All { get; }
    public GoodsCatalog(StorageSettings settings=null)
    {
      if(settings?.PalletCapacities!=null)foreach(var item in settings.PalletCapacities)
        if(item!=null && (!Enum.IsDefined(typeof(Goods),item.Goods) || item.Capacity<=0))throw new ArgumentException("Invalid pallet capacity override.");
      foreach(Goods goods in Enum.GetValues(typeof(Goods)))
      {
        int id=(int)goods;
        var kind=id<=11?StorageKind.Stockpile:id<=18?StorageKind.Pantry:id<=24?StorageKind.Granary:StorageKind.Armory;
        int capacity=goods switch {Goods.Wood or Goods.Iron=>36,Goods.Stone=>96,Goods.Wheat or Goods.Flour=>32,Goods.Grapes=>15,Goods.Cloth=>25,_=>16};
        if(settings?.PalletCapacities!=null)foreach(var item in settings.PalletCapacities)
          if(item!=null && item.Goods==goods) {if(item.Capacity<=0)throw new ArgumentOutOfRangeException(nameof(settings));capacity=item.Capacity;}
        entries[id]=new GoodsDefinition(goods,kind,kind==StorageKind.Stockpile?capacity:0);all.Add(entries[id]);
      }
      All=all.AsReadOnly();
    }
    public GoodsDefinition this[Goods goods] => (uint)(int)goods<37 && entries[(int)goods]!=null?entries[(int)goods]:throw new ArgumentOutOfRangeException(nameof(goods));
  }
}
