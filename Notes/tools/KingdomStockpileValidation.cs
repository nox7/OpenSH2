using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.Construction;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
public static class KingdomStockpileValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    public static int Main()
    {
        try
        {
            var map=new S2MFile{MapSize=64};
            Set(map,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});
            Set(map,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
            var keep=new Keep();Set(keep,"Id",10);Set(keep,"Type","Keep");Set(keep,"RawX",32*1024f);Set(keep,"RawZ",32*1024f);Set(keep,"FootprintWidth",7);Set(keep,"FootprintHeight",7);
            Set(map,"Keeps",new[]{keep});Set(map,"BuildingPlacements",new List<BuildingPlacement>{keep}.AsReadOnly());
            var world=new AiWorld();world.Add(new AiTerritory(2,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("test",1),0,AiConstructionPolicy.Allowed));world.UpsertBuilding(new AiBuilding(10,2,"Keep"));
            var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);var inventory=new EstateInventory();
            using var service=new S2MBuildingConstructionService(map,world,inventory,nav);
            using var policy=new S2MKingdomStockpileConstruction(map,world,inventory,Install,nav,construction:service);
            using var clock=new AiScheduler(world,null,planner:new GeneralKingdomPlanner(policy)); clock.PlannerFaulted+=(_,e)=>Console.WriteLine(e);policy.ConstructionFailed+=(_,e)=>Console.WriteLine(e);
            world.SetControl(2,new AiFaction("test",1),AiController.Human);
            for(int i=0;i<50;i++){clock.Advance(.1);policy.CommitPending(clock.Now);}
            Check(policy.CompletedCount==0,"AI built in a human kingdom");
            world.SetControl(2,new AiFaction("test",1),AiController.Autonomous);
            for(int i=0;i<1000;i++){clock.Advance(.1);policy.CommitPending(clock.Now);}
            Check(policy.CompletedCount==1 && world.Territories[2].BuildingCount("StockPile")==1,"Missing or duplicate free kingdom stockpile tests="+policy.CandidateTests+" pending="+policy.PendingCount);
            var p=map.BuildingPlacements.Last();Check(policy.KeepCanReach(p),"Keep cannot reach aisle");
            var locator=new StockpileLocator(inventory,nav,new S2MStorageAccessProvider(map,nav));
            var from=new MapPoint(10.5f,10.5f);Check(locator.TryNearest(2,Goods.Stone,from,out int found,out _) && found==p.Id,"Automatic storage discovery failed");
            Check(!locator.TryNearest(3,Goods.Stone,from,out _,out _),"Foreign estate store selected");
            var definition=S2MBuildingDefinition.Load(Install,"StockPile");var second=definition.Propose(2,12,12);Set(second,"Id",99);
            Check(!service.Placement.Check(definition.Propose(2,(int)p.MapX,(int)p.MapZ)).Allowed,"Occupied preview accepted");
            inventory.Register(99,2,StorageKind.Stockpile);Set(map,"BuildingPlacements",new List<BuildingPlacement>(map.BuildingPlacements){second}.AsReadOnly());
            Check(locator.TryNearest(2,Goods.Stone,from,out found,out _) && found==99,"Runtime registry addition not discovered");
            var store=inventory.Estate(2).Buildings.Single(b=>b.BuildingId==99);inventory.TryDeposit(99,2,Goods.Stone,(int)store.FreeCapacity(Goods.Stone));
            Check(locator.TryNearest(2,Goods.Stone,from,out found,out _) && found==p.Id,"Full store not skipped");
            for(int turn=0;turn<4;turn++)
            {
                var proposal=definition.Propose(2,48,48,turn);Set(proposal,"Id",100+turn);
                using(var occupancy=S2MBuildingNavigation.Register(nav,proposal))
                {Check(S2MBuildingNavigation.StockpileAccess(proposal,nav).Count>0,"Rotated aisle blocked");Check(!nav.IsWalkable(S2MBuildingNavigation.Point(proposal,.5f,.5f)),"Storage bay walkable");}
                Check(nav.IsWalkable(S2MBuildingNavigation.Point(proposal,.5f,.5f)),"Navigation lease leaked");
            }
            var cursor=definition.Propose(2,5,45);int buildingCount=map.BuildingPlacements.Count;
            Check(service.Placement.Check(cursor).Allowed && map.BuildingPlacements.Count==buildingCount,"Preview mutated buildings");
            Check(service.TrySubmit(new BuildingConstructionRequest{Definition=definition,Placement=cursor,ValidateIntent=()=>true},out var command),"Generic command rejected valid intent");
            Set(cursor,"RawX",55*1024f);command.Advance();
            Check(command.Status==BuildingConstructionStatus.Completed && command.Building.MapX==5,"Accepted command followed mutable placement cursor");
            Console.WriteLine("PASS: kingdom stockpile-first placement, free construction, no duplicates, keep access, runtime central discovery, same-estate/nearest/full-store filtering, read-only occupancy preview and four rotated aisle obstacles.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
