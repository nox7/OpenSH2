using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Movement;
public static class LocalizedNavigationValidation
{
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    sealed class Job : LaborJob{public Job(MapPoint at):base(1,1,at,at,0,0){}public override LaborRole Role=>LaborRole.Woodcutter;}
    public static int Main()
    {
        try{Fuzz();Gateways();RouteChanges();Scale();Console.WriteLine("PASS: localized connectivity matches cell BFS across random edits, overlapping blockers, diagonal/height constraints and tile edges; cached worker routes stop/replan; million-cell localized update benchmark.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Fuzz()
    {
        const int w=71,h=65;var random=new Random(42);var floor=new bool[w*h];var levels=new float[w*h];
        for(int i=0;i<floor.Length;i++){floor[i]=random.NextDouble()>.23;levels[i]=random.Next(4)*.3f;}
        var nav=new NavigationGrid(w,h,1,floor,levels,.4f);var holds=new Dictionary<long,(int x,int z,int w,int h)>();
        for(int edit=0;edit<240;edit++)
        {
            if(edit%3!=2)
            {
                var area=(x:random.Next(w-4),z:random.Next(h-4),w:random.Next(1,5),h:random.Next(1,5));
                holds.Add(edit,area);nav.SetRectangularObstacle(edit,area.x,area.z,area.x+area.w,area.z+area.h);
            }
            else if(holds.Count>0){long id=holds.Keys.First();nav.SetObstacle(id,default,0,false);holds.Remove(id);}
            var open=(bool[])floor.Clone();foreach(var area in holds.Values)for(int z=area.z;z<area.z+area.h;z++)for(int x=area.x;x<area.x+area.w;x++)open[z*w+x]=false;
            for(int q=0;q<12;q++)
            {
                int a=random.Next(w*h),b=random.Next(w*h);bool expected=Bfs(a,b,w,h,open,levels);
                var from=new MapPoint(a%w+.5f,a/w+.5f);var to=new MapPoint(b%w+.5f,b/w+.5f);
                Check(nav.CanReach(from,to)==expected,$"Connectivity mismatch edit={edit} query={q}");
                if(q==0){bool path=nav.TryFindPath(from,to,out var points);Check(path==expected,"A* reachability mismatch");if(path)for(int i=1;i<points.Count;i++)Check(nav.CanTravel(points[i-1],points[i]),"A* smoothed path crosses a blocker");}
            }
        }
    }
    static bool Bfs(int a,int b,int w,int h,bool[] open,float[] y)
    {
        if(!open[a] || !open[b])return false;var seen=new bool[open.Length];var queue=new Queue<int>();queue.Enqueue(a);seen[a]=true;
        while(queue.Count>0)
        {
            int at=queue.Dequeue();if(at==b)return true;int x=at%w,z=at/w;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int nx=x+dx,nz=z+dz;if(dx==0 && dz==0 || nx<0 || nz<0 || nx>=w || nz>=h)continue;
                int next=nz*w+nx;if(seen[next] || !open[next] || Math.Abs(y[at]-y[next])>.4f)continue;
                if(dx!=0 && dz!=0)
                {
                    int sideA=z*w+nx,sideB=nz*w+x;
                    if(!open[sideA] || !open[sideB] || Math.Abs(y[at]-y[sideA])>.4f || Math.Abs(y[at]-y[sideB])>.4f || Math.Abs(y[next]-y[sideA])>.4f || Math.Abs(y[next]-y[sideB])>.4f)continue;
                }
                seen[next]=true;queue.Enqueue(next);
            }
        }
        return false;
    }
    static void Gateways()
    {
        const int w=96,h=64;var floor=Enumerable.Repeat(true,w*h).ToArray();for(int z=0;z<h;z++)if(z!=31)floor[z*w+32]=false;
        var nav=new NavigationGrid(w,h,1,floor,new float[w*h],.4f);var a=new MapPoint(10.5f,31.5f);var b=new MapPoint(60.5f,31.5f);
        Check(nav.TryFindPath(a,b,out var path),"Open single-cell gateway blocked");
        var worker=new WorkerComponent(7,new MovementComponent(a,0));worker.TryAssign(new Job(b),path);worker.Advance(.1f,1,nav);
        nav.SetRectangularObstacle(1,32,31,33,32);Check(!nav.CanReach(a,b),"Gateway closing failed to split regions");
        nav.SetRectangularObstacle(2,32,31,33,32);nav.SetObstacle(1,default,0,false);Check(!nav.CanReach(a,b),"Overlapping blocker reopened gateway");
        var previous=worker.Movement.Position;worker.Advance(1,1,nav);Check(worker.WaitingForPath && MapPoint.Distance(previous,worker.Movement.Position)==0,"Worker crossed newly blocked route");
        nav.SetObstacle(2,default,0,false);Check(nav.CanReach(a,b),"Gateway reopening failed to reconnect regions");worker.Advance(60,1,nav);Check(worker.State==WorkerState.Working,"Worker did not resume after gateway reopened");
        nav.SetObstacle(3,new MapPoint(32.5f,31.5f),.2f,true);Check(!nav.CanReach(a,b),"Circular blocker failed");nav.SetObstacle(3,default,0,false);Check(nav.CanReach(a,b),"Circular blocker removal failed");
    }
    static void RouteChanges()
    {
        const int w=192;var nav=new NavigationGrid(w,w,1,Enumerable.Repeat(true,w*w).ToArray(),new float[w*w],.4f);var random=new Random(71);
        for(int i=0;i<1000;i++)
        {
            var a=new MapPoint((float)random.NextDouble()*191+.1f,(float)random.NextDouble()*191+.1f);
            var b=new MapPoint((float)random.NextDouble()*191+.1f,(float)random.NextDouble()*191+.1f);
            int before=nav.Revision,x=random.Next(190),z=random.Next(190);nav.SetRectangularObstacle(i,x,z,x+2,z+2);
            if(!nav.CanTravel(a,b))Check(nav.ChangedNear(a,b,before),"Route change hint missed a newly invalid segment");
            nav.SetObstacle(i,default,0,false);
        }
        int revision=nav.Revision;nav.SetRectangularObstacle(9000,10,170,12,172);
        Check(!nav.ChangedNear(new MapPoint(1,1),new MapPoint(190,190),revision),"Distant edit invalidated a diagonal route just because it lies in its bounding box");
    }
    static void Scale()
    {
        const int side=1024;var nav=new NavigationGrid(side,side,4,Enumerable.Repeat(true,side*side).ToArray(),new float[side*side],.4f);
        nav.PrepareConnectivity();Check(nav.ConnectivityCellsRebuilt==side*side,"Cold initialization was deferred into gameplay");
        var a=new MapPoint(.5f,.5f);var b=new MapPoint(255.5f,255.5f);Check(nav.CanReach(a,b),"Large map initialization");
        var times=new List<double>();long initial=nav.ConnectivityCellsRebuilt;
        for(int i=0;i<100;i++)
        {
            float x=8+(i*17)%230,z=8+(i*23)%230;long before=nav.ConnectivityCellsRebuilt;
            var sw=Stopwatch.StartNew();nav.SetRectangularObstacle(i,x,z,x+1,z+2);Check(nav.CanReach(a,b),"Open large map disconnected");sw.Stop();times.Add(sw.Elapsed.TotalMilliseconds);
            Check(nav.ConnectivityCellsRebuilt-before<=4096,"Local edit rebuilt distant navigation tiles");
            nav.SetObstacle(i,default,0,false);Check(nav.CanReach(a,b),"Large map reopen");
        }
        times.Sort();Console.WriteLine($"1,048,576-cell map: localized edits p50={times[50]:F3}ms p95={times[95]:F3}ms max={times.Last():F3}ms; {nav.ConnectivityCellsRebuilt-initial} cells rebuilt over 200 add/remove operations.");
    }
}
