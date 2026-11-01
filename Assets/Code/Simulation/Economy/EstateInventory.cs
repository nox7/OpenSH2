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
    private readonly Dictionary<(int Building,Goods Goods),int> reserved=new();
    private readonly Dictionary<(int Estate,Goods Goods),int> estateReserved=new();
    private readonly HashSet<int> initialWoodGranted=new();
    public long AvailableAmount(int estate,Goods goods)=>Amount(estate,goods)-(estateReserved.TryGetValue((estate,goods),out int n)?n:0);
    private long Available(BuildingStorage store,Goods goods)=>store.Amount(goods)-(reserved.TryGetValue((store.BuildingId,goods),out int n)?n:0);
    public bool TryGrantInitialWood(int estate,int amount)
    {
      if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));
      if(initialWoodGranted.Contains(estate))return false;
      var candidates=Estate(estate).Buildings;long space=0;foreach(var store in candidates)space+=Math.Min(amount,store.FreeCapacity(Goods.Wood));
      if(space<amount)return false;
      var changed=new List<BuildingStorage>();int left=amount;
      foreach(var store in candidates)
      {int take=(int)Math.Min(left,store.FreeCapacity(Goods.Wood));if(take<=0)continue;store.Change(Goods.Wood,take);left-=take;changed.Add(store);if(left==0)break;}
      estates[estate].Totals[(int)Goods.Wood]+=amount;initialWoodGranted.Add(estate);foreach(var store in changed)Changed?.Invoke(store);return true;
    }
    public bool TryReserveEstateGoods(int estate,Goods goods,int amount,out GoodsReservation reservation)
    {
      reservation=null;if(amount<0 || AvailableAmount(estate,goods)<amount)return false;
      Estate(estate);
      var parts=new List<(BuildingStorage Store,int Amount)>();int left=amount;
      foreach(var store in estates[estate].Buildings)
      {int take=(int)Math.Min(left,Available(store,goods));if(take<=0)continue;parts.Add((store,take));left-=take;if(left==0)break;}
      if(left!=0)return false;
      foreach(var p in parts){var key=(p.Store.BuildingId,goods);reserved[key]=(reserved.TryGetValue(key,out int n)?n:0)+p.Amount;}
      var estateKey=(estate,goods);estateReserved[estateKey]=(estateReserved.TryGetValue(estateKey,out int held)?held:0)+amount;
      reservation=new GoodsReservation(this,estate,goods,amount,parts);return true;
    }
    public sealed class GoodsReservation : IDisposable
    {
      private EstateInventory owner;private readonly int estate,amount;private readonly Goods goods;
      private readonly List<(BuildingStorage Store,int Amount)> parts;
      internal GoodsReservation(EstateInventory owner,int estate,Goods goods,int amount,List<(BuildingStorage,int)> parts)
      {this.owner=owner;this.estate=estate;this.goods=goods;this.amount=amount;this.parts=parts;}
      public void Commit()
      {
        if(owner==null)throw new ObjectDisposedException(nameof(GoodsReservation));
        var current=owner;foreach(var p in parts)p.Store.Change(goods,-p.Amount);current.estates[estate].Totals[(int)goods]-=amount;
        Release();foreach(var p in parts)current.Changed?.Invoke(p.Store);
      }
      private void Release()
      {
        foreach(var p in parts){var key=(p.Store.BuildingId,goods);int n=owner.reserved[key]-p.Amount;if(n==0)owner.reserved.Remove(key);else owner.reserved[key]=n;}
        var e=(estate,goods);int remaining=owner.estateReserved[e]-amount;if(remaining==0)owner.estateReserved.Remove(e);else owner.estateReserved[e]=remaining;
        owner=null;
      }
      public void Dispose(){if(owner!=null)Release();}
    }
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
      if(amount<=0 || !byId.TryGetValue(building,out var store) || Available(store,goods)<amount)return false;
      store.Change(goods,-amount);estates[store.EstateId].Totals[(int)goods]-=amount;Changed?.Invoke(store);return true;
    }
    public bool TryTransfer(int from,int to,Goods goods,int amount)
    {
      if(from==to || !byId.TryGetValue(from,out var source) || !byId.TryGetValue(to,out var target) || amount<=0
        || Available(source,goods)<amount || !target.CanAccept(goods,amount)
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
