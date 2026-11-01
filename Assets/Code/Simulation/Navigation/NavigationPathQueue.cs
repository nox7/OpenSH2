using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
namespace Assets.Code.Simulation.Navigation
{
    [Serializable] public sealed class PathfindingSettings
    {
        public int WorkUnitsPerFrame=2048,MaximumActiveSearches=2,MaximumQueuedSearches=1024;
        public double MillisecondsPerFrame=2;
        public void Validate()
        {if(WorkUnitsPerFrame<1 || MaximumActiveSearches<1 || MaximumQueuedSearches<MaximumActiveSearches || !(MillisecondsPerFrame>0) || double.IsInfinity(MillisecondsPerFrame))throw new ArgumentOutOfRangeException(nameof(PathfindingSettings));}
    }
    public readonly struct PathTarget
    {
        public readonly int Id;public readonly MapPoint Position;
        public PathTarget(int id,MapPoint position){Id=id;Position=position;}
    }
    public enum PathRequestStatus { Pending,Succeeded,Failed,Cancelled }
    public sealed partial class NavigationGrid
    {
        private readonly Stack<PathRequest.SearchNodes> pathWorkspaces=new();
        private PathRequest.SearchNodes RentPathNodes()
        {var nodes=pathWorkspaces.Count>0?pathWorkspaces.Pop():new PathRequest.SearchNodes(Width*Height);nodes.Begin();return nodes;}
        public NavigationPathQueue PathRequests { get; private set; }
        public void EnablePathRequests(PathfindingSettings settings=null)
        {if(PathRequests!=null)throw new InvalidOperationException("This grid already has a shared path queue.");PathRequests=new NavigationPathQueue(this,settings??new PathfindingSettings());}
        /// <summary>Exercise the request pipeline during loading, before first live dispatch incurs runtime/JIT setup.</summary>
        public void PreparePathRequests()
        {
            if(PathRequests==null || PathRequests.PendingCount!=0)throw new InvalidOperationException("Prepare an empty path queue during loading.");
            for(int cell=0;cell<walkable.Length;cell++)if(Open(cell))
            {
                int goal=cell;int x=cell%Width,z=cell/Width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0 && x+dx<Width && z+dz>=0 && z+dz<Height && Connected(cell,(z+dz)*Width+x+dx))goal=(z+dz)*Width+x+dx;
                var request=PathRequests.Request(Point(cell),new[]{new PathTarget(0,Point(goal))});
                while(request.Status==PathRequestStatus.Pending)PathRequests.AdvanceFrame();return;
            }
        }

