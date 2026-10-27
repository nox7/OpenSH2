using System;
using System.Collections.Generic;

namespace Assets.Code.Simulation.Navigation
{
  public readonly struct MapPoint
  {
    public float X { get; }
    public float Z { get; }
    public MapPoint(float x, float z) { X = x; Z = z; }
    public static float Distance(MapPoint a, MapPoint b)
    { float x = a.X - b.X, z = a.Z - b.Z; return (float)Math.Sqrt(x * x + z * z); }
  }

  /// <summary>Reusable ground-level A*. Eight neighbours, no corner cutting or climbing cliffs.</summary>
  public sealed class NavigationGrid
  {
    public int Width { get; }
    public int Height { get; }
    public int Resolution { get; }
    private readonly bool[] walkable;
    private readonly float[] heights;
    private readonly int[] visited, closed, parents;
    private readonly float[] costs;
    private readonly float maximumStep;
    private int search;
    private int[] regions;
    private readonly List<int> regionParents=new();
    private readonly Dictionary<long,List<int>> obstacles = new();
    private readonly int[] obstacleCounts;
    public int Revision { get; private set; }
    public NavigationGrid(int width, int height, int resolution, bool[] walkable, float[] heights, float maximumStep)
    {
      if (width < 1 || height < 1 || resolution < 1 || walkable.Length != width * height || heights.Length != walkable.Length
        || !(maximumStep > 0)) throw new ArgumentException("Invalid navigation grid.");
      Width = width; Height = height; Resolution = resolution; this.walkable = walkable; this.heights = heights;
      this.maximumStep = maximumStep;
      obstacleCounts = new int[walkable.Length];
      visited = new int[walkable.Length]; closed = new int[walkable.Length]; parents = new int[walkable.Length]; costs = new float[walkable.Length];
    }
    private bool Open(int index) => walkable[index] && obstacleCounts[index] == 0;
    public bool IsWalkable(MapPoint p) => Index(p, out int index) && Open(index);
    /// <summary>Reference-counted dynamic blockers cannot accidentally reopen walls or overlapping trees.</summary>
    public void SetObstacle(long id,MapPoint center,float radius,bool enabled)
    {
      if(!enabled)
      {
        if(!obstacles.TryGetValue(id,out var old))return;
        foreach(int i in old)obstacleCounts[i]--;
        obstacles.Remove(id);
        // Opening cells only merges connectivity; do not flood the map for each harvested trunk.
        if(regions!=null)
        {
          foreach(int i in old)if(Open(i) && regions[i]==0)
          {regions[i]=regionParents.Count;regionParents.Add(regionParents.Count);}
          foreach(int i in old)if(Open(i))
          {
            int x=i%Width,z=i/Width;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
              if((dx==0 && dz==0) || x+dx<0 || z+dz<0 || x+dx>=Width || z+dz>=Height)continue;
              int other=(z+dz)*Width+x+dx;
              if(Connected(i,other))Union(regions[i],regions[other]);
            }
          }
        }
        Revision++;return;
      }
      if(obstacles.ContainsKey(id))return;
      if(!(radius>=0) || float.IsInfinity(radius))throw new ArgumentOutOfRangeException(nameof(radius));
      var cells=new List<int>();int cx=(int)Math.Floor(center.X*Resolution),cz=(int)Math.Floor(center.Z*Resolution),r=(int)Math.Ceiling(radius*Resolution);
      for(int z=Math.Max(0,cz-r);z<=Math.Min(Height-1,cz+r);z++) for(int x=Math.Max(0,cx-r);x<=Math.Min(Width-1,cx+r);x++)
      {
        float dx=(x+.5f)/Resolution-center.X,dz=(z+.5f)/Resolution-center.Z;
        if(dx*dx+dz*dz>radius*radius && !(x==cx && z==cz))continue;
        int i=z*Width+x;cells.Add(i);obstacleCounts[i]++;
      }
      obstacles[id]=cells;regions=null;Revision++;
    }
    public bool CanReach(MapPoint a, MapPoint b)
    {
      if(!Index(a,out int first) || !Index(b,out int last) || !Open(first) || !Open(last))return false;
      EnsureRegions(); return Region(regions[first])==Region(regions[last]);
    }
    public bool CanTravel(MapPoint a, MapPoint b)
    {
      if (!Index(a, out int ia) || !Index(b, out int ib) || !Open(ia) || !Open(ib)) return false;
      int steps = Math.Max(1, (int)Math.Ceiling(MapPoint.Distance(a, b) * Resolution * 4));
      int previous = ia;
      for (int s = 1; s <= steps; s++)
      {
        float t = s / (float)steps;
        if (!Index(new MapPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t), out int next) || !Open(next)) return false;
        if (next != previous && !Connected(previous, next)) return false;
        previous = next;
      }
      return true;
    }
    public bool TryNearest(MapPoint point, float radius, out MapPoint result)
    {
      if (IsWalkable(point)) { result = point; return true; }
      int cx = (int)Math.Floor(point.X * Resolution), cz = (int)Math.Floor(point.Z * Resolution);
      float best = radius * radius; int found = -1;
      int r = (int)Math.Ceiling(radius * Resolution);
      for (int z = Math.Max(0, cz-r); z <= Math.Min(Height-1, cz+r); z++)
        for (int x = Math.Max(0, cx-r); x <= Math.Min(Width-1, cx+r); x++)
        {
          int index = z * Width + x; if (!Open(index)) continue;
          MapPoint p = Point(index); float dx = p.X-point.X, dz = p.Z-point.Z, d = dx*dx+dz*dz;
          if (d <= best) { best = d; found = index; }
        }
      result = found >= 0 ? Point(found) : default; return found >= 0;
    }
    public bool TryFindPath(MapPoint start, MapPoint goal, out IReadOnlyList<MapPoint> path)
    {
      path = Array.Empty<MapPoint>();
      if (!Index(start, out int first) || !Index(goal, out int last) || !Open(first) || !Open(last)) return false;
      EnsureRegions();
      if (Region(regions[first]) != Region(regions[last])) return false;
      if (++search == int.MaxValue) { Array.Clear(visited,0,visited.Length); Array.Clear(closed,0,closed.Length); search=1; }
      var open = new MinHeap(); visited[first]=search; costs[first]=0; parents[first]=-1; open.Push(first, Heuristic(first,last));
      while (open.Count > 0)
      {
        int current = open.Pop(); if (closed[current] == search) continue;
        if (current == last)
        {
          var nodes = new List<MapPoint> { goal };
          for (int p = current; p != first; p = parents[p]) nodes.Add(Point(p));
          nodes.Add(start); nodes.Reverse();
          // Remove grid zigzags only when every intermediate segment remains traversable.
          var smooth = new List<MapPoint> { start }; int anchor=0;
          while (anchor < nodes.Count-1)
          {
            int next=anchor+1;
            for (int n=next+1; n<nodes.Count && CanTravel(nodes[anchor],nodes[n]); n++) next=n;
            smooth.Add(nodes[next]); anchor=next;
          }
          path=smooth; return true;
        }
        closed[current]=search; int x=current%Width, z=current/Width;
        for (int dz=-1; dz<=1; dz++) for (int dx=-1; dx<=1; dx++)
        {
          if ((dx==0 && dz==0) || x+dx<0 || x+dx>=Width || z+dz<0 || z+dz>=Height) continue;
          int next=(z+dz)*Width+x+dx;
          if (closed[next]==search || !Connected(current,next)) continue;
          float cost=costs[current]+(dx!=0 && dz!=0 ? 1.41421356f : 1);
          if (visited[next]==search && cost>=costs[next]) continue;
          visited[next]=search; costs[next]=cost; parents[next]=current; open.Push(next,cost+Heuristic(next,last));
        }
      }
      return false;
    }
    private bool Connected(int a, int b)
    {
      if (!Open(a) || !Open(b) || Math.Abs(heights[a]-heights[b])>maximumStep) return false;
      if (a%Width != b%Width && a/Width != b/Width)
      {
        int c=a/Width*Width+b%Width, d=b/Width*Width+a%Width;
        if (!Open(c) || !Open(d) || Math.Abs(heights[a]-heights[c])>maximumStep
          || Math.Abs(heights[a]-heights[d])>maximumStep || Math.Abs(heights[b]-heights[c])>maximumStep
          || Math.Abs(heights[b]-heights[d])>maximumStep) return false;
      }
      return true;
    }
    private void EnsureRegions()
    {
      if(regions!=null)return;
      regionParents.Clear();regionParents.Add(0);
      regions=new int[walkable.Length]; var queue=new Queue<int>(); int region=0;
      for(int first=0;first<walkable.Length;first++)
      {
        if(!Open(first) || regions[first]!=0)continue;
        regions[first]=++region;regionParents.Add(region);queue.Enqueue(first);
        while(queue.Count>0)
        {
          int current=queue.Dequeue(),x=current%Width,z=current/Width;
          for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
          {
            if((dx==0 && dz==0) || x+dx<0 || z+dz<0 || x+dx>=Width || z+dz>=Height)continue;
            int next=(z+dz)*Width+x+dx;
            if(regions[next]==0 && Connected(current,next)) { regions[next]=region; queue.Enqueue(next); }
          }
        }
      }
    }
    private int Region(int label)
    {
      while(regionParents[label]!=label)
      {regionParents[label]=regionParents[regionParents[label]];label=regionParents[label];}
      return label;
    }
    private void Union(int a,int b)
    {a=Region(a);b=Region(b);if(a!=b){if(a<b)regionParents[b]=a;else regionParents[a]=b;}}
    private bool Index(MapPoint p,out int index)
    { int x=(int)Math.Floor(p.X*Resolution), z=(int)Math.Floor(p.Z*Resolution); index=z*Width+x; return x>=0 && z>=0 && x<Width && z<Height; }
    private MapPoint Point(int index) => new((index%Width+.5f)/Resolution,(index/Width+.5f)/Resolution);
    private float Heuristic(int a,int b)
    { int dx=Math.Abs(a%Width-b%Width), dz=Math.Abs(a/Width-b/Width); return Math.Max(dx,dz)+.41421356f*Math.Min(dx,dz); }
    private sealed class MinHeap
    {
      private readonly List<(int node,float score)> values = new();
      public int Count => values.Count;
      public void Push(int node,float score)
      { int i=values.Count; values.Add((node,score)); while(i>0) { int p=(i-1)/2; if(values[p].score<=score)break; values[i]=values[p]; i=p; } values[i]=(node,score); }
      public int Pop()
      {
        int result=values[0].node; var tail=values[values.Count-1]; values.RemoveAt(values.Count-1);
        if(values.Count==0)return result; int i=0;
        while(i*2+1<values.Count) { int c=i*2+1; if(c+1<values.Count && values[c+1].score<values[c].score)c++; if(values[c].score>=tail.score)break; values[i]=values[c]; i=c; }
        values[i]=tail; return result;
      }
    }
  }
}
