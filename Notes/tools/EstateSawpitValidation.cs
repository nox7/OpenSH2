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
public static class EstateSawpitValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static void Set(object o,string p,object value)=>o.GetType().GetProperty(p).SetValue(o,value);
    sealed class Job : LaborJob
    {public Job(int id,int estate):base(id,estate,new MapPoint(5,5),new MapPoint(5,5),0,0){}public override LaborRole Role=>LaborRole.Woodcutter;}
    public sealed class Prepared : IPreparedAiLabor
    {public LaborBuilding Building{get;}public bool Fail,Disposed;public Prepared(int id,int estate)=>Building=new LaborBuilding(id,estate,new[]{new Job(id,estate)});public void Show(){if(Fail)throw new Exception("controlled failure");}public void Dispose()=>Disposed=true;}
    public sealed class Fixture : IDisposable
    {
        public S2MFile Map=new(){MapSize=64};public AiWorld World=new();public EstateInventory Inventory=new();
        public NavigationGrid Nav=new(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],.4f);
        public LaborManager Labor;public PeasantRecruitment Recruits;public S2MEstateSawpitConstruction Build;public AiScheduler Clock;
        public Fixture(int code=3,int residents=8,int initialVacancies=0,int wood=32,bool housing=true,AiConstructionPolicy policy=AiConstructionPolicy.Allowed,AiTerritoryKind kind=AiTerritoryKind.Estate)
        {
            Set(Map,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});Set(Map,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
            var marker=new EstateMarkerFlag();Set(marker,"EstateId",2);Set(marker,"Type",EstateType.Estate);Set(marker,"X",32);Set(marker,"Y",32);Set(Map,"EstateMarkers",new[]{marker});
            var list=new List<BuildingPlacement>{Building(1,"StockPile",20,20,7,5)};if(housing)list.Add(Building(2,"Hovel",40,40,2,3));Set(Map,"BuildingPlacements",list.AsReadOnly());
            World.Add(new AiTerritory(2,kind,AiController.Autonomous,new AiFaction("test",1),code,policy));foreach(var b in list)World.UpsertBuilding(new AiBuilding(b.Id,2,b.Type));
            Inventory.Register(1,2,StorageKind.Stockpile);if(wood>0)Inventory.TryDeposit(1,2,Goods.Wood,wood);
            var people=Enumerable.Range(0,8).Select(i=>new Peasant(-i-1,2,false,new MapPoint(6,6),new MapPoint(6,6),new MapPoint(6,6),0,0,2,true)).ToArray();
            Recruits=new PeasantRecruitment(people,Nav,new PopulationSettings(),1);Recruits.SetSpawnLimit(residents);Recruits.Advance(100);
            var initial=Enumerable.Range(0,initialVacancies).Select(i=>(LaborJob)new Job(10+i,2)).ToArray();
            Labor=new LaborManager(initial,new[]{Recruits},Nav,new PopulationSettings());
            var foliage=new FoliageResources(new[]{new FoliageResource(999,new MapPoint(32,32),.05f)},Nav,new WoodcuttingSettings());
            Build=new S2MEstateSawpitConstruction(Map,World,Inventory,Install,Nav,Labor,settings:new EstateSawpitSettings{SearchRadius=12,RetrySeconds=1},foliage:foliage);Build.PrepareLabor=p=>new Prepared(p.Id,2);
            Clock=new AiScheduler(World,new AiInventoryView(Inventory),planner:Build);
        }
        public void Tick(int n=200){for(int i=0;i<n;i++){Clock.Advance(.1);Build.CommitPending(Clock.Now);}}
        public void Dispose(){Build.Dispose();Clock.Dispose();Labor.Dispose();}
    }
    static BuildingPlacement Building(int id,string type,int x,int z,int w,int h)
    {var p=new BuildingPlacement();Set(p,"Id",id);Set(p,"Type",type);Set(p,"RuntimeEstateId",(int?)2);Set(p,"RawX",x*1024f);Set(p,"RawZ",z*1024f);Set(p,"FootprintWidth",w);Set(p,"FootprintHeight",h);return p;}
    public static int Main()
    {
        try
        {
            Check(EstateHousingValidation.Main()==0,"Housing/granary regression");
            for(int code=0;code<20;code++)using(var f=new Fixture(code))
            {
                int target=code==9?2:code==12 || code==16?3:1;f.Tick();
                Check(f.Build.CompletedCount==target && f.World.Territories[2].DesiredSawpits==target,"Wrong native sawpit target "+code);
                Check(f.Inventory.Amount(2,Goods.Wood)==32-target*3,"Sawpit cost "+code);
                Check(f.Labor.Jobs.Count==target && f.Labor.GetLaborDemand(2).MissingLaborers==target,"Runtime worker slots missing "+code);
                f.Labor.Advance(15);Check(f.Labor.GetLaborDemand(2).AssignedWorkers==target,"Runtime jobs did not recruit available labor");
                f.Tick();Check(f.Build.CompletedCount==target,"Duplicate baseline sawpit");
            }
            using(var f=new Fixture(16,residents:1)){f.Tick();Check(f.Build.CompletedCount==1,"Spent the same free worker twice");f.Labor.Advance(15);f.Tick();Check(f.Build.CompletedCount==1,"Employed worker counted as spare labor");}
            using(var f=new Fixture(residents:0)){f.Tick();Check(f.Build.CompletedCount==0,"Production built without peasants");f.Recruits.SetSpawnLimit(8);f.Tick();Check(f.Build.CompletedCount==0,"Pending births counted as actual labor");f.Recruits.Advance(15);f.Tick();Check(f.Build.CompletedCount==1,"Did not retry after recruitment");}
            using(var f=new Fixture(residents:1,initialVacancies:1)){f.Tick();Check(f.Build.CompletedCount==0,"Existing vacancies lost worker priority");}
            using(var f=new Fixture(9,initialVacancies:8))
            {
                f.Labor.Advance(100);f.Tick();Check(f.Build.CompletedCount==0 && f.World.Territories[2].PlannedLaborers==1,"Blocked production did not request housing labor");
                using var housing=new S2MEstateHovelConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor);
                f.Clock.Dispose();f.Clock=new AiScheduler(f.World,new AiInventoryView(f.Inventory),planner:new EstateConstructionPlanner(housing,f.Build));
                for(int i=0;i<200;i++){f.Clock.Advance(.1);housing.CommitPending(f.Clock.Now);f.Build.CommitPending(f.Clock.Now);}
                Check(housing.CompletedCount==1 && f.World.Territories[2].BuildingCount("Hovel")==2,"Saturated estate failed to expand housing for production");
            }
            using(var f=new Fixture(wood:2)){f.Tick();Check(f.Build.CompletedCount==0 && f.Inventory.Amount(2,Goods.Wood)==2,"Unaffordable sawpit placed");f.Inventory.TryDeposit(1,2,Goods.Wood,1);f.Tick();Check(f.Build.CompletedCount==1 && f.Inventory.Amount(2,Goods.Wood)==0,"Exact affordable sawpit failed");}
            using(var f=new Fixture(housing:false)){f.Tick();Check(f.Build.CompletedCount==0,"Production preceded initial housing");}
            foreach(var policy in new[]{AiConstructionPolicy.Unknown,AiConstructionPolicy.UseMapBuildings})using(var f=new Fixture(policy:policy)){f.Tick();Check(f.Build.CompletedCount==0,"Placement policy ignored");}
            using(var f=new Fixture(kind:AiTerritoryKind.Kingdom)){f.Tick();Check(f.Build.CompletedCount==0,"Estate AI built in kingdom");}
            using(var f=new Fixture())
            {
                var held=new Prepared(0,2);f.Build.PrepareLabor=p=>{held=new Prepared(p.Id,2){Fail=true};return held;};f.Tick();
                Check(f.Build.CompletedCount==0 && f.Inventory.Amount(2,Goods.Wood)==32 && f.Inventory.AvailableAmount(2,Goods.Wood)==32 && f.Labor.Jobs.Count==0 && f.Labor.Buildings.Count==0 && held.Disposed,"Rollback left jobs/cost/equipment");
                Check(f.Map.BuildingPlacements.Count==2 && f.World.Territories[2].BuildingCount("SawPit")==0,"Rollback left building");
            }
            using(var f=new Fixture())
            {
                for(int i=0;i<100;i++)f.Clock.Advance(.1);Check(f.Build.PendingCount==1,"Expected one pending intent");f.Recruits.Peasants[0].Worker.Remove();for(int i=1;i<8;i++)f.Recruits.Peasants[i].Worker.Remove();f.Build.CommitPending(f.Clock.Now);Check(f.Build.CompletedCount==0,"Lost labor not revalidated at commit");
            }
            Console.WriteLine("PASS: all profiles' first sawpit; wood-profile targets; 3 wood / 1 worker; actual/free labor and vacancy priority; runtime recruitment; affordability/retry; policy/kingdom/housing guards; labor-loss cancellation; full staffing/equipment rollback.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
