// Headless integration checks for estate housing, material accounting and staffing facts.
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;
public static class EstateHousingValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool v,string why){if(!v)throw new Exception(why);}
    static void Set(object o,string p,object v)=>o.GetType().GetProperty(p).SetValue(o,v);
    sealed class Job : LaborJob
    { public override LaborRole Role=>LaborRole.Woodcutter;public Job(int id,int estate):base(id,estate,new MapPoint(5,5),new MapPoint(5,5),0,0){} }
    sealed class BadView : IPreparedAiBuilding {public void Show()=>throw new Exception("controlled failure");public void Dispose(){} }
    sealed class Fixture : IDisposable
    {
        public S2MFile Map=new(){MapSize=64};public AiWorld World=new();public EstateInventory Inventory=new();
        public NavigationGrid Nav=new(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],.4f);
        public List<LaborJob> Jobs=new();public LaborManager Labor;public S2MEstateHovelConstruction Build;public AiScheduler Clock;
        public PeasantRecruitment Residents;
        public Fixture(int production=15,int workers=0,bool population=false,AiConstructionPolicy policy=AiConstructionPolicy.Allowed,AiTerritoryKind kind=AiTerritoryKind.Estate)
        {
            Set(Map,"SourcePath","test.s2m");Set(Map,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});
            Set(Map,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
            var marker=new EstateMarkerFlag();Set(marker,"EstateId",2);Set(marker,"Type",EstateType.Estate);Set(marker,"X",32);Set(marker,"Y",32);Set(Map,"EstateMarkers",new[]{marker});
            var stock=new BuildingPlacement();Set(stock,"Id",1);Set(stock,"Type","StockPile");Set(stock,"StartupEstateId",(int?)2);Set(stock,"RawX",24*1024f);Set(stock,"RawZ",35*1024f);Set(stock,"FootprintWidth",7);Set(stock,"FootprintHeight",5);
            Set(Map,"BuildingPlacements",new[]{stock});Inventory.Register(1,2,StorageKind.Stockpile);
            World.Add(new AiTerritory(2,kind,AiController.Autonomous,new AiFaction("test",1),production,policy));World.UpsertBuilding(new AiBuilding(1,2,"StockPile"));
            for(int i=0;i<workers;i++)Jobs.Add(new Job(100+i,2));
            var settings=new PopulationSettings();
            var people=Enumerable.Range(0,24).Select(i=>new Peasant(-1-i,0,true,new MapPoint(6,6),new MapPoint(6,5),new MapPoint(6,4),0,0,2,true)).ToArray();
            Residents=new PeasantRecruitment(people,Nav,settings,1);Residents.SetSpawnLimit(0);
            Labor=new LaborManager(Jobs,population?new[]{Residents}:Array.Empty<PeasantRecruitment>(),Nav,settings);
            Build=new S2MEstateHovelConstruction(Map,World,Inventory,Install,Nav,Labor,settings:new EstateHovelSettings{SearchRadius=16,RetrySeconds=1});
            if(population)Build.BuildingCompleted+=_=>Residents.SetSpawnLimit(Residents.SpawnLimit+8);
            Clock=new AiScheduler(World,new AiInventoryView(Inventory),planner:Build);
            S2MEstateHovelConstruction.InitializeEstateResources(Map,World,Inventory);
        }
        public void Tick(int frames=100){for(int i=0;i<frames;i++){Clock.Advance(.1);Build.CommitPending(Clock.Now);}}
        public void Dispose(){Build.Dispose();Clock.Dispose();}
    }
    public static int Main()
    {
        try
        {
            Check(EstateGranaryValidation.Main()==0,"Granary regression");
            for(int code=0;code<20;code++)
            {var t=new AiTerritory(2,AiTerritoryKind.Estate,AiController.Autonomous,productionCode:code);Check(t.MaximumHovels==(code<8?1:code<15?2:3),"Header population tier "+code);}
            using(var f=new Fixture())
            {
                Check(f.Inventory.Amount(2,Goods.Wood)==32,"Starter wood");
                S2MEstateHovelConstruction.InitializeEstateResources(f.Map,f.World,f.Inventory);Check(f.Inventory.Amount(2,Goods.Wood)==32,"Repeated startup minted wood");
                f.Tick();Check(f.Build.CompletedCount==1 && f.World.Territories[2].DesiredHovels==1 && f.Inventory.Amount(2,Goods.Wood)==26,"Idle large estate overbuilt");
                Check(f.Map.BuildingPlacements.Last().HovelAppearance==0,"Runtime hovel lacks appearance");
                Check(f.Labor.GetLaborDemand(2).MissingLaborers==0,"Hovel demanded workers");
            }
            foreach(var item in new[]{(Code:0,Workers:25,Count:1),(Code:8,Workers:25,Count:2),(Code:15,Workers:25,Count:3),(Code:15,Workers:8,Count:1),(Code:15,Workers:9,Count:2),(Code:15,Workers:17,Count:3)})
            using(var f=new Fixture(item.Code,item.Workers))
            {f.Tick(300);Check(f.Build.CompletedCount==item.Count,$"Demand/ceiling code={item.Code} workers={item.Workers}");Check(f.Inventory.Amount(2,Goods.Wood)==32-6*item.Count,"Hovel cost not exactly six");}
            using(var f=new Fixture(workers:8,population:true))
            {
                f.Tick();Check(f.Residents.SpawnLimit==8 && f.Labor.GetLaborSupply(2).Pending==8 && f.Build.CompletedCount==1,"Pending births ignored");
                f.Labor.Advance(100);Check(f.Labor.GetLaborDemand(2).AssignedWorkers==8 && f.Labor.GetLaborSupply(2).Pending==0,"Hovel population failed to staff jobs");
                f.Tick();Check(f.Build.CompletedCount==1,"Travel/arrival caused extra hovel");
                f.Residents.Peasants[0].Worker.Remove();Check(f.Labor.GetLaborDemand(2).MissingLaborers==1,"Worker loss left stale demand");
            }
            using(var f=new Fixture(workers:9,population:true)){f.Tick();Check(f.Build.CompletedCount==2 && f.Residents.SpawnLimit==16,"Actual housing supply did not grow for ninth worker");}
            foreach(var policy in new[]{AiConstructionPolicy.Unknown,AiConstructionPolicy.UseMapBuildings})
                using(var f=new Fixture(policy:policy)){f.Tick();Check(f.Build.CompletedCount==0,"Map AI placement policy ignored");}
            using(var f=new Fixture(kind:AiTerritoryKind.Kingdom)){f.Tick();Check(f.Build.CompletedCount==0 && f.Inventory.Amount(2,Goods.Wood)==0,"Estate policy touched kingdom");}
            using(var f=new Fixture(production:-1)){f.Tick();Check(f.Build.CompletedCount==0,"Unknown header tier guessed");}
            using(var f=new Fixture())
            {
                f.Inventory.TryWithdraw(1,Goods.Wood,32);Set(f.Map,"SourcePath","save.sh2");
                S2MEstateHovelConstruction.InitializeEstateResources(f.Map,f.World,f.Inventory);f.Tick();Check(f.Build.CompletedCount==0 && f.Inventory.Amount(2,Goods.Wood)==0,"No-wood or saved-game guard ignored");
                f.Inventory.TryDeposit(1,2,Goods.Wood,6);f.Tick();Check(f.Build.CompletedCount==1 && f.Inventory.Amount(2,Goods.Wood)==0,"Construction did not resume on resources");
            }
            using(var f=new Fixture())
            {
                f.Build.PreparePresentation=_=>new BadView();f.Tick();
                Check(f.Build.CompletedCount==0 && f.Inventory.Amount(2,Goods.Wood)==32 && f.Inventory.AvailableAmount(2,Goods.Wood)==32 && f.Map.BuildingPlacements.Count==1,"Failed construction spent/reserved wood or left building");
            }
            using(var f=new Fixture())
            using(var blockedGranary=new S2MEstateGranaryConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor,settings:new EstateGranarySettings{SearchRadius=128}))
            {
                blockedGranary.AdditionalPlacementRule=_=>false;
                f.Clock.Dispose();var planner=new EstateConstructionPlanner(blockedGranary,f.Build);
                using var scheduler=new AiScheduler(f.World,new AiInventoryView(f.Inventory),new AiSettings{WorkUnitsPerEvaluation=1},planner);
                for(int i=0;i<100;i++){long before=f.Build.CandidateTests+blockedGranary.CandidateTests;scheduler.Advance(.1);Check(f.Build.CandidateTests+blockedGranary.CandidateTests-before<=1,"Combined search exceeded budget");planner.CommitPending(scheduler.Now);}
                Check(f.Build.CompletedCount==1 && blockedGranary.CompletedCount==0,"No-site granary search starved housing");
            }
            Reservations();Staffing();
            Console.WriteLine("PASS: all 20 population codes; demand-driven 1/2/3 hovel limits; pending births; recruitment/assignment; worker loss; exact 6-wood cost; one-time 32 wood; policy/save guards; reservation isolation and rollback; kingdom/estate staffing totals.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Reservations()
    {
        var inventory=new EstateInventory();inventory.Register(1,2,StorageKind.Stockpile);inventory.Register(2,2,StorageKind.Stockpile);inventory.Register(3,3,StorageKind.Stockpile);
        inventory.TryDeposit(1,2,Goods.Wood,4);inventory.TryDeposit(2,2,Goods.Wood,4);inventory.TryDeposit(3,3,Goods.Wood,100);
        Check(inventory.TryReserveEstateGoods(2,Goods.Wood,6,out var hold),"Multi-stockpile reservation");
        Check(!inventory.TryWithdraw(1,Goods.Wood,1) && !inventory.TryTransfer(1,3,Goods.Wood,1) && !inventory.TryReserveEstateGoods(2,Goods.Wood,6,out _),"Held materials double spent");
        hold.Dispose();Check(inventory.Amount(2,Goods.Wood)==8 && inventory.AvailableAmount(2,Goods.Wood)==8,"Cancelled reservation leaked");
        Check(inventory.TryReserveEstateGoods(2,Goods.Wood,6,out hold),"Second reservation");hold.Commit();hold.Dispose();Check(inventory.Amount(2,Goods.Wood)==2 && inventory.Amount(3,Goods.Wood)==100,"Cross-estate spend or bad total");
    }
    static void Staffing()
    {
        var nav=new NavigationGrid(16,16,1,Enumerable.Repeat(true,256).ToArray(),new float[256],.4f);
        var jobs=new LaborJob[]{new Job(1,2),new Job(1,2),new Job(1,2),new Job(2,3)};
        var buildings=new[]{new LaborBuilding(1,2,jobs.Take(3).ToArray()),new LaborBuilding(2,3,new[]{jobs[3]}),new LaborBuilding(3,2,Array.Empty<LaborJob>()),new LaborBuilding(4,2,Array.Empty<LaborJob>(),false)};
        var labor=new LaborManager(jobs,Array.Empty<PeasantRecruitment>(),nav,new PopulationSettings(),buildings);
        var d=labor.GetLaborDemand(2);Check(d.BuildingsNeedingLabor==1 && d.MissingLaborers==3 && d.UnknownRequirements==1,"Building count confused with worker count");
        var p=new Peasant(-1,0,false,new MapPoint(1,1),new MapPoint(1,1),new MapPoint(1,1),0,0,2);
        Check(p.Worker.TryAssign(jobs[0],new[]{new MapPoint(1,1),new MapPoint(5,5)}),"Worker reservation failed");
        d=labor.GetLaborDemand(2);Check(d.AssignedWorkers==1 && d.MissingLaborers==2 && d.ArrivedWorkers==0,"Travel reservation not counted");
        Check(labor.GetLaborDemand(3).MissingLaborers==1,"Estate labor isolation");
        p.Worker.Remove();Check(labor.GetLaborDemand(2).MissingLaborers==3,"Removal did not update counters");
    }
}
