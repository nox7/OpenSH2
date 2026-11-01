using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Movement;
using Assets.Code.Simulation.Population;
public static class IncrementalPathValidation
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static NavigationGrid Grid(int size=128)=>new(size,size,1,Enumerable.Repeat(true,size*size).ToArray(),new float[size*size],1);
    static void Finish(NavigationGrid grid,NavigationGrid.PathRequest request)
    {
        for(int frame=0;frame<100000 && request.Status==PathRequestStatus.Pending;frame++)
        {grid.PathRequests.AdvanceFrame();Check(grid.PathRequests.LastWorkUnits<=grid.PathRequests.Settings.WorkUnitsPerFrame,"Exceeded shared frame budget");}
        Check(request.Status!=PathRequestStatus.Pending,"Search never finished");
        if(request.Status==PathRequestStatus.Succeeded)for(int i=1;i<request.Path.Count;i++)Check(grid.CanTravel(request.Path[i-1],request.Path[i]),"Invalid smoothed segment");
    }
    sealed class Access : IStorageAccessProvider
    {public IReadOnlyList<MapPoint> AccessPoints(int id)=>new[]{new MapPoint(id==10?60.5f:100.5f,10.5f)};}
    sealed class TestJob : LaborJob
    {public override LaborRole Role=>LaborRole.Woodcutter;public TestJob(int id,MapPoint p):base(id,1,p,p,0,0){}}
    public static int Main()
    {
        try
        {
            var grid=Grid();grid.EnablePathRequests(new PathfindingSettings{WorkUnitsPerFrame=32,MillisecondsPerFrame=10000});
            grid.SetRectangularObstacle(1,50,0,51,120);
            var request=grid.PathRequests.Request(new MapPoint(10.5f,10.5f),new[]{new PathTarget(10,new MapPoint(60.5f,10.5f)),new PathTarget(20,new MapPoint(11.5f,115.5f))});
            grid.PathRequests.AdvanceFrame();Check(request.Status==PathRequestStatus.Pending,"Long search completed synchronously");
            Finish(grid,request);Check(request.Status==PathRequestStatus.Succeeded && request.TargetId==20,"Multi-target query chose Euclidean nearest behind wall");
            var changed=grid.PathRequests.Request(new MapPoint(10.5f,10.5f),new[]{new PathTarget(1,new MapPoint(60.5f,10.5f))});
            grid.PathRequests.AdvanceFrame();grid.SetRectangularObstacle(2,0,70,128,71);Finish(grid,changed);
            Check(changed.Status==PathRequestStatus.Failed,"Topology change returned stale path");grid.SetObstacle(2,default,0,false);
            var cancelled=grid.PathRequests.Request(new MapPoint(5,5),new[]{new PathTarget(0,new MapPoint(100,100))});cancelled.Dispose();
            var batch=Enumerable.Range(0,24).Select(i=>grid.PathRequests.Request(new MapPoint(5.5f,5.5f+i),new[]{new PathTarget(i,new MapPoint(100.5f,100.5f))})).ToArray();
            foreach(var next in batch){Finish(grid,next);Check(next.Status==PathRequestStatus.Succeeded,"Queued request starved or failed");}
            Check(cancelled.Status==PathRequestStatus.Cancelled,"Cancellation lost");
            var inventory=new EstateInventory();inventory.Register(10,1,StorageKind.Stockpile);inventory.Register(20,2,StorageKind.Stockpile);
            var lookup=new StockpileLocator(inventory,grid,new Access());var store=lookup.RequestNearest(1,Goods.Stone,new MapPoint(10.5f,10.5f));Finish(grid,store);Check(store.TargetId==10,"Foreign store selected");
            // Production and complete hauling with scheduled source, ox join and delivery routes.
            var quarry=new QuarryProduction(new QuarrySettings());
            for(int i=0;i<3;i++){var p=new MapPoint(15+i,15);var job=new QuarryJob(100,1,i,-100,p,p,0,0,p,quarry);var worker=new WorkerComponent(100+i,new MovementComponent(p,0));worker.TryAssign(job,new[]{p});worker.Advance(1000,1);}
            quarry.CompleteWorkStep();
            var network=new OxHaulingNetwork(grid,new[]{new OxHaulingSource(100,1,new MapPoint(20.5f,10.5f),quarry)},Array.Empty<HaulingDestination>());network.ConfigureStorage(inventory,lookup);
            var ox=new OxHandlerJob(200,1,-1000,new MapPoint(10.5f,10.5f),new MapPoint(11.5f,10.5f),0,0,new OxHaulingSettings());ox.Configure(network);
            var handler=new WorkerComponent(500,new MovementComponent(ox.Approach,0));handler.TryAssign(ox,new[]{ox.Approach});
            int frames=0;for(;frames<50000 && ox.DeliveredStone<16;frames++){grid.PathRequests.AdvanceFrame();handler.Advance(.1f,1);}
            Check(ox.DeliveredStone==16 && inventory.Amount(1,Goods.Stone)==16 && quarry.StoredStone==32,"Scheduled ox hauling changed resource counts or stalled phase="+ox.Phase);
            handler.Remove();Check(!ox.OxVisible && quarry.HaulerId==null,"Removal leaked ox/claim");
            var woodSettings=new WoodcuttingSettings{CartSpeedCellsPerSecond=10,FallSeconds=.1f,SawingSeconds=.1f,FellingCycles=1,ChoppingCycles=1};
            var resources=new FoliageResources(new[]{new FoliageResource(999,new MapPoint(12,12),.3f)},grid,woodSettings);
            var wood=new WoodcutterJob(300,1,-2000,new MapPoint(3,4),new MapPoint(3,4),0,0);wood.ConfigureHarvest(resources,woodSettings);wood.SetClipDurations(.1f,.1f);
            wood.ConfigureProduction(new MapPoint(3,4),0,Array.Empty<WoodDeliveryPoint>());wood.ConfigureStorage(inventory,lookup);
            var cutter=new WorkerComponent(600,new MovementComponent(wood.Approach,0));cutter.TryAssign(wood,new[]{wood.Approach});
            for(int i=0;i<50000 && wood.DeliveredPlanks<32;i++){grid.PathRequests.AdvanceFrame();cutter.Advance(.1f,1);}
            Check(wood.DeliveredPlanks==32 && inventory.Amount(1,Goods.Wood)==32,"Queued woodcutting/delivery loop stalled phase="+wood.Phase);cutter.Remove();
            var peopleGrid=Grid(64);peopleGrid.SetRectangularObstacle(77,30,0,31,58);peopleGrid.EnablePathRequests(new PathfindingSettings{WorkUnitsPerFrame=64,MillisecondsPerFrame=10000});
            var people=Enumerable.Range(0,3).Select(i=>new Peasant(700+i,1,false,new MapPoint(3.5f,3.5f+i),new MapPoint(3.5f,3.5f+i),new MapPoint(5.5f,3.5f+i),0,0,1,true)).ToArray();
            var populationSettings=new PopulationSettings{SpawnIntervalSeconds=.01f,JobAssignmentIntervalSeconds=.1f,WalkSpeedCellsPerSecond=10};
            var recruits=new PeasantRecruitment(people,peopleGrid,populationSettings,1);
            var jobs=Enumerable.Range(0,3).Select(i=>(LaborJob)new TestJob(900+i,new MapPoint(60.5f,3.5f+i))).ToArray();
            using(var manager=new LaborManager(jobs,new[]{recruits},peopleGrid,populationSettings))
            {
                for(int i=0;i<5000 && jobs.Any(j=>!j.IsOccupied);i++)manager.Advance(.1f);
                Check(jobs.All(j=>j.IsOccupied) && people.Select(p=>p.Job).Distinct().Count()==3,"Queued recruitment/staffing lost workers or stalled");
                peopleGrid.SetRectangularObstacle(78,0,40,30,41);
                manager.Advance(0);Check(peopleGrid.PathRequests.LastWorkUnits<=64,"Paused staffing exceeded frame budget");
            }
            Console.WriteLine("PASS: bounded long/multi-target search and smoothing, nearest by navigable route, topology restart, cancellation, queue fairness, same-estate storage, and scheduled ox collection/join/16-item delivery in "+frames+" frames.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
