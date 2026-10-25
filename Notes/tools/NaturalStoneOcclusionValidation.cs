// Copy to an isolated Unity project's Assets/Editor alongside OpenSH2's Code.
// Unity -batchmode -executeMethod NaturalStoneOcclusionValidation.Run -quit
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;

public static class NaturalStoneOcclusionValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  const string Output = Repo + "/Temp/StoneOcclusionProbe";

  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      OccupancyChecks();
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      string converter = Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
      var data = new S2MVegetationAssetLoader().Load(map, Install, converter, Repo + "/Temp/FoliageProbe/Cache");
      var buildingData = new S2MBuildingAssetLoader().Load(map, Install, converter, Output + "/Cache");
      var world = new GameObject("Occlusion world");
      world.transform.localScale = Vector3.one * .7f;
      world.transform.rotation = Quaternion.Euler(0, 27, 0);
      var vegetation = S2MVegetationRenderer.Render(data, world.transform, new S2MVegetationRenderSettings {FlipX=true});
      var occupancy = world.GetComponent<S2MSceneryOcclusion>().Occupancy;
      int Hidden() => vegetation.GetComponentsInChildren<MeshRenderer>().Count(r => r.forceRenderingOff);
      int initialHidden = Hidden();
      Check(initialHidden > 0, "Loaded building footprints did not hide any stone");
      foreach(Transform batch in vegetation.transform)
      {
        var source = data.Batches.FirstOrDefault(b => b.AssetRelativePath.Replace('\\','/') == batch.name);
        if(source == null) continue;
        for(int i=0;i<source.Instances.Count;i++)
        {
          SceneryInstance p=source.Instances[i];
          bool hidden=p.Kind==SceneryKind.NaturalStone && occupancy.IsOccupied(Mathf.FloorToInt(p.MapX),Mathf.FloorToInt(p.MapZ));
          foreach(var renderer in batch.GetChild(i).GetComponentsInChildren<MeshRenderer>())
            Check(renderer.forceRenderingOff==hidden,"Wrong stone/category coverage");
        }
      }
      var buildings = S2MBuildingRenderer.Render(buildingData, world.transform, new S2MTerrainSettings {FlipX=true});
      Check(Hidden()==initialHidden,"Building/scenery load order changed occupancy");
      var placements=map.BuildingPlacements.ToDictionary(p=>p.Id);
      var presences=buildings.GetComponentsInChildren<S2MBuildingPresence>();
      foreach(var presence in presences)
      {
        BuildingPlacement p=placements[presence.BuildingId];
        Vector3 expected=world.transform.TransformPoint(new Vector3(map.MapSize-p.MapX,p.RawY/1024f,p.MapZ));
        foreach(var mesh in presence.GetComponentsInChildren<MeshFilter>(true))
          Check(Vector3.Distance(mesh.transform.TransformPoint(Vector3.zero),expected)<.001f,"Grouping moved a building model/collider");
      }
      S2MBuildingPresence quarry = null;
      foreach(var candidate in presences.Where(p=>placements[p.BuildingId].Type=="StoneQuarry"))
      {
        candidate.gameObject.SetActive(false);
        bool uncovered=Hidden()<initialHidden;
        candidate.gameObject.SetActive(true);
        if(uncovered) {quarry=candidate;break;}
      }
      Check(quarry!=null,"No quarry fixture uncovered stone");
      var exterior=quarry.GetComponentsInChildren<MeshRenderer>().First(r=>r.enabled).gameObject;
      exterior.SetActive(false); Check(Hidden()==initialHidden,"Changing the building view released its footprint");
      exterior.SetActive(true);
      quarry.gameObject.SetActive(false); int afterDisable=Hidden();
      Check(afterDisable<initialHidden,"Disabling quarry did not restore stone");
      quarry.gameObject.SetActive(true); Check(Hidden()==initialHidden,"Re-enabling quarry did not hide stone");
      int quarryId=quarry.BuildingId;
      UnityEngine.Object.DestroyImmediate(quarry.gameObject);
      Check(Hidden()==afterDisable,"Destroying quarry did not restore stone");
      var replacement=new GameObject("New quarry"); replacement.transform.SetParent(buildings.transform,false);
      replacement.AddComponent<S2MBuildingPresence>().Initialize(occupancy,placements[quarryId]);
      Check(Hidden()==initialHidden,"New building placement did not hide stone");
      UnityEngine.Object.DestroyImmediate(replacement);
      Check(Hidden()==afterDisable,"Removing new building did not restore stone");
      foreach(var p in map.BuildingPlacements) occupancy.RemoveBuilding(p.Id);
      Check(Hidden()==0,"Stones remained hidden after all buildings were removed");
      Check(data.Batches.Sum(b=>b.Instances.Count(p=>p.Kind==SceneryKind.NaturalStone))==176,"Source natural stone records were removed");
      UnityEngine.Object.DestroyImmediate(world);
      Check(typeof(S2MBuildingOccupancy).GetField("FootprintChanged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(occupancy)==null,"Destroyed vegetation kept an event subscription");
      world=new GameObject("Buildings first world");
      buildings=S2MBuildingRenderer.Render(buildingData,world.transform);
      vegetation=S2MVegetationRenderer.Render(data,world.transform);
      Check(Hidden()==initialHidden,"Buildings-first load order changed stone visibility");
      UnityEngine.Object.DestroyImmediate(world);
      File.WriteAllText(Output+"/validation.txt",$"PASS: {initialHidden} hidden stone renderers at load; real quarry disable/re-enable/destroy/new placement; rotated/scaled/flipped map, unchanged model/collider positions, view switching, categories, load order, all 176 stone placements retained. Overlap/move/edge/rotation/idempotence/replacement-owner checks also passed.\n");
      Debug.Log("NATURAL_STONE_OCCLUSION_VALIDATION_PASS");
    }
    catch(Exception e) {File.WriteAllText(Output+"/validation.txt","FAIL: "+e); Debug.LogException(e);EditorApplication.Exit(1);}
  }

  static void OccupancyChecks()
  {
    var map=new S2MFile {MapSize=16};
    var state=new S2MBuildingOccupancy(map);
    BuildingPlacement a=Placement(1,4,4,2,3,0), b=Placement(2,5,5,2,2,0);
    state.SetBuilding(a);state.SetBuilding(a);state.SetBuilding(b);
    Check(state.IsOccupied(4,4)&&!state.IsOccupied(6,4),"Half-open footprint boundary");
    state.RemoveBuilding(1);Check(state.IsOccupied(5,5)&&!state.IsOccupied(4,4),"Overlap restored too early");
    Set(b,"RawX",10*1024f);state.SetBuilding(b);
    Check(!state.IsOccupied(5,5)&&state.IsOccupied(10,5),"Moving building left stale coverage");
    state.RemoveBuilding(2);state.RemoveBuilding(2);Check(!state.IsOccupied(10,5),"Removal is not idempotent");
    for(int quarter=0;quarter<4;quarter++)
    {
      a=Placement(1,8,8,2,3,quarter*90);
      state.SetBuilding(a);
      int x=8-(quarter==2||quarter==3?2:0), z=8-(quarter==1||quarter==2?3:0);
      Check(state.IsOccupied(x,z)&&!state.IsOccupied(x+2,z)&&!state.IsOccupied(x,z+3),"Rotated footprint");
      state.RemoveBuilding(1);
    }
    a=Placement(1,-1,-1,2,2,0);state.SetBuilding(a);
    Check(state.IsOccupied(0,0)&&!state.IsOccupied(-1,-1),"Map edge clipping");state.RemoveBuilding(1);
    a=Placement(1,4,4,2,3,0);
    var old=new GameObject("Old building");old.AddComponent<S2MBuildingPresence>().Initialize(state,a);
    var next=new GameObject("Replacement building");next.AddComponent<S2MBuildingPresence>().Initialize(state,a);
    UnityEngine.Object.DestroyImmediate(old);Check(state.IsOccupied(4,4),"Old owner removed replacement coverage");
    UnityEngine.Object.DestroyImmediate(next);Check(!state.IsOccupied(4,4),"Replacement owner did not release coverage");
  }
  static BuildingPlacement Placement(int id,float x,float z,int width,int height,float yaw)
  {
    var p=new BuildingPlacement();Set(p,"Id",id);Set(p,"RawX",x*1024);Set(p,"RawZ",z*1024);
    Set(p,"FootprintWidth",width);Set(p,"FootprintHeight",height);Set(p,"RotationDegrees",yaw);return p;
  }
  static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
  static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
}
