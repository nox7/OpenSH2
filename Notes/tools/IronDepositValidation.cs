// Isolated Unity editor harness: Unity -batchmode -executeMethod IronDepositValidation.Run -quit
using System;
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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class IronDepositValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Output=Repo+"/Temp/IronDepositProbe";
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      int total=0;
      for(int chapter=1;chapter<=12;chapter++)
      {
        var map=S2MFileLoader.Load($"C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter{chapter}.s2m");
        var original=(byte[])map.WaterLayer.SurfaceMaterialIds.Clone();
        var features=(byte[])map.WaterLayer.LandscapeFeatureValues.Clone();
        var effective=S2MTerrainSurfaceMaterialGrid.Create(map);
        int cells=features.Count(v=>v==35);total+=cells;
        for(int i=0;i<features.Length;i++) if(features[i]==35 && original[i]!=39) Check(effective[i]==35,"Iron texture missing");
        Check(original.SequenceEqual(map.WaterLayer.SurfaceMaterialIds)&&features.SequenceEqual(map.WaterLayer.LandscapeFeatureValues),"Source grids mutated");
        if(chapter!=8 && chapter!=10 && chapter!=5)continue;
        const string install="C:/Steam/steamapps/common/Stronghold 2";
        var loader=new S2MVegetationAssetLoader();
        var data=loader.Load(map,install,Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",Output+"/Cache");
        var ore=data.Batches.Where(b=>b.Instances.Any(p=>p.Kind==SceneryKind.IronOre)).ToArray();
        var placements=ore.SelectMany(b=>b.Instances).Where(p=>p.Kind==SceneryKind.IronOre).ToArray();
        var saved=(map.Rocks?.Instances??Array.Empty<SceneryInstance>()).Where(p=>p.Enabled&&p.Family==8&&p.Variant<12)
          .Select(p=>((int)p.MapX,(int)p.MapZ)).Where(c=>c.Item1>=0&&c.Item2>=0&&c.Item1<map.MapSize&&c.Item2<map.MapSize).Distinct().ToArray();
        int alreadyCovered=saved.Count(c=>features[c.Item2*map.MapSize+c.Item1]==35);
        Check(placements.Length==cells-alreadyCovered,"Iron model placement count");
        Check(placements.All(p=>p.Family==8&&p.Variant<12&&p.SampleTerrainHeight),"Invalid iron placement");
        Check(placements.All(p=>features[(int)p.MapZ*map.MapSize+(int)p.MapX]==35),"Iron placed outside deposits");
        Check(placements.Select(p=>((int)p.MapX,(int)p.MapZ)).Distinct().Count()==placements.Length,"Duplicate deposit models");
        if(chapter==5) {Check(ore.Length==0,"Loaded ore models for an ore-free map");continue;}
        var again=loader.Load(map,install,Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",Output+"/Cache");
        Check(placements.Select(p=>(p.RawX,p.RawZ,p.RotationDegrees,p.Variant)).SequenceEqual(
          again.Batches.SelectMany(b=>b.Instances).Where(p=>p.Kind==SceneryKind.IronOre).Select(p=>(p.RawX,p.RawZ,p.RotationDegrees,p.Variant))),"Unstable iron variation");
        var subset=new S2MVegetationData();Set(subset,"Map",map);Set(subset,"Batches",ore);
        var world=new GameObject("Iron validation");
        var rocks=S2MVegetationRenderer.Render(subset,world.transform,modelScales:new S2MSceneryModelScales {Rocks=.5f});
        foreach(var mr in rocks.GetComponentsInChildren<MeshRenderer>())
          foreach(var material in mr.sharedMaterials)
          {
            Check(!material.IsKeywordEnabled("_ALPHATEST_ON"),"Ore uses cutout foliage materials");
            Check(material.GetTexture("_BaseMap")!=null,"Ore texture missing");
            Check(!ShaderUtil.ShaderHasError(material.shader),"Ore shader error");
          }
        var textures=new S2MTerrainTextureAssetLoader().Load(map,install);
        Check(textures.Textures.ContainsKey(35)&&!textures.UnsupportedMaterialIds.Contains((byte)35),"Iron DDS not loaded");
        var terrain=S2MTerrainRenderer.Render(map,world.transform);
        S2MTerrainTextureRenderer.Apply(terrain,textures);
        var array=(Texture2DArray)terrain.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_TerrainTextureArray");
        Check(array.depth==28 && S2MTerrainTextureAssetCatalog.GetMaterialIdForSlice(27)==35,"Iron array slice missing");
        Check(!ShaderUtil.ShaderHasError(terrain.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader),"Terrain shader error");
        if(chapter==8) Preview(world,placements[0],map);
        var state=world.GetComponent<S2MSceneryOcclusion>().Occupancy;
        var building=new BuildingPlacement();Set(building,"Id",int.MaxValue);Set(building,"RawX",Mathf.Floor(placements[0].MapX)*1024);
        Set(building,"RawZ",Mathf.Floor(placements[0].MapZ)*1024);Set(building,"FootprintWidth",1);Set(building,"FootprintHeight",1);
        int before=rocks.GetComponentsInChildren<MeshRenderer>().Count(r=>r.forceRenderingOff);
        state.SetBuilding(building);Check(rocks.GetComponentsInChildren<MeshRenderer>().Count(r=>r.forceRenderingOff)>before,"Ore building coverage missing");
        state.RemoveBuilding(int.MaxValue);Check(rocks.GetComponentsInChildren<MeshRenderer>().Count(r=>r.forceRenderingOff)==before,"Ore did not return after removal");
        UnityEngine.Object.DestroyImmediate(world);
      }
      File.WriteAllText(Output+"/validation.txt",$"PASS: tag-19 iron materials across all 12 campaigns ({total} cells); chapter 8/10 ore placements, stable variants, installed meshes and DDS, 28-slice terrain array, opaque materials, shader compilation, building coverage/removal, ore-free chapter 5.\n");
      Debug.Log("IRON_DEPOSIT_VALIDATION_PASS");
    }
    catch(Exception e){File.WriteAllText(Output+"/validation.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static void Preview(GameObject world,SceneryInstance first,S2MFile map)
  {
    var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
    GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
    var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-35,0);light.intensity=1.2f;
    var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.orthographic=true;camera.orthographicSize=7;
    var target=new Vector3(first.MapX,map.HeightLayer.Heights[(int)first.MapZ*map.MapSize+(int)first.MapX]/1024f,first.MapZ);
    camera.transform.position=target+new Vector3(-8,12,-8);camera.transform.LookAt(target);
    var rt=new RenderTexture(900,700,24);
    for(int i=0;i<2;i++)RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
    RenderTexture.active=rt;var image=new Texture2D(900,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,700),0,0);image.Apply();
    File.WriteAllBytes(Output+"/iron-deposit.png",image.EncodeToPNG());RenderTexture.active=null;
    UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);
  }
  static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
