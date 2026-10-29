using System;
using System.Collections.Generic;
namespace Assets.Code.Simulation.Economy
{
  public readonly struct Pallet
  {
    public Goods? Goods { get; }
    public int Amount { get; }
    internal Pallet(Goods goods,int amount){Goods=goods;Amount=amount;}
  }
  public sealed class EstateResources
  {
    public int EstateId { get; }
    private readonly List<BuildingStorage> buildings=new();
    internal readonly long[] Totals=new long[37];
    public IReadOnlyList<BuildingStorage> Buildings { get; }
    private readonly GoodsCatalog catalog;
    internal EstateResources(int id,GoodsCatalog catalog){EstateId=id;this.catalog=catalog;Buildings=buildings.AsReadOnly();}
    internal void Add(BuildingStorage building)=>buildings.Add(building);
    internal void Remove(BuildingStorage building)=>buildings.Remove(building);
    public long this[Goods goods] {get{_=catalog[goods];return Totals[(int)goods];}}
  }
  public sealed class BuildingStorage
  {
    public int BuildingId { get; }
    public int EstateId { get; }
    public StorageKind Kind { get; }
    public int PalletCount => pallets.Length;
    public long Revision { get; internal set; }
    private readonly Pallet[] pallets;
    private readonly long[] quantities=new long[37];
    private readonly GoodsCatalog catalog;
    internal BuildingStorage(int id,int estate,StorageKind kind,int slots,GoodsCatalog catalog)
    {BuildingId=id;EstateId=estate;Kind=kind;this.catalog=catalog;pallets=new Pallet[kind==StorageKind.Stockpile?slots:0];}
    public Pallet GetPallet(int index)=>pallets[index];
    public long Amount(Goods goods){_=catalog[goods];return quantities[(int)goods];}
    public long FreeCapacity(Goods goods)
    {
      var definition=catalog[goods];if(definition.Storage!=Kind)return 0;
      if(Kind!=StorageKind.Stockpile)return long.MaxValue-Amount(goods);
      long free=0;foreach(var pallet in pallets)
        if(!pallet.Goods.HasValue || pallet.Goods==goods)free+=definition.PalletCapacity-pallet.Amount;
      return free;
    }
    public bool CanAccept(Goods goods,int amount)=>amount>0 && FreeCapacity(goods)>=amount;
    internal void Change(Goods goods,int delta)
    {
      if(Kind==StorageKind.Stockpile)
      {
        int remaining=Math.Abs(delta),capacity=catalog[goods].PalletCapacity;
        if(delta>0)
        {
          // Pack existing partial pallets before claiming an empty one. Never mix goods.
          for(int pass=0;pass<2 && remaining>0;pass++)for(int i=0;i<pallets.Length && remaining>0;i++)
          {
            var slot=pallets[i];if(pass==0?slot.Goods!=goods:slot.Goods.HasValue)continue;
            int added=Math.Min(remaining,capacity-slot.Amount);if(added==0)continue;
            pallets[i]=new Pallet(goods,slot.Amount+added);remaining-=added;
          }
        }
        else for(int i=pallets.Length-1;i>=0 && remaining>0;i--)
        {
          var slot=pallets[i];if(slot.Goods!=goods)continue;
          int taken=Math.Min(remaining,slot.Amount),left=slot.Amount-taken;
          pallets[i]=left==0?default:new Pallet(goods,left);remaining-=taken;
        }
        if(remaining!=0)throw new InvalidOperationException("Unvalidated storage mutation.");
      }
      quantities[(int)goods]+=delta;Revision++;
    }
  }
  /// <summary>Simulation-thread inventory authority. Queries are O(1); mutations publish after totals and pallets agree.</summary>
  public sealed class EstateInventory
  {
    public GoodsCatalog Catalog { get; }
    public int DefaultStockpilePallets { get; }
    private readonly Dictionary<int,EstateResources> estates=new();
    private readonly Dictionary<int,BuildingStorage> byId=new();
    private readonly List<BuildingStorage> stores=new();
    public IReadOnlyList<BuildingStorage> Buildings { get; }
    public event Action<BuildingStorage> Changed;
    public StockpileDeliverySettings DeliverySettings { get; }
    public EstateInventory(StorageSettings settings=null,StockpileDeliverySettings deliverySettings=null){DeliverySettings=deliverySettings??new StockpileDeliverySettings();DeliverySettings.Validate();DefaultStockpilePallets=settings?.StockpilePallets??30;if(DefaultStockpilePallets<1)throw new ArgumentOutOfRangeException(nameof(settings));Catalog=new GoodsCatalog(settings);Buildings=stores.AsReadOnly();}
    public EstateResources Estate(int id)
    {
      if(id<0)throw new ArgumentOutOfRangeException(nameof(id));
      if(!estates.TryGetValue(id,out var estate))estates.Add(id,estate=new EstateResources(id,Catalog));return estate;
    }
    public BuildingStorage Register(int id,int estateId,StorageKind kind,int palletCount=0)
    {
      if(palletCount==0)palletCount=DefaultStockpilePallets;
      if(palletCount<1 || !Enum.IsDefined(typeof(StorageKind),kind))throw new ArgumentOutOfRangeException();
      if(byId.ContainsKey(id))throw new ArgumentException("Building already registered.");
      var estate=Estate(estateId);var store=new BuildingStorage(id,estateId,kind,palletCount,Catalog);
      byId.Add(id,store);stores.Add(store);estate.Add(store);return store;
    }
    public bool TryGetBuilding(int id,out BuildingStorage store)=>byId.TryGetValue(id,out store);
    public long Amount(int estate,Goods goods){_=Catalog[goods];return estates.TryGetValue(estate,out var e)?e[goods]:0;}
    public bool CanDeposit(int building,int estate,Goods goods,int amount)
      =>byId.TryGetValue(building,out var store) && store.EstateId==estate && store.CanAccept(goods,amount)
        && long.MaxValue-Amount(estate,goods)>=amount;
    public bool TryDeposit(int building,int estate,Goods goods,int amount)
    {
      if(!CanDeposit(building,estate,goods,amount))return false;
      var store=byId[building];store.Change(goods,amount);estates[estate].Totals[(int)goods]+=amount;Changed?.Invoke(store);return true;
    }
    public bool TryWithdraw(int building,Goods goods,int amount)
    {
      if(amount<=0 || !byId.TryGetValue(building,out var store) || store.Amount(goods)<amount)return false;
      store.Change(goods,-amount);estates[store.EstateId].Totals[(int)goods]-=amount;Changed?.Invoke(store);return true;
    }
    public bool TryTransfer(int from,int to,Goods goods,int amount)
    {
      if(from==to || !byId.TryGetValue(from,out var source) || !byId.TryGetValue(to,out var target) || amount<=0
        || source.Amount(goods)<amount || !target.CanAccept(goods,amount)
        || (source.EstateId!=target.EstateId && !CanDeposit(to,target.EstateId,goods,amount)))return false;
      source.Change(goods,-amount);target.Change(goods,amount);
      estates[source.EstateId].Totals[(int)goods]-=amount;estates[target.EstateId].Totals[(int)goods]+=amount;
      Changed?.Invoke(source);Changed?.Invoke(target);return true;
    }
    public bool TryRemoveEmptyBuilding(int id)
    {
      if(!byId.TryGetValue(id,out var store))return false;
      foreach(var goods in Catalog.All)if(store.Amount(goods.Goods)!=0)return false;
      byId.Remove(id);stores.Remove(store);estates[store.EstateId].Remove(store);Changed?.Invoke(store);return true;
    }
  }
}
