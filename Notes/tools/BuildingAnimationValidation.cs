// Copy into an isolated Unity project's Assets/Editor alongside the current Code and Settings.
// Unity -batchmode -executeMethod BuildingAnimationValidation.Run
using System;
using System.IO;
using System.Linq;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildingAnimationValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  const string Output = Repo + "/Temp/BuildingAnimationValidation";
  public static void Run()
  {
    try
    {
      QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"PC"),true);
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      var mill = map.BuildingPlacements.First(p => p.Type == "Mill");
      var isolated = new S2MFile { MapSize = map.MapSize };
      Set(isolated,"HeightLayer",map.HeightLayer); Set(isolated,"Landscape",map.Landscape);
      Set(isolated,"BuildingPlacements",new[] {mill}); Set(isolated,"S2GameObjects",map.S2GameObjects);
      var settings = new S2MBuildingRenderSettings { ViewMode = BuildingViewMode.Closed };
      var data = new S2MBuildingAssetLoader().Load(isolated, Install,
        Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe", Output + "/Cache", settings.AmbientAnimations);
      Check(data.Batches.Count == 1 && data.Batches[0].AmbientAnimations.Count == 2, "Missing windmill loops");
      var world = S2MBuildingRenderer.Render(data, terrainSettings: new S2MTerrainSettings { GenerateMeshColliders = false }, settings: settings);
      foreach (var t in world.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
      var clocks = world.GetComponentsInChildren<S2MBuildingAmbientAnimation>();
      Check(clocks.Length == 2 && clocks.All(c => c.InstanceCount == 1), "Animation setup duplicated parts or instances");
      var animated = world.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterial.shader.name == "OpenSH2/Instanced Actor").ToArray();
      Check(animated.Length == 2, "Animated/static part duplication");
      foreach (var r in animated) Check(r.GetComponent<MeshFilter>().sharedMesh.uv2.Length > 0, "Missing GPU skin channels");
      var camera = new GameObject("Mill animation camera").AddComponent<Camera>();
      var center = new Vector3(mill.MapX, mill.RawY / 1024, mill.MapZ);
      camera.transform.position = center + Quaternion.Euler(0,mill.RotationDegrees,0) * new Vector3(12,9,-15);
      camera.transform.LookAt(center + Vector3.up * 3);
      camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.16f,.2f);
      camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
      var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45,-25,0);
      RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
      var target = new RenderTexture(640,640,24); var image = new Texture2D(640,640,TextureFormat.RGB24,false);
      Color[] Capture(string file)
      {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; image.ReadPixels(new Rect(0,0,640,640),0,0); image.Apply();
        File.WriteAllBytes(Output + "/" + file, image.EncodeToPNG()); return image.GetPixels();
      }
      var before = Capture("mill-before.png");
      float sample = data.Batches[0].AmbientAnimations.Values.First().Duration * .137f;
      foreach (var clock in clocks) clock.SetTime(sample);
      var after = Capture("mill-after.png");
      int changed = before.Where((c,i) => Mathf.Abs(c.r-after[i].r)+Mathf.Abs(c.g-after[i].g)+Mathf.Abs(c.b-after[i].b) > .08f).Count();
      Check(changed > 100, "No visible GPU animation: " + changed);
      var block = new MaterialPropertyBlock();
      foreach (var clock in clocks) { Check(clock.SetPlaying(mill.Id,false), "Pause ID lookup failed"); clock.Advance(1); }
      foreach (var r in animated) { r.GetPropertyBlock(block); Check(block.GetVector("_ActorPlayback").y == 0, "Pause did not freeze instance playback"); }
      foreach (var clock in clocks) Check(clock.SetPlaying(mill.Id,true), "Resume ID lookup failed");
      foreach (var r in animated) { r.GetPropertyBlock(block); var p = block.GetVector("_ActorPlayback"); Check(Mathf.Abs(p.x+r.sharedMaterial.GetFloat("_ActorClock")-sample)<.001f,"Resume jumped in time"); }
      foreach (var clock in clocks) clock.Advance(.01f);
      long allocated = GC.GetAllocatedBytesForCurrentThread();
      for (int i=0;i<10;i++) foreach (var clock in clocks) clock.Advance(.01f);
      allocated = GC.GetAllocatedBytesForCurrentThread()-allocated;
      Check(allocated==0,"Per-frame animation allocations: "+allocated);
      var mesh = animated[0].GetComponent<MeshFilter>().sharedMesh; var pose = animated[0].sharedMaterial.GetTexture("_ActorPoses");
      UnityEngine.Object.DestroyImmediate(world); Check(mesh==null && pose==null,"Animation resources leaked");
      settings.AmbientAnimations.Enabled=false;
      var staticWorld=S2MBuildingRenderer.Render(data,terrainSettings:new S2MTerrainSettings {GenerateMeshColliders=false},settings:settings);
      Check(staticWorld.GetComponentsInChildren<S2MBuildingAmbientAnimation>().Length==0,"Disable setting ignored");
      UnityEngine.Object.DestroyImmediate(staticWorld);
      RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
      UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(sun.gameObject);
      File.WriteAllText(Output + "/pass.txt",$"PASS: two authored windmill loops, {changed} changed image pixels, pause/resume continuity, zero frame allocations, disabled fallback and GPU resource cleanup.");
      if(File.Exists(Output+"/failure.txt"))File.Delete(Output+"/failure.txt");
      EditorApplication.Exit(0);
    }
    catch(Exception e) { File.WriteAllText(Output+"/failure.txt",e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
  static void Check(bool value,string message) {if(!value)throw new Exception(message);}
}
