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
  public sealed partial class NavigationGrid
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
    // Exact components within small tiles, linked by real traversable boundary edges.
    private const int RegionTileSize=32;
    private int[] regions;
    private List<int>[] tileRegions;
    private int tilesX,tilesZ;
    private readonly int[] tileChangedAt;
    private readonly HashSet<int> dirtyTiles=new();
    private readonly Dictionary<int,HashSet<int>> regionEdges=new();
    private readonly Dictionary<int,int> connectedGroups=new();
    private readonly List<int> graphQueue=new();
    private readonly int[] tileQueue=new int[RegionTileSize*RegionTileSize];
    public long ConnectivityCellsRebuilt { get; private set; }
    public long ConnectivityTilesRebuilt { get; private set; }
    private readonly Dictionary<long,List<int>> obstacles = new();
    private readonly int[] obstacleCounts;
    public int Revision { get; private set; }
    public NavigationGrid(int width, int height, int resolution, bool[] walkable, float[] heights, float maximumStep)
    {
      if (width < 1 || height < 1 || resolution < 1 || walkable.Length != width * height || heights.Length != walkable.Length
        || !(maximumStep > 0)) throw new ArgumentException("Invalid navigation grid.");
      Width = width; Height = height; Resolution = resolution; this.walkable = walkable; this.heights = heights;
      this.maximumStep = maximumStep;
      tilesX=(width+RegionTileSize-1)/RegionTileSize;tilesZ=(height+RegionTileSize-1)/RegionTileSize;tileChangedAt=new int[tilesX*tilesZ];
      obstacleCounts = new int[walkable.Length];
      visited = new int[walkable.Length]; closed = new int[walkable.Length]; parents = new int[walkable.Length]; costs = new float[walkable.Length];
    }
    private bool Open(int index) => walkable[index] && obstacleCounts[index] == 0;
    public bool IsWalkable(MapPoint p) => Index(p, out int index) && Open(index);
    /// <summary>Exact logical building footprint; does not add model or cart clearance.</summary>
    public void SetRectangularObstacle(long id,float minX,float minZ,float maxX,float maxZ)
    {
      if(obstacles.ContainsKey(id))throw new ArgumentException("Obstacle already registered.");
      if(float.IsNaN(minX)||float.IsNaN(minZ)||float.IsNaN(maxX)||float.IsNaN(maxZ)
        ||float.IsInfinity(minX)||float.IsInfinity(minZ)||float.IsInfinity(maxX)||float.IsInfinity(maxZ)
        ||maxX<=minX||maxZ<=minZ)throw new ArgumentOutOfRangeException();
      var cells=new List<int>();
      for(int z=Math.Max(0,(int)Math.Floor(minZ*Resolution));z<Math.Min(Height,(int)Math.Ceiling(maxZ*Resolution));z++)
        for(int x=Math.Max(0,(int)Math.Floor(minX*Resolution));x<Math.Min(Width,(int)Math.Ceiling(maxX*Resolution));x++)
        {
          float px=(x+.5f)/Resolution,pz=(z+.5f)/Resolution;
          if(px<minX||pz<minZ||px>=maxX||pz>=maxZ)continue;
          int i=z*Width+x;cells.Add(i);if(obstacleCounts[i]++==0 && walkable[i])DirtyRegion(i);
        }
      obstacles.Add(id,cells);Revision++;
    }
    /// <summary>Reference-counted dynamic blockers cannot accidentally reopen walls or overlapping trees.</summary>
    public void SetObstacle(long id,MapPoint center,float radius,bool enabled)
    {
      if(!enabled)
      {
        if(!obstacles.TryGetValue(id,out var old))return;
        foreach(int i in old)if(--obstacleCounts[i]==0 && walkable[i])DirtyRegion(i);
        obstacles.Remove(id);
        Revision++;return;
      }
      if(obstacles.ContainsKey(id))return;
      if(!(radius>=0) || float.IsInfinity(radius))throw new ArgumentOutOfRangeException(nameof(radius));
      var cells=new List<int>();int cx=(int)Math.Floor(center.X*Resolution),cz=(int)Math.Floor(center.Z*Resolution),r=(int)Math.Ceiling(radius*Resolution);
      for(int z=Math.Max(0,cz-r);z<=Math.Min(Height-1,cz+r);z++) for(int x=Math.Max(0,cx-r);x<=Math.Min(Width-1,cx+r);x++)
      {
        float dx=(x+.5f)/Resolution-center.X,dz=(z+.5f)/Resolution-center.Z;
        if(dx*dx+dz*dz>radius*radius && !(x==cx && z==cz))continue;
        int i=z*Width+x;cells.Add(i);if(obstacleCounts[i]++==0 && walkable[i])DirtyRegion(i);
      }
      obstacles[id]=cells;Revision++;
    }
    /// <summary>Build the initial connectivity index during map loading, after static and foliage blockers are registered.</summary>
    public void PrepareConnectivity()=>EnsureRegions();
    public bool CanReach(MapPoint a, MapPoint b)
    {
      if(!Index(a,out int first) || !Index(b,out int last) || !Open(first) || !Open(last))return false;
      EnsureRegions(); return RegionsConnected(regions[first],regions[last]);
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
      if (!RegionsConnected(regions[first],regions[last])) return false;
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
    private void DirtyRegion(int cell)
    {
      int x=cell%Width,z=cell/Width;
      // A changed side cell also changes diagonal edges between its neighbours.
      for(int tz=Math.Max(0,z-1)/RegionTileSize;tz<=Math.Min(Height-1,z+1)/RegionTileSize;tz++)
        for(int tx=Math.Max(0,x-1)/RegionTileSize;tx<=Math.Min(Width-1,x+1)/RegionTileSize;tx++)
        {int tile=tz*tilesX+tx;tileChangedAt[tile]=Revision+1;if(regions!=null)dirtyTiles.Add(tile);}
    }
    /// <summary>Conservative local change test for cached route segments; exact traversal checks run only when needed.</summary>
    public bool ChangedNear(MapPoint a,MapPoint b,int sinceRevision)
    {
      if(sinceRevision<0 || !Index(a,out int first) || !Index(b,out int last))return true;
      int x=first%Width/RegionTileSize,z=first/Width/RegionTileSize,endX=last%Width/RegionTileSize,endZ=last/Width/RegionTileSize;
      double dx=b.X-a.X,dz=b.Z-a.Z,size=RegionTileSize/(double)Resolution;
      int stepX=Math.Sign(dx),stepZ=Math.Sign(dz);
      double deltaX=stepX==0?double.PositiveInfinity:size/Math.Abs(dx),deltaZ=stepZ==0?double.PositiveInfinity:size/Math.Abs(dz);
      double edgeX=stepX==0?double.PositiveInfinity:((stepX>0?x+1:x)*size-a.X)/dx;
      double edgeZ=stepZ==0?double.PositiveInfinity:((stepZ>0?z+1:z)*size-a.Z)/dz;
      // Supercover traversal visits only tiles crossed by the segment, not its potentially map-sized bounding box.
      while(true)
      {
        if(TileChanged(x,z,sinceRevision))return true;
        if(x==endX && z==endZ)return false;
        if(edgeX<edgeZ){x+=stepX;edgeX+=deltaX;}
        else if(edgeZ<edgeX){z+=stepZ;edgeZ+=deltaZ;}
        else
        {
          if(TileChanged(x+stepX,z,sinceRevision) || TileChanged(x,z+stepZ,sinceRevision))return true;
          x+=stepX;z+=stepZ;edgeX+=deltaX;edgeZ+=deltaZ;
        }
      }
    }
    private bool TileChanged(int x,int z,int since)=>x>=0 && z>=0 && x<tilesX && z<tilesZ && tileChangedAt[z*tilesX+x]>since;

    private void EnsureRegions()
    {
      if(regions==null)
      {
        regions=new int[walkable.Length];tilesX=(Width+RegionTileSize-1)/RegionTileSize;tilesZ=(Height+RegionTileSize-1)/RegionTileSize;
        tileRegions=new List<int>[tilesX*tilesZ];
        for(int tile=0;tile<tileRegions.Length;tile++){tileRegions[tile]=new List<int>();RebuildTile(tile);}
        for(int tile=0;tile<tileRegions.Length;tile++)LinkTile(tile);
        return;
      }
      if(dirtyTiles.Count==0)return;
      // Remove both ends of stale boundary links before replacing local component IDs.
      foreach(int tile in dirtyTiles)foreach(int label in tileRegions[tile])
      {
        if(!regionEdges.TryGetValue(label,out var adjacent))continue;
        foreach(int other in adjacent)if(regionEdges.TryGetValue(other,out var reverse))reverse.Remove(label);
        regionEdges.Remove(label);
      }
      foreach(int tile in dirtyTiles)RebuildTile(tile);
      foreach(int tile in dirtyTiles)LinkTile(tile);
      dirtyTiles.Clear();connectedGroups.Clear();
    }
    private void RebuildTile(int tile)
    {
      int minX=tile%tilesX*RegionTileSize,minZ=tile/tilesX*RegionTileSize,maxX=Math.Min(Width,minX+RegionTileSize),maxZ=Math.Min(Height,minZ+RegionTileSize);
      tileRegions[tile].Clear();
      for(int z=minZ;z<maxZ;z++)Array.Clear(regions,z*Width+minX,maxX-minX);
      for(int z=minZ;z<maxZ;z++)for(int x=minX;x<maxX;x++)
      {
        int first=z*Width+x;if(!Open(first) || regions[first]!=0)continue;
        int label=first+1,head=0,tail=1;regions[first]=label;tileQueue[0]=first;tileRegions[tile].Add(label);
        while(head<tail)
        {
          int cell=tileQueue[head++],cx=cell%Width,cz=cell/Width;
          for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
          {
            int nx=cx+dx,nz=cz+dz;if((dx==0 && dz==0) || nx<minX || nz<minZ || nx>=maxX || nz>=maxZ)continue;
            int next=nz*Width+nx;if(regions[next]!=0 || !Connected(cell,next))continue;
            regions[next]=label;tileQueue[tail++]=next;
          }
        }
      }
      ConnectivityCellsRebuilt+=(maxX-minX)*(maxZ-minZ);ConnectivityTilesRebuilt++;
    }
    private void LinkTile(int tile)
    {
      int minX=tile%tilesX*RegionTileSize,minZ=tile/tilesX*RegionTileSize,maxX=Math.Min(Width,minX+RegionTileSize),maxZ=Math.Min(Height,minZ+RegionTileSize);
      for(int x=minX;x<maxX;x++){LinkBoundary(x,minZ,tile);if(maxZ-1!=minZ)LinkBoundary(x,maxZ-1,tile);}
      for(int z=minZ+1;z<maxZ-1;z++){LinkBoundary(minX,z,tile);if(maxX-1!=minX)LinkBoundary(maxX-1,z,tile);}
    }
    private void LinkBoundary(int x,int z,int tile)
    {
      int cell=z*Width+x;if(regions[cell]==0)return;
      for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
      {
        int nx=x+dx,nz=z+dz;if(nx<0 || nz<0 || nx>=Width || nz>=Height || nz/RegionTileSize*tilesX+nx/RegionTileSize==tile)continue;
        int next=nz*Width+nx;if(!Connected(cell,next))continue;
        int a=regions[cell],b=regions[next];
        if(!regionEdges.TryGetValue(a,out var aa))regionEdges.Add(a,aa=new HashSet<int>());
        if(!regionEdges.TryGetValue(b,out var bb))regionEdges.Add(b,bb=new HashSet<int>());
        aa.Add(b);bb.Add(a);
      }
    }
    private bool RegionsConnected(int first,int last)
    {
      if(first==last)return true;
      if(!connectedGroups.TryGetValue(first,out int group))
      {
        group=first;connectedGroups[first]=group;graphQueue.Clear();graphQueue.Add(first);
        for(int head=0;head<graphQueue.Count;head++)
        {
          if(!regionEdges.TryGetValue(graphQueue[head],out var adjacent))continue;
          foreach(int other in adjacent)if(!connectedGroups.ContainsKey(other)){connectedGroups[other]=group;graphQueue.Add(other);}
        }
      }
      return connectedGroups.TryGetValue(last,out int otherGroup) && otherGroup==group;
    }
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
