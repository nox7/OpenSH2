// Copy into an isolated Unity project's Assets/Editor alongside OpenSH2 Code.
// Unity -batchmode -executeMethod ActorRenderValidation.Run -quit
using System;
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

public static class ActorRenderValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  const string Output = Repo + "/Temp/ActorValidation";
  public static void Run()
  {
    try
    {
      Directory.CreateDirectory(Output);
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Check(map.Actors.Count == 792, "Chapter 8 actor decoding failed");
      Check(map.Actors.Count(a => a.Type == "Archer") == 278, "Lost archers");
      Check(map.Actors.Any(a => a.Type == "Dog") && map.Actors.Any(a => a.Type == "Sheep"), "Lost animals");
      Check(map.Actors.Count(a => a.Type == "Knight" && a.HasKnightState) == 60, "Knight section decoding failed");
      Check(map.Actors.Count(a => a.Type == "Knight" && a.SavedMounted) == 0, "Chapter 8 raw mount flags changed");
      Check(map.Actors.Count(a => a.Type == "Knight" && a.MountAtMapStart) == 54, "Campaign startup mount classification failed");
      Check(map.Actors.Count(a => a.Type == "Knight" && !a.IsMounted) == 6, "Elevated knights should remain on foot");
      Check(map.Actors.Where(a => a.Type == "Knight" && a.MountAtMapStart).All(a => a.SavedWalkingClass == 0), "Startup mounted a knight outside open walking space");
      Check(map.Actors.Where(a => a.Type == "Knight" && !a.IsMounted).All(a => a.SavedWalkingClass == 5), "Expected six walkway knights");
      var catalog = new S2MActorCatalog(Install);
      var footKnight = map.Actors.First(a => a.Type == "Knight" && !a.IsMounted);
      var mountedKnight = map.Actors.First(a => a.Type == "Knight" && a.IsMounted);
      Check(catalog.ResolveParts(footKnight).Count == 1, "Foot knight received a horse");
      Check(catalog.ResolveParts(mountedKnight).Count == 2, "Mounted knight lost horse (state cache collision)");
      Check(catalog.Resolve(mountedKnight).AnimationPath.Contains("knight_horseback_idle"), "Mounted rider clip selection failed");
      Check(catalog.Resolve(mountedKnight).Attachments.Count > 0, "Mounted equipment omitted");
      var explicitMount = new ActorPlacement(); Set(explicitMount, "Type", "Knight"); Set(explicitMount, "SavedMounted", true);
      Set(explicitMount, "AnimationId", 461); Set(explicitMount, "NavigationLayer", (byte)1);
      var walkingMount = catalog.ResolveParts(explicitMount);
      Check(walkingMount[0].AnimationPath.Contains("knight_horseback_walk") && walkingMount[1].AnimationPath.Contains("horse_knight_walk"), "Saved mounted walking pair failed");
      var dog = new ActorPlacement(); Set(dog, "Type", "Dog"); Set(dog, "AnimationId", 18);
      Check(catalog.Resolve(dog).AnimationPath.EndsWith("hunters_dog_idle.GR2", StringComparison.OrdinalIgnoreCase), "Animal action number interpreted as AML row");
      var timer = System.Diagnostics.Stopwatch.StartNew();
      var data = new S2MActorAssetLoader().Load(map, Install, converterPath: Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",
        cacheDirectory: Output + "/Cache");
      File.WriteAllLines(Output + "/coverage.txt", data.Batches.Select(b => b.Asset.Key + " : " + b.Placements.Count)
        .Concat(data.Issues).Prepend($"Actors={map.Actors.Count}, rendered={data.PlacedCount}, inactive={data.InactiveCount}, load={timer.Elapsed.TotalSeconds:F3}s"));
      Check(data.PlacedCount == map.Actors.Count, "Insufficient actor asset coverage");
      var renderer = S2MActorRenderer.Render(data);
      Check(renderer.ActorCount == data.PlacedCount, "Instance loss");
      var scaled = S2MActorRenderer.Render(data, modelScaleMultiplier: 2);
      var drawField = typeof(S2MActorRenderer).GetField("draws", BindingFlags.NonPublic | BindingFlags.Instance);
      object FirstDraw(S2MActorRenderer r) => ((System.Collections.IEnumerable)drawField.GetValue(r)).Cast<object>().First();
      Matrix4x4 FirstMatrix(S2MActorRenderer r) => ((Matrix4x4[])FirstDraw(r).GetType().GetField("Local").GetValue(FirstDraw(r)))[0];
      var originalMatrix = FirstMatrix(renderer); var scaledMatrix = FirstMatrix(scaled);
      Check(Vector3.Distance(originalMatrix.GetColumn(3), scaledMatrix.GetColumn(3)) < .0001f, "Unit scaling moved placement anchors");
      Check(Vector3.Distance(originalMatrix.lossyScale * 2, scaledMatrix.lossyScale) < .0001f, "Unit scale multiplier failed");
      UnityEngine.Object.DestroyImmediate(scaled.gameObject);
      Check(renderer.GetComponentsInChildren<Transform>(true).Length < 200, "Per-actor objects allocated");
      Check(renderer.SetPlayback(map.Actors.First(a => a.Type == "Archer").Id, 2), "Playback API failed");
      var template = Resources.Load<Material>("InstancedActor");
      Check(template != null && template.enableInstancing, "Runtime material does not preserve instancing variants");
      var shader = Shader.Find("OpenSH2/Instanced Actor");
      Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Actor shader has errors");
      foreach (var batch in data.Batches)
      {
        Check(batch.Motion.Matrices.Length == batch.Motion.Bones * batch.Motion.Frames * 16, "Pose dimensions");
        int stride = batch.Motion.Bones * 16;
        if (!batch.Asset.HoldPose) Check(batch.Motion.Matrices.Take(stride).Where((v,i) => Math.Abs(v - batch.Motion.Matrices[stride * (batch.Motion.Frames / 2) + i]) > .0001).Any(), "Static animation export");
      }
      // Draw mounted and dismounted knights at their shared authored origins.
      var cameraObject = new GameObject("Actor validation camera"); var camera = cameraObject.AddComponent<Camera>();
      camera.transform.position = new Vector3(5, 3, 7); camera.transform.LookAt(new Vector3(0, 1.5f, 0));
      camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f,.18f,.2f);
      var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(45, -25, 0);
      RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
      foreach (var a in map.Actors) renderer.SetTransform(a.Id, new Vector3(10000,0,10000), Quaternion.identity, Vector3.one);
      renderer.SetTransform(mountedKnight.Id, new Vector3(-1.5f,0,0), Quaternion.Euler(0,180,0), Vector3.one);
      renderer.SetTransform(footKnight.Id, new Vector3(1.5f,0,0), Quaternion.Euler(0,180,0), Vector3.one);
      renderer.SetPlayback(mountedKnight.Id, 0); renderer.SetPlayback(footKnight.Id, 0);
      var target = new RenderTexture(640,640,24); camera.targetTexture = target;
      typeof(S2MActorRenderer).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(renderer, null);
      camera.Render();
      RenderTexture.active = target;
      var image = new Texture2D(640,640,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,640,640),0,0); image.Apply();
      File.WriteAllBytes(Output + "/knights.png", image.EncodeToPNG());
      RenderTexture.active = null; camera.targetTexture = null;
      foreach (var error in ShaderUtil.GetShaderMessages(shader)) Check(error.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, error.message);
      File.WriteAllText(Output + "/pass.txt", $"PASS: {data.PlacedCount} actors in {renderer.DrawBatchCount} spatial batches; {renderer.GetComponentsInChildren<Transform>(true).Length} transforms; native pose sampling, skin weights, shader, playback, map coverage; 54 mounted / 6 foot knights.");
      var generatedMesh = renderer.GetComponentInChildren<MeshFilter>(true).sharedMesh;
      var generatedMaterial = renderer.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
      var generatedPose = generatedMaterial.GetTexture("_ActorPoses");
      UnityEngine.Object.DestroyImmediate(renderer.gameObject);
      Check(generatedMesh == null && generatedMaterial == null && generatedPose == null, "Generated actor GPU resources leaked");
      UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(light.gameObject);
      UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
      // A dense 5,000-actor crowd must split at the instancing API's 1,023 limit.
      var crowdData = new S2MActorData(); Set(crowdData, "Map", map);
      var crowdBatch = data.Batches.First(b => b.Placements[0].Type == "Archer");
      crowdBatch.Placements.Clear(); crowdBatch.Scales.Clear();
      for (int i = 0; i < 5000; i++)
      {
        var actor = new ActorPlacement(); Set(actor, "Id", i + 1000000); Set(actor, "Type", "Archer");
        Set(actor, "RawX", (i % 50) * 100f); Set(actor, "RawZ", (i / 50) * 100f); Set(actor, "OnMap", true);
        Set(actor, "AnimationLoops", true); crowdBatch.Placements.Add(actor); crowdBatch.Scales.Add(1);
      }
      crowdData.Batches.Add(crowdBatch);
      var crowd = S2MActorRenderer.Render(crowdData);
      Check(crowd.ActorCount == 5000 && crowd.DrawBatchCount == 5, "Instancing limit/count failed");
      Check(crowd.GetComponentsInChildren<Transform>(true).Length < 10, "Crowd allocated individual scene objects");
      var tick = (Action)Delegate.CreateDelegate(typeof(Action), crowd, typeof(S2MActorRenderer).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance));
      // Exercise deferred playback uploads for many changes in the same population batch.
      for (int i = 0; i < 5000; i++) Check(crowd.SetPlayback(i + 1000000, i % 7), "Crowd playback update failed");
      tick(); long before = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
      for (int frame = 0; frame < 10; frame++) tick();
      double milliseconds = timer.Elapsed.TotalMilliseconds / 10; long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
      Check(allocated < 4096, "Per-frame managed allocation in population rendering: " + allocated);
      File.AppendAllText(Output + "/pass.txt", $"\nCrowd PASS: 5,000 actors, {crowd.DrawBatchCount} batches, {crowd.GetComponentsInChildren<Transform>(true).Length} transforms, CPU submission {milliseconds:F3}ms/frame, {allocated} managed bytes over 10 frames (editor; not a GPU benchmark).");
      UnityEngine.Object.DestroyImmediate(crowd.gameObject);
      if (File.Exists(Output + "/failure.txt")) File.Delete(Output + "/failure.txt");
      EditorApplication.Exit(0);
    }
    catch (Exception e) { File.WriteAllText(Output + "/failure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
  static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
