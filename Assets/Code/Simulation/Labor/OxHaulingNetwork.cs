using System;using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Economy;
namespace Assets.Code.Simulation.Labor
{
  [Serializable] public sealed class OxHaulingSettings
  {
    public float HandlerSpeedCellsPerSecond=.85f,OxSpeedCellsPerSecond=1.5f;
    public float FollowDistance=1,HandlerPickupRadius=2,RetrySeconds=2;
    public float LoadSecondsPerStone=.5f,LoadSecondsPerIron=.25f,OxModelScale=1.25f;
    public int CapacityStone=16,CapacityIron=8;
    public void Validate()
    {
      foreach(float f in new[]{HandlerSpeedCellsPerSecond,OxSpeedCellsPerSecond,FollowDistance,HandlerPickupRadius,RetrySeconds,LoadSecondsPerStone,LoadSecondsPerIron,OxModelScale})
        if(!(f>0) || float.IsInfinity(f))throw new ArgumentOutOfRangeException(nameof(OxHaulingSettings));
      if(CapacityStone<1 || CapacityStone>16 || CapacityIron<1 || CapacityIron>8 || FollowDistance>HandlerPickupRadius || OxSpeedCellsPerSecond<HandlerSpeedCellsPerSecond)throw new ArgumentOutOfRangeException(nameof(OxHaulingSettings));
    }
  }
  public sealed class OxHaulingSource
  {
    public int BuildingId { get; }public int EstateId { get; }public MapPoint Position { get; }public IOxProduction Production { get; }
    public Goods Resource=>Production.Resource;
    public OxHaulingSource(int building,int estate,MapPoint position,IOxProduction production)
    {BuildingId=building;EstateId=estate;Position=position;Production=production;}
  }
  public readonly struct HaulingDestination
  {
    public int BuildingId { get; }public int EstateId { get; }public MapPoint Position { get; }
    public HaulingDestination(int building,int estate,MapPoint position){BuildingId=building;EstateId=estate;Position=position;}
  }
  /// <summary>One map-level registry. Route searches occur on dispatch/retry, not every rendered frame.</summary>
  public sealed class OxHaulingNetwork
  {
    public NavigationGrid Navigation { get; }public EstateInventory Inventory { get; private set; }
    public IReadOnlyList<OxHaulingSource> Sources { get; }public IReadOnlyList<HaulingDestination> Destinations { get; }
    private readonly Dictionary<int,List<OxHaulingSource>> sourcesByEstate=new();
    private readonly Dictionary<int,List<HaulingDestination>> destinationsByEstate=new();
    public OxHaulingNetwork(NavigationGrid navigation,IReadOnlyList<OxHaulingSource> sources,IReadOnlyList<HaulingDestination> destinations)
    {
      Navigation=navigation;Sources=sources;Destinations=destinations;
      foreach(var source in sources){if(!sourcesByEstate.TryGetValue(source.EstateId,out var list))sourcesByEstate.Add(source.EstateId,list=new List<OxHaulingSource>());list.Add(source);}
      foreach(var destination in destinations){if(!destinationsByEstate.TryGetValue(destination.EstateId,out var list))destinationsByEstate.Add(destination.EstateId,list=new List<HaulingDestination>());list.Add(destination);}
    }
    public void ConfigureStorage(EstateInventory inventory)=>Inventory=inventory??throw new ArgumentNullException(nameof(inventory));
    public bool HasStorage(int estate,Goods goods=Goods.Stone)
    {if(!destinationsByEstate.TryGetValue(estate,out var destinations))return false;foreach(var d in destinations)if(Inventory!=null && Inventory.CanDeposit(d.BuildingId,estate,goods,1))return true;return false;}
    public bool TrySource(int handler,int estate,MapPoint from,out OxHaulingSource source,out IReadOnlyList<MapPoint> route,Goods? resource=null)
    {
      source=null;route=null;float best=float.PositiveInfinity;
      if(!sourcesByEstate.TryGetValue(estate,out var sources))return false;
      bool stoneStore=HasStorage(estate,Goods.Stone),ironStore=HasStorage(estate,Goods.Iron);
      foreach(var candidate in sources)
      {
        if(candidate.EstateId!=estate || resource.HasValue && candidate.Resource!=resource.Value || !(candidate.Resource==Goods.Iron?ironStore:stoneStore) || candidate.Production.HaulerId.HasValue && candidate.Production.HaulerId!=handler
          || MapPoint.Distance(from,candidate.Position)>=best || !Navigation.TryFindPath(from,candidate.Position,out var path))continue;
        float length=Length(path);if(length>=best)continue;best=length;source=candidate;route=path;
      }
      return source!=null && source.Production.TryReserveHauler(handler);
    }
    public bool TryDestination(int estate,MapPoint from,out int building,out IReadOnlyList<MapPoint> route,Goods goods=Goods.Stone)
    {
      building=0;route=null;float best=float.PositiveInfinity;
      if(!destinationsByEstate.TryGetValue(estate,out var destinations))return false;
      foreach(var d in destinations)
      {
        if(d.EstateId!=estate || Inventory==null || !Inventory.CanDeposit(d.BuildingId,estate,goods,1) || MapPoint.Distance(from,d.Position)>=best
          || !Navigation.TryFindPath(from,d.Position,out var path))continue;
        float length=Length(path);if(length>=best)continue;best=length;route=path;building=d.BuildingId;
      }
      return route!=null;
    }
    internal static float Length(IReadOnlyList<MapPoint> path)
    {float length=0;for(int i=1;i<path.Count;i++)length+=MapPoint.Distance(path[i-1],path[i]);return length;}
  }
}
