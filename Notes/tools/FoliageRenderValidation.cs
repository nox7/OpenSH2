// Copy into an isolated Unity project's Assets/Editor alongside OpenSH2's Code.
// Run Unity -batchmode -executeMethod FoliageRenderValidation.Run -quit.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FoliageRenderValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  static string Output => Repo + "/Temp/FoliageProbe";
  static string Converter => Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  public static void ScaleOnly()
  {
    Directory.CreateDirectory(Output);
    try
    {
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      var method = typeof(S2MVegetationRenderer).GetMethod("ToUnityInstance", BindingFlags.Static | BindingFlags.NonPublic);
      var scales = new S2MSceneryModelScales { Vegetation=2, Rocks=3, NaturalStone=4, Grass=5, Crops=6 };
      foreach (SceneryKind kind in Enum.GetValues(typeof(SceneryKind)))
      {
        var p = new SceneryInstance(); Set(p,"Kind",kind); Set(p,"RawX",10240f); Set(p,"RawY",2560f); Set(p,"RawZ",20480f);
        object[] args = {p,map,new S2MVegetationRenderSettings(),0,.5f,new S2MSceneryModelScales()};
        var before = (Granny2ModelInstance)method.Invoke(null,args);
        args[5]=scales;
        var after = (Granny2ModelInstance)method.Invoke(null,args);
        float expected=kind==SceneryKind.Shrubs?2:kind==SceneryKind.Rocks?3:kind==SceneryKind.NaturalStone?4:
          kind==SceneryKind.Grass?5:kind==SceneryKind.Forest?1:6;
        Check(after.Position==before.Position,"Model scaling moved " + kind);
        Check(after.Scale==before.Scale*expected,"Wrong category scale: " + kind);
      }
      var grassData = new S2MVegetationData(); Set(grassData,"Map",map);
      Set(grassData,"Grass",new[] {map.Grass.Instances[0]}); Set(grassData,"GrassTexturePath",Install+"/terrain/grass_bb.dds");
      var a = S2MVegetationRenderer.Render(grassData);
      var b = S2MVegetationRenderer.Render(grassData,modelScales:scales);
      Mesh am=a.GetComponentInChildren<MeshFilter>().sharedMesh, bm=b.GetComponentInChildren<MeshFilter>().sharedMesh;
      Check(am.vertices.SequenceEqual(bm.vertices),"Grass scaling moved anchors");
      Check(Vector2.Distance(bm.uv2[3],am.uv2[3]*5)<.00001f,"Grass corners did not scale");
      Check(Mathf.Abs(bm.bounds.size.y-am.bounds.size.y*5)<.00001f,"Grass culling bounds did not scale");
      UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
      var plant = new PlantActor(); Set(plant,"Kind",SceneryKind.Wheat); Set(plant,"Stage",1);
      Set(plant,"RawX",10240f); Set(plant,"RawY",2560f); Set(plant,"RawZ",20480f); Set(plant,"WheatOffsets",new float[8]);
      var wheatData = new S2MVegetationData(); Set(wheatData,"Map",map); Set(wheatData,"Wheat",new[] {plant});
      Set(wheatData,"WheatTexturePath",Install+"/meshes/buildings/farm_plants.transparent.dds");
      a=S2MVegetationRenderer.Render(wheatData); b=S2MVegetationRenderer.Render(wheatData,modelScales:scales);
      am=a.GetComponentInChildren<MeshFilter>().sharedMesh; bm=b.GetComponentInChildren<MeshFilter>().sharedMesh;
      Check(Vector3.Distance(bm.bounds.size,am.bounds.size*6)<.0001f,"Wheat geometry did not scale");
      Check(Mathf.Abs(am.bounds.min.y-bm.bounds.min.y)<.00001f,"Wheat ground anchor moved");
      UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
      foreach(float invalid in new[] {0f,-1f,float.NaN,float.PositiveInfinity})
      {
        bool rejected=false;
        try { S2MVegetationRenderer.Render(grassData,modelScales:new S2MSceneryModelScales {Grass=invalid}); }
        catch(ArgumentOutOfRangeException) { rejected=true; }
        Check(rejected,"Invalid scale accepted");
      }
      File.WriteAllText(Output+"/scale-validation.txt","PASS: category isolation, fixed positions, grass geometry/bounds, wheat geometry/ground anchor, invalid scales.\n");
      Debug.Log("SCENERY_SCALE_VALIDATION_PASS");
    }
    catch(Exception e) { File.WriteAllText(Output+"/scale-validation.txt","FAIL: "+e); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Check(map.Grass.Instances.Count == 2430 && map.Shrubs.Instances.Count == 51
        && map.Rocks.Instances.Count == 638 && map.NaturalStone.Instances.Count == 176, "Typed scenery counts");
      Check(map.PlantActors.Count == 207 && map.PlantActors.Count(p => p.Stage < 0) == 195, "Saved plant stages");
      var data = new S2MVegetationAssetLoader().Load(map, Install, Converter, Output + "/Cache");
      Check(data.SupportedInstanceCount == 4317 && data.UnsupportedInstanceCount == 0
        && data.InactiveInstanceCount == 197, "Active/inactive coverage");
      Check(data.Batches.Sum(b => b.Instances.Count) == 1887, "Model placement total");
      for (ushort f = 0; f < 14; f++)
        for (ushort v = 0; v < 8; v++)
          if (S2MVegetationAssetCatalog.TryGetTreeAsset(f, v, true, out string path))
            Check(File.Exists(Path.Combine(Install, path)), "Low detail fallback: " + path);
      var missingShader=(Shader)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Shader));
      Check(!ReferenceEquals(missingShader,null) && missingShader==null,"Unity missing-shader fixture");
      var renderSettings=new S2MVegetationRenderSettings(); renderSettings.Model.OpaqueShader=missingShader;
      var render = S2MVegetationRenderer.Render(data, settings: renderSettings, treeModelScale: .5f, naturalStoneTextureBrightness: 2f);
      foreach(Transform batch in render.transform)
        if(batch.name.Contains("rock_natural_stone"))
          Check(batch.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials)
            .All(m=>m.shader.name=="OpenSH2/Stronghold 2 Natural Stone" && m.GetFloat("_TextureBrightness")==2f),"Natural-stone shader/brightness selection failed");
      MeshFilter[] filters = render.GetComponentsInChildren<MeshFilter>();
      foreach (MeshRenderer mr in render.GetComponentsInChildren<MeshRenderer>())
        foreach (Material m in mr.sharedMaterials)
        {
          Check((m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture) != null, "Missing texture " + m.name);
          Check(!ShaderUtil.ShaderHasError(m.shader), "Shader error: " + m.shader.name);
        }
      int grassVertices = filters.Where(f => f.name.StartsWith("Grass ")).Sum(f => f.sharedMesh.vertexCount);
      Check(grassVertices == 2430 * 4, "Grass geometry count");
      Check(filters.Where(f => f.name.StartsWith("Grass ")).All(f => f.sharedMesh.bounds.size.y > 0), "Billboard bounds");
      ParserChecks(map);

      var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
      GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
      RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
      var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
      sun.transform.rotation = Quaternion.Euler(50, -35, 0); sun.intensity = 1.2f;
      var camera = new GameObject("Camera").AddComponent<Camera>(); camera.orthographic = true;
      camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.16f,.2f);
      camera.nearClipPlane = .01f; camera.farClipPlane = 1000;
      camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
      Preview(camera, new Vector3(128, 0, 128), new Vector3(-180,200,-180), 160, "map-scenery.png");
      UnityEngine.Object.DestroyImmediate(render);

      var galleryMap = new S2MFile { MapSize = 64 };
      Set(galleryMap,"HeightLayer",new HeightLayer { Width = 64, Height = 64, Heights = new float[4096] });
      var batches = new List<S2MVegetationBatch>();
      int n = 0;
      foreach (var batch in data.Batches)
      {
        var p = new SceneryInstance();
        Set(p, "Kind", batch.Instances[0].Kind); Set(p, "RawX", (2f + n % 8 * 4f) * 1024);
        Set(p, "RawZ", (2f + n / 8 * 4f) * 1024);
        var b = new S2MVegetationBatch(); Set(b,"AssetRelativePath",batch.AssetRelativePath); Set(b,"Model",batch.Model);
        Set(b,"Instances",new[] { p }); batches.Add(b); n++;
      }
      var grass = new List<SceneryInstance>();
      for (ushort i = 0; i < 11; i++)
      {
        var p = new SceneryInstance(); Set(p,"Kind",SceneryKind.Grass); Set(p,"Family",i);
        Set(p,"RawX",(2f+i*2f)*1024); Set(p,"RawZ",-2f*1024); grass.Add(p);
      }
      var gallery = new S2MVegetationData(); Set(gallery,"Map",galleryMap); Set(gallery,"Batches",batches);
      Set(gallery,"Grass",grass); Set(gallery,"GrassTexturePath",data.GrassTexturePath);
      var galleryRoot = S2MVegetationRenderer.Render(gallery);
      Preview(camera,new Vector3(15,0,10),new Vector3(-25,35,-35),25,"asset-gallery.png");
      foreach (Transform child in galleryRoot.transform) if(child.name != "Grass Billboards") child.gameObject.SetActive(false);
      Preview(camera,new Vector3(12,0,-2),new Vector3(0,7,-14),5,"grass-front.png");
      Preview(camera,new Vector3(12,0,-2),new Vector3(14,7,0),5,"grass-side.png");
      UnityEngine.Object.DestroyImmediate(galleryRoot);
      Check(Resources.FindObjectsOfTypeAll<Material>().All(m => m.name != "S2M Grass Atlas"),"Generated grass material leaked");
      CropChecks(galleryMap,camera);
      File.WriteAllText(Output + "/validation.txt", "PASS: 4317 active placements, 197 inactive states, zero unsupported; 1887 model instances, 2430 grass cards; installed textures, shader compilation, low-detail fallbacks, malformed arrays, cleanup and URP previews.\n");
      Debug.Log("FOLIAGE_VALIDATION_PASS");
    }
    catch (Exception e) { File.WriteAllText(Output + "/validation.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  static void CropChecks(S2MFile map,Camera camera)
  {
    var plants = new List<PlantActor>();
    foreach(var kind in new[] {SceneryKind.AppleTree,SceneryKind.Hops,SceneryKind.Wheat})
      for(int stage=0;stage<(kind==SceneryKind.AppleTree?5:kind==SceneryKind.Hops?7:6);stage++)
      {
        var p = new PlantActor(); Set(p,"Kind",kind); Set(p,"Stage",stage);
        Set(p,"RawX",(2f+stage*5f)*1024); Set(p,"RawZ",kind==SceneryKind.AppleTree?0f:kind==SceneryKind.Hops?8192f:16384f);
        Set(p,"WheatOffsets",new float[] {100,300,500,700,100,300,500,700}); plants.Add(p);
      }
    Set(map,"PlantActors",plants);
    var data = new S2MVegetationAssetLoader().Load(map,Install,Converter,Output+"/Cache");
    Check(data.SupportedInstanceCount==18 && data.UnsupportedInstanceCount==0 && data.Wheat.Count==6,"Positive crop stages");
    var render = S2MVegetationRenderer.Render(data);
    Preview(camera,new Vector3(16,0,8),new Vector3(-20,35,-35),23,"crop-stages.png");
    foreach(var mr in render.GetComponentsInChildren<MeshRenderer>())
      foreach(var m in mr.sharedMaterials) Check(!ShaderUtil.ShaderHasError(m.shader),"Crop shader error");
    UnityEngine.Object.DestroyImmediate(render);
  }
  static void ParserChecks(S2MFile map)
  {
    Type reader = typeof(S2MFile).Assembly.GetType("Assets.Code.Stronghold2.S2MReader.ObjectReaders.SceneryLayerReader");
    MethodInfo decode = reader.GetMethod("Decode", BindingFlags.Static | BindingFlags.NonPublic);
    foreach (SceneryLayer layer in new[] {map.Grass,map.Shrubs,map.Rocks,map.NaturalStone})
    {
      byte[] broken = layer.RawPayload.Take(layer.RawPayload.Length-1).ToArray();
      bool rejected = false;
      try { decode.Invoke(null,new object[] {layer.Type,broken}); }
      catch (TargetInvocationException e) { rejected = e.InnerException is InvalidDataException; }
      Check(rejected,"Truncated array accepted: " + layer.Type);
    }
  }
  static void Set(object target,string name,object value) => target.GetType().GetProperty(name).SetValue(target,value);
  static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
  static void Preview(Camera camera,Vector3 target,Vector3 offset,float size,string name)
  {
    camera.transform.position=target+offset; camera.transform.LookAt(target); camera.orthographicSize=size;
    var rt=new RenderTexture(1200,900,24);
    for(int i=0;i<2;i++) RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
    RenderTexture.active=rt; var image=new Texture2D(1200,900,TextureFormat.RGB24,false);
    image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply(); File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());
    RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
  }
}
