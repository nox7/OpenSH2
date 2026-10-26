using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  public static class S2MNavigationBuilder
  {
    // Quarter-cell nodes allow close seating and narrow gaps without editing source terrain.
    public const int Resolution = 4;
    private static readonly HashSet<string> GroundBuildings = new(StringComparer.Ordinal) {
      "StockPile", "KillingPit", "ManTrap", "WheatFarm", "HopFarm", "AppleFarm", "PitchRig",
      "FishPond", "ConstructingBridge", "ServantsQuarters", "Signpost" };

    public static NavigationGrid Build(S2MFile map, PopulationSettings settings, S2MBuildingData buildings = null)
    {
      settings.Validate(); int size = map.MapSize * Resolution;
      var blocked = new bool[size * size];
      var physicalBarriers = new bool[size * size];
      var models=new Dictionary<int,Granny2ModelData>();
      var barriers=new Dictionary<int,IReadOnlyList<Granny2ModelData>>();
      if(buildings!=null)
      {
        foreach(var batch in buildings.Batches)foreach(var placement in batch.Placements)
        {
          models[placement.Id]=batch.Closed;
          var parts=new List<Granny2ModelData>{batch.Closed};parts.AddRange(batch.StaticParts);barriers[placement.Id]=parts;
        }
        foreach(var batch in buildings.Keeps.Batches)foreach(var placement in batch.Keeps)
        { models[placement.Id]=batch.Closed;barriers[placement.Id]=new[]{batch.Closed}; }
      }
      foreach (var building in map.BuildingPlacements)
      {
        if (GroundBuildings.Contains(building.Type)) continue;
        // Collision proxies can seal real doorways. Use the visible walls plus authored parts
        // (including gate parts in their resolved initial state), not an opaque proxy around the whole building.
        if(barriers.TryGetValue(building.Id,out var parts))foreach(var part in parts)
        {
          var state=S2MGatePartState.Resolve(map,building,part);
          if(state.Visible)S2MBuildingObstacleRasterizer.Rasterize(building,part,physicalBarriers,size,Resolution,settings.MaximumStepHeight,state.Lift);
        }
        if(models.TryGetValue(building.Id,out var model)) { Rasterize(building,model,blocked,size); continue; }
        var footprint = S2MBuildingFootprint.FromPlacement(building);
        for (int z=Math.Max(0,footprint.MinZ*Resolution); z<Math.Min(size,footprint.MaxZExclusive*Resolution); z++)
          for (int x=Math.Max(0,footprint.MinX*Resolution); x<Math.Min(size,footprint.MaxXExclusive*Resolution); x++)
          { blocked[z*size+x]=true;physicalBarriers[z*size+x]=true; }
      }
      var walkable = new bool[size*size]; var heights = new float[walkable.Length];
      var gateLanes=new bool[walkable.Length];
      var gateFootprints=new bool[map.MapSize*map.MapSize];
      foreach(var building in map.BuildingPlacements)
      {
        if(!building.Type.EndsWith("GateHouse",StringComparison.Ordinal))continue;
        S2MBuildingFootprint.FromPlacement(building).AddToMask(gateFootprints,map.MapSize,map.MapSize);
        float width=UnrotatedWidth(building);
        float depth=Math.Abs((int)Math.Round(building.RotationDegrees/90))%2==0 ? building.FootprintHeight : building.FootprintWidth;
        // Ground passage through the center, not the gatehouse's raised wall-walk/roof.
        for(float z=-.125f;z<depth+.25f;z+=.125f)for(float x=width*.5f-.4f;x<width*.5f+.4f;x+=.125f)
        {
          var p=BuildingPoint(building,x,z); int gx=(int)Math.Floor(p.X*Resolution),gz=(int)Math.Floor(p.Z*Resolution);
          if(gx>=0 && gz>=0 && gx<size && gz<size)gateLanes[gz*size+gx]=true;
        }
      }
      for (int z=0; z<size; z++) for (int x=0; x<size; x++)
      {
        int cx=x/Resolution, cz=z/Resolution, index=z*size+x;
        var wall=map.WallLayer;
        bool hasWall=wall!=null && cx<wall.Width && cz<wall.Height && wall.GetCell(cx,cz).HasWall;
        bool water=map.WaterLayer!=null && map.WaterLayer.HasWaterLikeSurface(cx,cz);
        bool cutout=map.Landscape?.TerrainCutoutValues != null && map.Landscape.TerrainCutoutValues[cz*map.MapSize+cx]==1;
        walkable[index]=!blocked[index] && !hasWall && !water && !cutout;
        heights[index]=SampleHeight(map,(x+.5f)/Resolution,(z+.5f)/Resolution)/1024;
        if(gateLanes[index]) { walkable[index]=true; heights[index]=S2MCampfireLayout.GroundHeight(map,(x+.5f)/Resolution,(z+.5f)/Resolution)/1024; }
        if(buildings?.Bridges?.ReplacedTerrainCells!=null && buildings.Bridges.ReplacedTerrainCells[cz*map.MapSize+cx]
          && !hasWall && !blocked[index])walkable[index]=true;
        var walking=map.WalkingLayer;
        if(walking!=null && cx<walking.Width && cz<walking.Height)
        {
          // Walking classes identify candidate surfaces, not permission to cross physical walls.
          byte surface=walking.GetClass(cx,cz);
          bool gatePassage=gateFootprints[cz*map.MapSize+cx] && (surface==4 || surface==10);
          bool bridge=buildings?.Bridges?.ReplacedTerrainCells!=null && buildings.Bridges.ReplacedTerrainCells[cz*map.MapSize+cx];
          walkable[index]=walking.IsGroundSurface(cx,cz) && !physicalBarriers[index]
            && (!hasWall || gatePassage) && (!water || bridge);
          if(gateFootprints[cz*map.MapSize+cx] && walkable[index])
            heights[index]=S2MCampfireLayout.GroundHeight(map,(x+.5f)/Resolution,(z+.5f)/Resolution)/1024;
        }
        walkable[index]&=!physicalBarriers[index];
      }
      // A campfire is a physical obstacle, not a shortcut through the fire.
      foreach (var camp in S2MCampfireLayout.Create(map, settings))
        for(int z=Math.Max(0,(int)((camp.Center.y-.4f)*Resolution)); z<Math.Min(size,(int)Math.Ceiling((camp.Center.y+.4f)*Resolution)); z++)
          for(int x=Math.Max(0,(int)((camp.Center.x-.4f)*Resolution)); x<Math.Min(size,(int)Math.Ceiling((camp.Center.x+.4f)*Resolution)); x++)
          { float dx=(x+.5f)/Resolution-camp.Center.x, dz=(z+.5f)/Resolution-camp.Center.y; if(dx*dx+dz*dz<.16f)walkable[z*size+x]=false; }
      return new NavigationGrid(size,size,Resolution,walkable,heights,settings.MaximumStepHeight);
    }

    private static void Rasterize(BuildingPlacement placement,Granny2ModelData model,bool[] blocked,int size)
    {
      Quaternion rotation=Quaternion.Euler(0,placement.RotationDegrees,0);
      foreach(var mesh in model.Meshes)
      {
        var vertices=new Vector3[mesh.VertexData.Length/8];
        for(int v=0;v<vertices.Length;v++)
        {
          int i=v*8; vertices[v]=rotation*new Vector3(mesh.VertexData[i],mesh.VertexData[i+2],mesh.VertexData[i+1])/204.8f
            +new Vector3(placement.MapX,placement.RawY/1024,placement.MapZ);
        }
        for(int i=0;i<mesh.Indices.Length;i+=3)
        {
          var a=vertices[mesh.Indices[i]];var b=vertices[mesh.Indices[i+1]];var c=vertices[mesh.Indices[i+2]];
          // Project roof/body surfaces onto ground. Flat exported ground patches are walkable.
          if(Math.Max(a.y,Math.Max(b.y,c.y))<placement.RawY/1024+.35f)continue;
          float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
          if(Math.Abs(denominator)<.000001f)continue;
          int minX=Math.Max(0,(int)Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x))*Resolution));
          int maxX=Math.Min(size-1,(int)Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))*Resolution));
          int minZ=Math.Max(0,(int)Math.Floor(Math.Min(a.z,Math.Min(b.z,c.z))*Resolution));
          int maxZ=Math.Min(size-1,(int)Math.Ceiling(Math.Max(a.z,Math.Max(b.z,c.z))*Resolution));
          for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
          {
            float px=(x+.5f)/Resolution,pz=(z+.5f)/Resolution;
            float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/denominator;
            float v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/denominator;
            if(u>=0 && v>=0 && u+v<=1 && u*a.y+v*b.y+(1-u-v)*c.y>=placement.RawY/1024+.35f)blocked[z*size+x]=true;
          }
        }
      }
    }

    public static float SampleHeight(S2MFile map,float x,float z)
    {
      var h=map.HeightLayer; if(h?.Heights==null)return 0;
      int ix=Mathf.Clamp(Mathf.FloorToInt(x),0,h.Width-1), iz=Mathf.Clamp(Mathf.FloorToInt(z),0,h.Height-1), i=iz*h.Width+ix;
      if(h.CornerHeightPlanes==null || h.CornerHeightPlanes.Length==0)return h.Heights[i];
      float u=Mathf.Clamp01(x-ix), v=Mathf.Clamp01(z-iz);
      return Mathf.Lerp(Mathf.Lerp(h.GetCornerHeight(0,i,2),h.GetCornerHeight(0,i,3),u),
        Mathf.Lerp(h.GetCornerHeight(0,i,0),h.GetCornerHeight(0,i,1),u),v);
    }

    public static MapPoint BuildingPoint(BuildingPlacement b,float localX,float localZ)
    {
      Vector3 delta=Quaternion.Euler(0,b.RotationDegrees,0)*new Vector3(localX,0,localZ);
      return new MapPoint(b.MapX+delta.x,b.MapZ+delta.z);
    }
    public static float UnrotatedWidth(BuildingPlacement b) => Math.Abs((int)Math.Round(b.RotationDegrees/90))%2==0 ? b.FootprintWidth : b.FootprintHeight;
  }
}
