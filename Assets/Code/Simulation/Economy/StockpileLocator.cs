using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
namespace Assets.Code.Simulation.Economy
{
    public interface IStorageAccessProvider { IReadOnlyList<MapPoint> AccessPoints(int buildingId); }
    /// <summary>Shared delivery query over the authoritative estate inventory. Consumers retain only a selected target, not store lists.</summary>
    public class StorageLocator
    {
        private readonly EstateInventory inventory;private readonly NavigationGrid navigation;private readonly IStorageAccessProvider geometry;private readonly StorageKind kind;
        public StorageLocator(EstateInventory inventory,NavigationGrid navigation,IStorageAccessProvider geometry,StorageKind kind)
        {this.inventory=inventory;this.navigation=navigation;this.geometry=geometry;this.kind=kind;}
        public bool HasCapacity(int estate,Goods goods)
        {
            foreach(var store in inventory.Estate(estate).Buildings)
                if(store.Kind==kind && inventory.CanDeposit(store.BuildingId,estate,goods,1) && geometry.AccessPoints(store.BuildingId).Count>0)return true;
            return false;
        }
        public bool CanReachAny(int estate,Goods goods,MapPoint from)
        {
            foreach(var store in inventory.Estate(estate).Buildings)
            {
                if(store.Kind!=kind || !inventory.CanDeposit(store.BuildingId,estate,goods,1))continue;
                foreach(var point in geometry.AccessPoints(store.BuildingId))if(navigation.CanReach(from,point))return true;
            }
            return false;
        }
        public NavigationGrid.PathRequest RequestNearest(int estate,Goods goods,MapPoint from,int amount=1)
        {
            var targets=new List<PathTarget>();
            foreach(var store in inventory.Estate(estate).Buildings)
                if(store.Kind==kind && inventory.CanDeposit(store.BuildingId,estate,goods,amount))
                    foreach(var point in geometry.AccessPoints(store.BuildingId))targets.Add(new PathTarget(store.BuildingId,point));
            return navigation.PathRequests.Request(from,targets);
        }
        public bool TryNearest(int estate,Goods goods,MapPoint from,out int building,out IReadOnlyList<MapPoint> route,int amount=1)
        {
            building=0;route=null;float best=float.PositiveInfinity;
            foreach(var store in inventory.Estate(estate).Buildings)
            {
                if(store.Kind!=kind || !inventory.CanDeposit(store.BuildingId,estate,goods,amount))continue;
                foreach(var point in geometry.AccessPoints(store.BuildingId))
                {
                    if(MapPoint.Distance(from,point)>best || !navigation.CanReach(from,point) || !navigation.TryFindPath(from,point,out var path))continue;
                    float length=0;for(int i=1;i<path.Count;i++)length+=MapPoint.Distance(path[i-1],path[i]);
                    if(length>best || length==best && store.BuildingId>=building)continue;
                    best=length;building=store.BuildingId;route=path;
                }
            }
            return route!=null;
        }
    }
    public sealed class StockpileLocator : StorageLocator
    {
        public StockpileLocator(EstateInventory inventory,NavigationGrid navigation,IStorageAccessProvider geometry)
            :base(inventory,navigation,geometry,StorageKind.Stockpile){}
    }
}