        /// <summary>Sparse per-active-search state; no per-agent map-sized buffers. All phases consume work units.</summary>
        public sealed class PathRequest : IDisposable
        {
            internal struct Node {public float Cost;public int Parent,Stamp;public bool Closed;}
            // Small pages avoid whole-dictionary rehashes and map-sized clears in a live frame.
            internal sealed class SearchNodes
            {
                private Node[][] pages;private int stamp;
                public SearchNodes(int count){pages=new Node[(count+255)/256][];}
                public void Begin(){if(++stamp==int.MaxValue){pages=new Node[pages.Length][];stamp=1;}}
                public Node this[int cell]
                {get=>pages[cell>>8][cell&255];set{var page=pages[cell>>8]??(pages[cell>>8]=new Node[256]);value.Stamp=stamp;page[cell&255]=value;}}
                public bool TryGetValue(int cell,out Node value)
                {var page=pages[cell>>8];value=page==null?default:page[cell&255];return value.Stamp==stamp;}
            }
            private sealed class PathPoints : IReadOnlyList<MapPoint>
            {
                private readonly List<MapPoint[]> pages=new();public int Count { get; private set; }
                public MapPoint this[int index]=>pages[index>>8][index&255];
                public void Add(MapPoint point){if((Count&255)==0)pages.Add(new MapPoint[256]);pages[Count>>8][Count&255]=point;Count++;}
                public IReadOnlyList<MapPoint> AsReadOnly()=>this;
                public IEnumerator<MapPoint> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}
                IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
            }
            private readonly NavigationGrid grid;private readonly MapPoint start;private readonly PathTarget[] targets;
            private SearchNodes nodes;private Dictionary<int,PathTarget> goals;private MinHeap open;
            private PathPoints reverse,ordered,smooth;
            private int revision,first,trace,phase,copy,anchor,candidate,best,rayStep,raySteps,rayPrevious;
            private int goalMinX,goalMaxX,goalMinZ,goalMaxZ;
            public PathRequestStatus Status { get; private set; }
            public IReadOnlyList<MapPoint> Path { get; private set; }
            public int TargetId { get; private set; }
            public int Revision=>revision;
            public long ExpandedNodes { get; private set; }
            internal PathRequest(NavigationGrid grid,MapPoint start,IReadOnlyList<PathTarget> targets)
            {this.grid=grid;this.start=start;this.targets=new PathTarget[targets.Count];for(int i=0;i<targets.Count;i++)this.targets[i]=targets[i];}
            private void Initialize()
            {
                ReleaseNodes();revision=grid.Revision;phase=0;goals=new();nodes=grid.RentPathNodes();open=new();reverse=null;ordered=null;smooth=null;
                if(!grid.Index(start,out first) || !grid.Open(first)){Finish(false);return;}
                foreach(var target in targets)if(grid.Index(target.Position,out int cell) && grid.Open(cell))
                    if(!goals.TryGetValue(cell,out var old) || target.Id<old.Id)goals[cell]=target;
                if(goals.Count==0){Finish(false);return;}
                goalMinX=goalMinZ=int.MaxValue;goalMaxX=goalMaxZ=int.MinValue;
                foreach(int cell in goals.Keys)
                {goalMinX=Math.Min(goalMinX,cell%grid.Width);goalMaxX=Math.Max(goalMaxX,cell%grid.Width);goalMinZ=Math.Min(goalMinZ,cell/grid.Width);goalMaxZ=Math.Max(goalMaxZ,cell/grid.Width);}
                nodes[first]=new Node{Parent=-1};open.Push(first,Estimate(first));
            }
            private float Estimate(int cell)
            {
                // Octile distance to the goal bounding rectangle is admissible and O(1), even with many stores.
                int x=cell%grid.Width,z=cell/grid.Width;
                int dx=Math.Max(0,Math.Max(goalMinX-x,x-goalMaxX)),dz=Math.Max(0,Math.Max(goalMinZ-z,z-goalMaxZ));
                return Math.Max(dx,dz)+.41421356f*Math.Min(dx,dz);
            }
            internal void Step()
            {
                if(Status!=PathRequestStatus.Pending)return;
                if(goals==null || revision!=grid.Revision){Initialize();return;}
                if(phase==0)
                {
                    if(open.Count==0){Finish(false);return;}
                    int cell=open.Pop();var current=nodes[cell];if(current.Closed)return;
                    if(goals.TryGetValue(cell,out var target))
                    {TargetId=target.Id;trace=cell;reverse=new PathPoints();reverse.Add(target.Position);phase=1;return;}
                    current.Closed=true;nodes[cell]=current;ExpandedNodes++;int x=cell%grid.Width,z=cell/grid.Width;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        if(dx==0 && dz==0 || x+dx<0 || x+dx>=grid.Width || z+dz<0 || z+dz>=grid.Height)continue;
                        int next=(z+dz)*grid.Width+x+dx;if(!grid.Connected(cell,next))continue;
                        float cost=current.Cost+(dx!=0 && dz!=0?1.41421356f:1);
                        if(nodes.TryGetValue(next,out var previous) && (previous.Closed || previous.Cost<=cost))continue;
                        nodes[next]=new Node{Parent=cell,Cost=cost};open.Push(next,cost+Estimate(next));
                    }
                }
                else if(phase==1)
                {
                    if(trace!=first){reverse.Add(grid.Point(trace));trace=nodes[trace].Parent;return;}
                    reverse.Add(start);ordered=new PathPoints();copy=reverse.Count-1;phase=2;
                }
                else if(phase==2)
                {
                    if(copy>=0){ordered.Add(reverse[copy--]);return;}
                    reverse=null;ReleaseNodes();open=null;smooth=new PathPoints();smooth.Add(start);anchor=0;best=1;candidate=2;rayStep=0;phase=3;
                }
                else
                {
                    if(candidate>=ordered.Count){CommitSegment();return;}
                    var a=ordered[anchor];var b=ordered[candidate];
                    if(rayStep==0)
                    {raySteps=Math.Max(1,(int)Math.Ceiling(MapPoint.Distance(a,b)*grid.Resolution*4));grid.Index(a,out rayPrevious);rayStep=1;}
                    float t=rayStep/(float)raySteps;
                    if(!grid.Index(new MapPoint(a.X+(b.X-a.X)*t,a.Z+(b.Z-a.Z)*t),out int next) || !grid.Open(next) || next!=rayPrevious && !grid.Connected(rayPrevious,next))
                    {CommitSegment();return;}
                    rayPrevious=next;
                    if(++rayStep>raySteps){best=candidate++;rayStep=0;}
                }
            }
            private void CommitSegment()
            {
                smooth.Add(ordered[best]);anchor=best;
                if(anchor==ordered.Count-1){Path=smooth.AsReadOnly();Finish(true);return;}
                best=anchor+1;candidate=best+1;rayStep=0;
            }
            private void Finish(bool success)
            {Status=success?PathRequestStatus.Succeeded:PathRequestStatus.Failed;ReleaseNodes();open=null;goals=null;ordered=null;reverse=null;smooth=null;}
            private void ReleaseNodes(){if(nodes!=null){grid.pathWorkspaces.Push(nodes);nodes=null;}}
            public void Dispose(){if(Status==PathRequestStatus.Pending){Finish(false);Status=PathRequestStatus.Cancelled;}}
        }
    }
    /// <summary>One shared real-frame budget, independent of game speed and worker count. No threads or Unity APIs.</summary>
    public sealed class NavigationPathQueue
    {
        private readonly NavigationGrid grid;private readonly Queue<NavigationGrid.PathRequest> waiting=new(),active=new();
        public PathfindingSettings Settings { get; }
        public int LastWorkUnits { get; private set; }
        public double LastMilliseconds { get; private set; }
        public int PendingCount=>waiting.Count+active.Count;
        public NavigationPathQueue(NavigationGrid grid,PathfindingSettings settings){settings.Validate();this.grid=grid;Settings=settings;}
        public NavigationGrid.PathRequest Request(MapPoint start,IReadOnlyList<PathTarget> targets)
        {
            var request=new NavigationGrid.PathRequest(grid,start,targets);
            if(PendingCount>=Settings.MaximumQueuedSearches)request.Dispose();else waiting.Enqueue(request);return request;
        }
        public void AdvanceFrame()
        {
            Settings.Validate();LastWorkUnits=0;var timer=Stopwatch.StartNew();
            while(LastWorkUnits<Settings.WorkUnitsPerFrame && timer.Elapsed.TotalMilliseconds<Settings.MillisecondsPerFrame)
            {
                while(active.Count<Settings.MaximumActiveSearches && waiting.Count>0)
                {var next=waiting.Dequeue();LastWorkUnits++;if(next.Status==PathRequestStatus.Pending)active.Enqueue(next);if(LastWorkUnits>=Settings.WorkUnitsPerFrame)break;}
                if(active.Count==0 || LastWorkUnits>=Settings.WorkUnitsPerFrame)break;
                var request=active.Dequeue();int quantum=Math.Min(32,Settings.WorkUnitsPerFrame-LastWorkUnits);
                for(int i=0;i<quantum && request.Status==PathRequestStatus.Pending;i++){request.Step();LastWorkUnits++;}
                if(request.Status==PathRequestStatus.Pending)active.Enqueue(request);
            }
            LastMilliseconds=timer.Elapsed.TotalMilliseconds;
        }
    }
}
