using System;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Shared render/worker anchor. Saved terrain height is retained for navigation;
  /// only the rig's visible assembly is seated on the decoded swamp surface.</summary>
  public static class S2MPitchRigLayout
  {
    public static bool HasPitch(S2MFile map,BuildingPlacement building)
    {
      int count=0;
      foreach(var cell in Cells(map,building))
      {if(map.WaterLayer.GetWaterType(cell.x,cell.z)!=S2MWaterType.PitchSwamp)return false;count++;}
      return count==4;
    }
    public static float RawHeight(S2MFile map,BuildingPlacement building,S2MWaterSettings settings=null)
    {
      if(map?.HeightLayer?.CornerHeightPlanes==null || map.WaterLayer==null)return building.RawY;
      settings??=new S2MWaterSettings();float raw=building.RawY;
      foreach(var cell in Cells(map,building))
      {
        if(map.WaterLayer.GetWaterType(cell.x,cell.z)!=S2MWaterType.PitchSwamp)continue;
        for(int corner=0;corner<4;corner++)
          raw=Math.Max(raw,map.HeightLayer.GetCornerHeight(settings.SurfaceHeightPlane,cell.z*map.HeightLayer.Width+cell.x,corner)
            +(settings.PitchSwampSurfaceOffset+.02f)/settings.HeightUnitScale);
      }
      return raw;
    }
    private static System.Collections.Generic.IEnumerable<(int x,int z)> Cells(S2MFile map,BuildingPlacement building)
    {
      if(map?.WaterLayer==null)yield break;
      for(int z=0;z<2;z++)for(int x=0;x<2;x++)
      {
        var point=S2MNavigationBuilder.BuildingPoint(building,x+.5f,z+.5f);
        int cx=(int)Math.Floor(point.X),cz=(int)Math.Floor(point.Z);
        if(cx>=0 && cz>=0 && cx<map.WaterLayer.Width && cz<map.WaterLayer.Height)yield return(cx,cz);
      }
    }
  }
}
