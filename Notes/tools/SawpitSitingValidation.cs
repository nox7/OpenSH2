using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
public static class SawpitSitingValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static void Set(object o,string p,object value)=>o.GetType().GetProperty(p).SetValue(o,value);
    public static int Main()
    {
        try
        {
            Check(EstateSawpitValidation.Main()==0,"Production regressions");
            var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],.4f);
            var random=new Random(57);var trees=new List<FoliageResource>();
            for(int i=0;i<600;i++){var t=new FoliageResource(i,new MapPoint((float)random.NextDouble()*63,(float)random.NextDouble()*63),.05f,i%5==0);if(i%7==0)Set(t,"State",FoliageState.Removed);trees.Add(t);}
            var foliage=new FoliageResources(trees,nav,new WoodcuttingSettings());
            foreach(bool bushes in new[]{false,true})for(int i=0;i<50;i++)
            {
                var from=new MapPoint((float)random.NextDouble()*63,(float)random.NextDouble()*63);
                using var query=foliage.FindNearestTree(from,bushes);int steps=0;while(!query.Complete && steps++<10000)query.Step();
                var expected=trees.Where(t=>t.Bush==bushes && t.State==FoliageState.Available).OrderBy(t=>Math.Pow(t.Position.X-from.X,2)+Math.Pow(t.Position.Z-from.Z,2)).ThenBy(t=>t.Id).First();
                Check(query.Complete && query.Result.Id==expected.Id,"Spatial nearest query disagrees with exhaustive search");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.Dispose();f.Clock.Dispose();
                var near=new FoliageResource(100,new MapPoint(12.4f,22.5f),.2f);var far=new FoliageResource(101,new MapPoint(45,45),.2f);
                var resources=new FoliageResources(new[]{near,far},f.Nav,new WoodcuttingSettings());
                using var build=new S2MEstateSawpitConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor,settings:new EstateSawpitSettings{SearchRadius=12},foliage:resources);
                build.PrepareLabor=p=>new EstateSawpitValidation.Prepared(p.Id,2);
                using var clock=new AiScheduler(f.World,null,new AiSettings{WorkUnitsPerEvaluation=1},build);
                for(int i=0;i<1000;i++){long before=build.CandidateTests+build.ResourceSearchSteps;clock.Advance(.1);Check(build.CandidateTests+build.ResourceSearchSteps-before<=1,"Tree and placement searches exceeded shared budget");build.CommitPending(clock.Now);}
                var pit=f.Map.BuildingPlacements.Single(b=>b.Type=="SawPit");
                Check(Math.Abs(pit.MapX-near.Position.X)<3 && Math.Abs(pit.MapZ-near.Position.Z)<3,"Sawpit stayed near estate marker instead of nearest stockpile tree");
                Check(near.ReservedBy==null && near.State==FoliageState.Available,"AI siting stole a worker's tree reservation");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.Dispose();f.Clock.Dispose();
                var near=new FoliageResource(100,new MapPoint(12.4f,22.5f),.2f);var far=new FoliageResource(101,new MapPoint(45,30),.2f);
                var resources=new FoliageResources(new[]{near,far},f.Nav,new WoodcuttingSettings());
                using var build=new S2MEstateSawpitConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor,foliage:resources);build.PrepareLabor=p=>new EstateSawpitValidation.Prepared(p.Id,2);
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<100 && build.PendingCount==0;i++)clock.Advance(.1);
                Check(build.PendingCount==1,"No queued tree-centred site");Set(near,"State",FoliageState.Removed);build.CommitPending(clock.Now);
                Check(build.CompletedCount==0,"Committed beside a felled tree without re-evaluation");
                for(int i=0;i<300;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                Check(build.CompletedCount==1 && f.Map.BuildingPlacements.Single(b=>b.Type=="SawPit").MapX>40,"Failed to retarget remaining trees");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.Dispose();f.Clock.Dispose();
                BuildingPlacement Stock(int id,int estate,int x,int z)
                {
                    var b=new BuildingPlacement();Set(b,"Id",id);Set(b,"Type","StockPile");Set(b,"RuntimeEstateId",(int?)estate);Set(b,"RawX",x*1024f);Set(b,"RawZ",z*1024f);Set(b,"FootprintWidth",7);Set(b,"FootprintHeight",5);return b;
                }
                var second=Stock(3,2,50,10);var foreign=Stock(4,3,2,50);
                Set(f.Map,"BuildingPlacements",f.Map.BuildingPlacements.Concat(new[]{second,foreign}).ToArray());
                f.World.UpsertBuilding(new AiBuilding(3,2,"StockPile"));
                var resources=new FoliageResources(new[]{new FoliageResource(1,new MapPoint(14,22),.2f),new FoliageResource(2,new MapPoint(57,10),.2f),new FoliageResource(3,new MapPoint(5.5f,52.5f),.2f)},f.Nav,new WoodcuttingSettings());
                using var build=new S2MEstateSawpitConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor,foliage:resources);build.PrepareLabor=p=>new EstateSawpitValidation.Prepared(p.Id,2);
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<300;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                var pit=f.Map.BuildingPlacements.Single(b=>b.Type=="SawPit");Check(pit.MapX>50 && pit.MapZ<15,"Multiple stockpiles/foreign estate filtering chose the wrong tree");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.Dispose();f.Clock.Dispose();
                using var build=new S2MEstateSawpitConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor);build.PrepareLabor=p=>new EstateSawpitValidation.Prepared(p.Id,2);
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<100;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                Check(build.CompletedCount==0 && f.World.Territories[2].PlannedLaborers==0,"Missing foliage provider silently fell back to marker placement");
            }
            foreach(int angle in new[]{0,90,180,270})
            {
                var p=new BuildingPlacement();Set(p,"Type","SawPit");Set(p,"RawX",10*1024f);Set(p,"RawZ",10*1024f);Set(p,"RotationDegrees",(float)angle);Set(p,"FootprintWidth",angle%180==0?1:4);Set(p,"FootprintHeight",angle%180==0?4:1);
                int[] cells=S2MTerrainEdits.SawpitCells(p,32,32);Check(cells.Length==2 && cells.Distinct().Count()==2,"Cutout included worker clearance");
                Check(angle%180==0?Math.Abs(cells[1]-cells[0])==32:Math.Abs(cells[1]-cells[0])==1,"Cutout rotation wrong");
            }
            Console.WriteLine("PASS: exact resumable spatial tree/bush search; shared tree/site budget; stockpile-centred tree choice, multiple stores/estate filtering; no reservation side effects; stale-tree cancellation; rotated physical cutout masks.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
