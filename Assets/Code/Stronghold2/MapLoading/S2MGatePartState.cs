using System;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Shared initial gate presentation/barriers for saved navigable gate portals.</summary>
  public readonly struct S2MGatePartState
  {
    public bool Visible { get; }
    public float Lift { get; }
    private S2MGatePartState(bool visible,float lift) { Visible=visible;Lift=lift; }
    public static S2MGatePartState Resolve(S2MFile map,BuildingPlacement building,Granny2ModelData part)
    {
      var normal=new S2MGatePartState(true,0);
      if(!building.Type.EndsWith("GateHouse",StringComparison.Ordinal) || map.WalkingLayer==null || part.SourcePath==null)return normal;
      bool grille=part.SourcePath.IndexOf("porticulis",StringComparison.OrdinalIgnoreCase)>=0
        || part.SourcePath.IndexOf("portcullis",StringComparison.OrdinalIgnoreCase)>=0;
      bool doors=part.SourcePath.IndexOf("_doors.",StringComparison.OrdinalIgnoreCase)>=0;
      if(!grille && !doors)return normal;
      var footprint=S2MBuildingFootprint.FromPlacement(building);bool passage=false;var walking=map.WalkingLayer;
      for(int z=Math.Max(0,footprint.MinZ);z<Math.Min(walking.Height,footprint.MaxZExclusive);z++)
        for(int x=Math.Max(0,footprint.MinX);x<Math.Min(walking.Width,footprint.MaxXExclusive);x++)
          if(walking.GetClass(x,z)==10)passage=true;
      if(!passage)return normal;
      // Initial open state: retract grilles; omit shut door leaves until hinge animation is decoded.
      if(doors)return new S2MGatePartState(false,0);
      float low=float.MaxValue,high=float.MinValue;
      foreach(var mesh in part.Meshes)for(int v=2;v<mesh.VertexData.Length;v+=8)
      { low=Math.Min(low,mesh.VertexData[v]);high=Math.Max(high,mesh.VertexData[v]); }
      return high>=low ? new S2MGatePartState(true,(high-low)/204.8f+.2f) : normal;
    }
  }
}
