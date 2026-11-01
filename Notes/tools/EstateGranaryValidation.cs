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

public static class EstateGranaryValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    sealed class Prepared : IPreparedAiBuilding
    {
        public bool Fail,Shown,Disposed;
        public void Show(){if(Fail)throw new Exception("controlled presentation failure");Shown=true;}
        public void Dispose()=>Disposed=true;
    }
    sealed class Fixture : IDisposable
    {
        public S2MFile Map;
        public AiWorld World=new();
        public EstateInventory Inventory=new();
        public NavigationGrid Nav;
        public LaborManager Labor;
        public S2MEstateGranaryConstruction Build;
        public AiScheduler Clock;
        public Fixture(AiConstructionPolicy policy=AiConstructionPolicy.Allowed,bool occupied=false,bool blocked=false,int budget=128,AiTerritoryKind kind=AiTerritoryKind.Estate)
        {
            Map=new S2MFile{MapSize=64};
            Set(Map,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});
            Set(Map,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
            var marker=new EstateMarkerFlag();Set(marker,"EstateId",2);Set(marker,"Type",EstateType.Estate);Set(marker,"X",32);Set(marker,"Y",32);Set(Map,"EstateMarkers",new[]{marker});
            var flag=Building(1,"VillageFlagPole",32,32,1);var stock=Building(2,"StockPile",24,35,7,5);
            var buildings=new List<BuildingPlacement>{flag,stock};if(occupied)buildings.Add(Building(3,"Granary",40,40,3));
            Set(Map,"BuildingPlacements",buildings.AsReadOnly());
            World.Add(new AiTerritory(2,kind,AiController.Autonomous,new AiFaction("test",1),constructionPolicy:policy));
            foreach(var b in buildings)World.UpsertBuilding(new AiBuilding(b.Id,2,b.Type));
            Inventory.Register(2,2,StorageKind.Stockpile);
            if(occupied)Inventory.Register(3,2,StorageKind.Granary);
            Nav=new NavigationGrid(256,256,4,Enumerable.Repeat(!blocked,256*256).ToArray(),new float[256*256],.4f);
            var initial=buildings.Select(b=>new LaborBuilding(b.Id,2,Array.Empty<LaborJob>())).ToArray();
            Labor=new LaborManager(Array.Empty<LaborJob>(),Array.Empty<PeasantRecruitment>(),Nav,new PopulationSettings(),initial);
            Build=new S2MEstateGranaryConstruction(Map,World,Inventory,Install,Nav,Labor,settings:new EstateGranarySettings{SearchRadius=4,RetrySeconds=1});
            Clock=new AiScheduler(World,new AiInventoryView(Inventory),new AiSettings{WorkUnitsPerEvaluation=budget},Build);
        }
        public void Tick(int count=100,bool commit=true)
        {for(int i=0;i<count;i++){Clock.Advance(.1);if(commit)Build.CommitPending(Clock.Now);}}
        public BuildingPlacement Granary=>Map.BuildingPlacements.Single(b=>b.Type=="Granary");
        public void Dispose(){Build.Dispose();Clock.Dispose();}
    }
    static BuildingPlacement Building(int id,string type,int x,int z,int w,int h=0)
    {
        var b=new BuildingPlacement();Set(b,"Id",id);Set(b,"Type",type);Set(b,"StartupEstateId",(int?)2);
        Set(b,"RawX",x*1024f);Set(b,"RawZ",z*1024f);Set(b,"FootprintWidth",w);Set(b,"FootprintHeight",h==0?w:h);return b;
    }
    public static int Main()
    {
        try
        {
            Check(AiInfrastructureValidation.Main()==0,"AI infrastructure regression");
            using(var f=new Fixture(budget:1))
            {
                var view=new Prepared();f.Build.PreparePresentation=_=>view;
                for(int i=0;i<120;i++){long before=f.Build.CandidateTests;f.Clock.Advance(.1);Check(f.Build.CandidateTests-before<=1,"Search exceeded work budget");f.Build.CommitPending(f.Clock.Now);}
                Check(f.Build.CompletedCount==1 && f.Map.BuildingPlacements.Count==3,"Granary missing/duplicated");
                var p=f.Granary;
                Check(p.MapX==35 && p.MapZ==32,"Expected nearest site east of pole apron");
                Check(p.RuntimeEstateId==2 && !p.IsStartupGenerated && p.IsRuntimeGenerated,"Wrong runtime ownership/source");
                Check(view.Shown && f.Inventory.TryDeposit(p.Id,2,Goods.Apples,10),"Presentation/storage integration");
                Check(f.Inventory.Amount(2,Goods.Apples)==10 && !f.Inventory.TryDeposit(p.Id,3,Goods.Apples,1),"Estate isolation");
                Check(!f.Nav.IsWalkable(new MapPoint(p.MapX+.5f,p.MapZ+.5f)) && f.Nav.IsWalkable(new MapPoint(p.MapX-.5f,p.MapZ+.5f)),"Logical navigation footprint/clearance");
                Check(f.Labor.TryGetBuilding(p.Id,out var labor) && labor.MaximumWorkers==0 && labor.IsFullyStaffed,"Passive staffing not registered");
                Check(f.World.Territories[2].BuildingCount("Granary")==1,"AI index not updated");
                f.Tick();Check(f.Build.CompletedCount==1,"Repeated economy tick duplicated granary");
            }
            foreach(var policy in new[]{AiConstructionPolicy.Unknown,AiConstructionPolicy.UseMapBuildings})
                using(var f=new Fixture(policy)){f.Tick();Check(f.Build.CompletedCount==0 && f.Build.CandidateTests==0,"Map policy ignored");}
            using(var f=new Fixture(occupied:true)){f.Tick();Check(f.Build.CompletedCount==0 && f.Build.CandidateTests==0,"Existing granary ignored");}
            using(var f=new Fixture(kind:AiTerritoryKind.Kingdom)){f.Tick();Check(f.Build.CandidateTests==0,"Estate policy applied to a kingdom");}
            using(var f=new Fixture())
            {
                f.Tick(commit:false);Check(f.Build.PendingCount==1,"Pending intent duplicated");
                f.World.SetControl(2,new AiFaction("test",1),AiController.Human);
                f.Build.CommitPending(f.Clock.Now);Check(f.Build.CompletedCount==0,"Authority changed before commit");
            }
            using(var f=new Fixture())
            {
                f.Tick(commit:false);var other=Building(9,"Granary",40,40,3);
                Set(f.Map,"BuildingPlacements",f.Map.BuildingPlacements.Concat(new[]{other}).ToArray());
                f.Build.CommitPending(f.Clock.Now);Check(f.Build.CompletedCount==0 && f.World.Territories[2].BuildingCount("Granary")==1,"Late external granary duplicated");
            }
            using(var f=new Fixture(blocked:true,budget:1))
            {f.Tick(2000);Check(f.Build.CompletedCount==0 && f.Build.PendingCount==0 && f.Clock.PlannerFailureCount==0 && f.Build.CandidateTests>81,"Blocked map did not retry gracefully");}
            using(var f=new Fixture())
            {
                f.Tick(commit:false);Set(f.World.Territories[2],"ConstructionPolicy",AiConstructionPolicy.UseMapBuildings);
                f.Build.CommitPending(f.Clock.Now);Check(f.Build.CompletedCount==0,"Changed map policy not revalidated");
            }
            using(var f=new Fixture())
            {
                var troop=new ActorPlacement();Set(troop,"Id",30);Set(troop,"Type","Archer");Set(troop,"IsSoldier",true);Set(troop,"OnMap",true);
                Set(troop,"RawX",36.5f*1024);Set(troop,"RawZ",32.5f*1024);Set(troop,"RawY",2048f);Set(troop,"NavigationLayer",(byte)1);Set(troop,"PendingPlayer",1);
                Set(f.Map,"Actors",new[]{troop});f.Tick();
                var p=f.Granary;float dx=p.MapX+3-36.5f,dz=p.MapZ+3-32.5f;
                Check(dx*dx+dz*dz>=49,"Friendly wall troop ignored by granary placement");
            }
            using(var f=new Fixture())
            {
                var view=new Prepared{Fail=true};f.Build.PreparePresentation=_=>view;f.Tick();
                Check(f.Build.CompletedCount==0 && f.Map.BuildingPlacements.Count==2 && f.Inventory.Buildings.Count==1 && f.Labor.Buildings.Count==2
                    && f.World.Territories[2].BuildingCount("Granary")==0 && view.Disposed,"Commit rollback incomplete");
                Check(f.Nav.IsWalkable(new MapPoint(35.5f,32.5f)),"Rollback left navigation blocked");
            }
            Console.WriteLine("PASS: budgeted estate granary construction; policy/existing-store guards; exact placement/aprons; one completed store; inventory/navigation/staffing; capture and late-store cancellation; no-site retry; presentation rollback.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
