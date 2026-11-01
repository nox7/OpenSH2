using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public enum FoliageState { Available, Reserved, Falling, Down, LogPile, Removed }
  public sealed class FoliageResource
  {
    public int Id { get; }
    public MapPoint Position { get; }
    public float Radius { get; }
    public bool Bush { get; }
    public FoliageState State { get; internal set; }
    public int? ReservedBy { get; internal set; }
    public float FallProgress { get; internal set; }
    public float FallHeading { get; internal set; }
    public int LogsRemaining { get; internal set; }
    public int InitialLogs { get; internal set; }
    public FoliageResource(int id,MapPoint position,float radius,bool bush=false)
    {
      if(id<0 || !(radius>0) || float.IsInfinity(radius) || float.IsNaN(position.X) || float.IsNaN(position.Z)
        || float.IsInfinity(position.X) || float.IsInfinity(position.Z))throw new ArgumentOutOfRangeException(nameof(id));
      Id=id;Position=position;Radius=radius;Bush=bush;
    }
  }

  public sealed class WoodcuttingRoute
  {
    public FoliageResource Resource { get; internal set; }
    public MapPoint Start,Home,Parking,Dismount,Chop;
    public IReadOnlyList<MapPoint> DriveOut,WalkOut,DriveHome;
  }

  /// <summary>One kingdom-wide reservation and spatial index. Only complete, reachable plans claim a resource.</summary>
  public sealed class FoliageResources
  {
    public NavigationGrid Walking { get; }
    // Vehicles and pedestrians share logical ground navigation; model size does not affect routes.
    public NavigationGrid Carts => Walking;
    public IReadOnlyList<FoliageResource> All { get; }
    public event Action<FoliageResource> Changed;
    private const float BucketSize=8;
    private readonly Dictionary<(int,int),List<FoliageResource>> buckets=new();
    private readonly List<(float distance,FoliageResource resource)> candidates=new();
    private readonly WoodcuttingSettings settings;
    private readonly Dictionary<int,FoliageResource> assignments=new();
    public FoliageResource AssignedPile(int worker)=>assignments.TryGetValue(worker,out var tree)?tree:null;
    public FoliageResources(IReadOnlyList<FoliageResource> resources,NavigationGrid navigation,WoodcuttingSettings settings)
    {
      settings.Validate();this.settings=settings;All=resources;Walking=navigation;
      var ids=new HashSet<int>();
      foreach(var tree in resources)if(tree==null || !ids.Add(tree.Id))throw new ArgumentException("Duplicate or missing foliage resource.",nameof(resources));
      foreach(var tree in resources)
      {
        var key=Bucket(tree.Position);
        if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<FoliageResource>();list.Add(tree);
        Walking.SetObstacle((1L<<32)+tree.Id,tree.Position,tree.Radius,true);
      }
    }
    /// <summary>A resumable nearest-standing-tree query over the shared spatial buckets. Never reserves a worker's resource.</summary>
    public NearestTreeSearch FindNearestTree(MapPoint origin,bool bushes=false)=>new NearestTreeSearch(this,origin,bushes);
    public sealed class NearestTreeSearch : IDisposable
    {
      private readonly FoliageResources owner;private readonly MapPoint origin;private readonly bool bushes;
      private readonly IEnumerator<List<FoliageResource>> buckets;
      private List<FoliageResource> current;private int cursor;
      private double distance=double.PositiveInfinity;
      public FoliageResource Result { get; private set; }
      public bool Complete { get; private set; }
      internal NearestTreeSearch(FoliageResources owner,MapPoint origin,bool bushes)
      {this.owner=owner;this.origin=origin;this.bushes=bushes;buckets=VisitBuckets().GetEnumerator();}
      // One step visits one bucket or examines one resource, so a host can enforce its own work budget.
      public void Step()
      {
        if(Complete)return;
        if(current!=null && cursor<current.Count)
        {
          var tree=current[cursor++];
          if(tree.Bush!=bushes || tree.State!=FoliageState.Available && tree.State!=FoliageState.Reserved)return;
          double dx=tree.Position.X-origin.X,dz=tree.Position.Z-origin.Z,d=dx*dx+dz*dz;
          if(d<distance || d==distance && (Result==null || tree.Id<Result.Id)){Result=tree;distance=d;}
          return;
        }
        if(!buckets.MoveNext()){Complete=true;buckets.Dispose();return;}
        current=buckets.Current;cursor=0;
      }
      private IEnumerable<List<FoliageResource>> VisitBuckets()
      {
        int cx=(int)Math.Floor(origin.X/BucketSize),cz=(int)Math.Floor(origin.Z/BucketSize);
        int maxX=(int)Math.Ceiling(owner.Walking.Width/(double)owner.Walking.Resolution/BucketSize)-1;
        int maxZ=(int)Math.Ceiling(owner.Walking.Height/(double)owner.Walking.Resolution/BucketSize)-1;
        int limit=Math.Max(Math.Max(Math.Abs(cx),Math.Abs(cz)),Math.Max(Math.Abs(maxX-cx),Math.Abs(maxZ-cz)));
        for(int ring=0;ring<=limit;ring++)
        {
          if(ring==0){owner.buckets.TryGetValue((cx,cz),out var first);yield return first;}
          else
          {
            for(int x=Math.Max(0,cx-ring);x<=Math.Min(maxX,cx+ring);x++)
            {
              if(cz-ring>=0){owner.buckets.TryGetValue((x,cz-ring),out var north);yield return north;}
              if(cz+ring<=maxZ){owner.buckets.TryGetValue((x,cz+ring),out var south);yield return south;}
            }
            for(int z=Math.Max(0,cz-ring+1);z<=Math.Min(maxZ,cz+ring-1);z++)
            {
              if(cx-ring>=0){owner.buckets.TryGetValue((cx-ring,z),out var west);yield return west;}
              if(cx+ring<=maxX){owner.buckets.TryGetValue((cx+ring,z),out var east);yield return east;}
            }
          }
          double edge=Math.Min(Math.Min(origin.X-(cx-ring)*BucketSize,(cx+ring+1)*BucketSize-origin.X),
            Math.Min(origin.Z-(cz-ring)*BucketSize,(cz+ring+1)*BucketSize-origin.Z));
          if(Result!=null && edge*edge>distance)yield break;
        }
      }
      public void Dispose(){buckets.Dispose();Complete=true;}
    }

    public bool TryReserveNearest(int worker,MapPoint origin,out WoodcuttingRoute route)
      => TryReserveNearest(worker,origin,origin,out route);
    public bool TryReserveNearest(int worker,MapPoint origin,MapPoint homeOrigin,out WoodcuttingRoute route)
    {
      route=null;
      if(!Carts.TryNearest(origin,2,out var start) || !Carts.TryNearest(homeOrigin,2,out var home))return false;
      if(assignments.TryGetValue(worker,out var assigned))
        return assigned.ReservedBy==worker && assigned.State!=FoliageState.Removed && TryPlan(assigned,start,home,out route);
      candidates.Clear();
      float width=Carts.Width/(float)Carts.Resolution,height=Carts.Height/(float)Carts.Resolution;
      float diagonal=(float)Math.Sqrt(width*width+height*height);
      float radius=settings.SearchRadiusCells>0?Math.Min(diagonal,settings.SearchRadiusCells):diagonal;
      var min=Bucket(new MapPoint(origin.X-radius,origin.Z-radius));var max=Bucket(new MapPoint(origin.X+radius,origin.Z+radius));
      for(int z=Math.Max(0,min.Item2);z<=Math.Min((int)(height/BucketSize),max.Item2);z++)
        for(int x=Math.Max(0,min.Item1);x<=Math.Min((int)(width/BucketSize),max.Item1);x++)
        if(buckets.TryGetValue((x,z),out var list))foreach(var tree in list)
        {
          if(tree.State!=FoliageState.Available && !(tree.State==FoliageState.LogPile && !tree.ReservedBy.HasValue))continue;
          float d=MapPoint.Distance(origin,tree.Position);if(d<=radius)candidates.Add((d,tree));
        }
      candidates.Sort((a,b)=>{int d=a.distance.CompareTo(b.distance);return d!=0?d:a.resource.Id.CompareTo(b.resource.Id);});
      foreach(var candidate in candidates)
      {
        var tree=candidate.resource;
        if(!TryPlan(tree,start,home,out route))continue;
        if(tree.State!=FoliageState.Available && !(tree.State==FoliageState.LogPile && !tree.ReservedBy.HasValue))continue;
        if(tree.State==FoliageState.Available)tree.State=FoliageState.Reserved;
        tree.ReservedBy=worker;assignments[worker]=tree;Changed?.Invoke(tree);return true;
      }
      return false;
    }
    private bool TryPlan(FoliageResource tree,MapPoint start,MapPoint home,out WoodcuttingRoute route)
    {
      route=null;double initial=Math.Atan2(start.Z-tree.Position.Z,start.X-tree.Position.X);
      for(int i=0;i<16;i++)
      {
        double angle=initial+(i%2==0?1:-1)*((i+1)/2)*Math.PI/8;
        float x=(float)Math.Cos(angle),z=(float)Math.Sin(angle);
        float parkingDistance=Math.Max(settings.CartParkingDistance*settings.UnitScale,tree.Radius+5*settings.UnitScale);
        var wanted=new MapPoint(tree.Position.X+x*parkingDistance,tree.Position.Z+z*parkingDistance);
        if(!Carts.TryNearest(wanted,.75f,out var park) || !Carts.CanReach(start,park) || !Carts.CanReach(park,home))continue;
        float reach=Math.Max(settings.ChopDistance*settings.UnitScale,tree.Radius+.4f*settings.UnitScale);
        var stand=new MapPoint(tree.Position.X+x*reach,tree.Position.Z+z*reach);
        if(!Walking.TryNearest(stand,.4f,out var chop) || MapPoint.Distance(chop,tree.Position)>reach+.65f)continue;
        var side=new MapPoint(park.X+z*settings.DismountDistance*settings.UnitScale,park.Z-x*settings.DismountDistance*settings.UnitScale);
        if(!Walking.TryNearest(side,.4f,out var dismount) || !Walking.CanTravel(park,dismount) || !Walking.CanReach(dismount,chop))continue;
        if(!Carts.TryFindPath(start,park,out var drive) || !Walking.TryFindPath(dismount,chop,out var walk)
          || !Carts.TryFindPath(park,home,out var back))continue;
        route=new WoodcuttingRoute{Resource=tree,Start=start,Home=home,Parking=park,Dismount=dismount,Chop=chop,DriveOut=drive,WalkOut=walk,DriveHome=back};return true;
      }
      return false;
    }
    public bool Release(FoliageResource tree,int worker)
    {
      if(tree.ReservedBy!=worker || (tree.State!=FoliageState.Reserved && tree.State!=FoliageState.LogPile))return false;
      tree.ReservedBy=null;if(tree.State==FoliageState.Reserved)tree.State=FoliageState.Available;
      assignments.Remove(worker);Changed?.Invoke(tree);return true;
    }
    public void BeginFall(FoliageResource tree,int worker,float heading)
    {
      Require(tree,worker,FoliageState.Reserved);tree.State=FoliageState.Falling;tree.FallHeading=heading;tree.FallProgress=0;Changed?.Invoke(tree);
    }
    public void Fall(FoliageResource tree,int worker,float progress)
    {
      Require(tree,worker,FoliageState.Falling);tree.FallProgress=Math.Min(1,Math.Max(0,progress));
      if(tree.FallProgress>=1)tree.State=FoliageState.Down;
    }
    public void Remove(FoliageResource tree,int worker)
    {
      Require(tree,worker,FoliageState.Down);tree.State=FoliageState.Removed;tree.ReservedBy=null;
      assignments.Remove(worker);
      Walking.SetObstacle((1L<<32)+tree.Id,tree.Position,tree.Radius,false);Changed?.Invoke(tree);
    }
    public void CreateLogPile(FoliageResource tree,int worker)
    {
      Require(tree,worker,FoliageState.Down);
      tree.InitialLogs=tree.LogsRemaining=tree.Bush?settings.LogsPerBush:settings.LogsPerTree;
      tree.State=FoliageState.LogPile;Changed?.Invoke(tree);
    }
    /// <summary>Exactly one topmost log is transferred. Ownership survives transport and unloading.</summary>
    public int TakeLog(FoliageResource tree,int worker)
    {
      Require(tree,worker,FoliageState.LogPile);
      int index=--tree.LogsRemaining;
      if(index==0)
      {
        tree.State=FoliageState.Removed;tree.ReservedBy=null;assignments.Remove(worker);
        Walking.SetObstacle((1L<<32)+tree.Id,tree.Position,tree.Radius,false);
      }
      Changed?.Invoke(tree);return index;
    }
    /// <summary>Death/removal abandons an intact tree or leaves a completed, unclaimed log pile.</summary>
    public void ReleaseWorker(int worker)
    {
      if(!assignments.TryGetValue(worker,out var tree))return;
      if(tree.State==FoliageState.Falling)Fall(tree,worker,1);
      if(tree.State==FoliageState.Down)CreateLogPile(tree,worker);
      Release(tree,worker);
    }
    private static void Require(FoliageResource tree,int worker,FoliageState state)
    {if(tree.State!=state || tree.ReservedBy!=worker)throw new InvalidOperationException("Foliage is not owned by this harvest operation.");}
    private static (int,int) Bucket(MapPoint p)=>((int)Math.Floor(p.X/BucketSize),(int)Math.Floor(p.Z/BucketSize));
  }
}
