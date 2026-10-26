// Run in an isolated Unity project with current Assets/Code and Settings copied in.
// Unity -batchmode -projectPath <project> -executeMethod LiveryValidation.Run -quit
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class LiveryValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  const string Output = Repo + "/Temp/LiveryValidation";
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Check(map.Actors.Count == 792, "Actor count changed");
      var ownership = S2MLiveryOwnership.Read(map);
      Check(!ownership.HasSavedPlayers, "Initial Chapter 8 unexpectedly has saved players");
      foreach (var pair in new[] { (1,1), (2,4), (3,7), (8,10), (10,9), (11,3) })
        Check(ownership.Estates[pair.Item1].Lord == pair.Item2, "Incorrect scenario ownership for estate " + pair.Item1);
      Check(map.Actors.Where(a => a.Type == "Archer").GroupBy(a => a.SavedLivery)
        .ToDictionary(g => g.Key, g => g.Count()).OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")
        .SequenceEqual(new[] { "1:21", "3:79", "4:24", "7:13", "9:110", "10:31" }), "Incorrect Chapter 8 archer colors");
      Check(map.Actors.Where(a => a.Type == "Knight").All(a => a.SavedLivery == a.PendingPlayer), "Knight ownership mismatch");
      var world = new GameObject("Livery validation map");
      var context = S2MLiveryContext.Create(map, Install, world.transform);
      CheckSavedPlayerNumbering(world.transform);
      Check(context.CrestArray.depth == 14, "Crest array dimensions");
      var red = map.Actors.First(a => a.Type == "Archer" && a.SavedLivery == 1);
      var green = map.Actors.First(a => a.Type == "Archer" && a.SavedLivery == 3);
      Check(context.ResolveActor(red).Crest == 1 && context.ResolveActor(green).Crest == 3, "Actor crest fallback");
      Check(context.ResolveEstate(10).Crest == 9 && context.ResolveEstate(3).Crest == 7, "Estate/lord conflation");
      var unknown = new ActorPlacement(); Set(unknown, "SavedLivery", -1);
      Check(context.ResolveActor(unknown).Crest == 13, "Unassigned actor received a lord crest");
      Set(unknown, "SavedLivery", (8 << 16) | 3);
      Check(context.ResolveActor(unknown).Crest == 8 && context.ResolveActor(unknown).Color == context.ResolveActor(green).Color, "Separate crest/color words lost");
      Set(unknown, "SavedLivery", unchecked((int)0xFFFF0003));
      Check(context.ResolveActor(unknown).Crest == 3, "Unset crest word fallback");
      var catalog = new S2MActorCatalog(Install);
      Check(catalog.Resolve(red).TeamGroup == 1, "Definition team group not retained");
      var knight = map.Actors.First(a => a.Type == "Knight" && a.IsMounted);
      Check(catalog.ResolveParts(knight).Count == 2, "Mounted knight assembly regressed");
      var converter = Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
      var data = new S2MActorAssetLoader().Load(map, Install, converterPath: converter);
      var actors = S2MActorRenderer.Render(data, world.transform);
      var actorMaterials = actors.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
      Check(actorMaterials.Any(m => m.GetFloat("_LiveryMode") == 1), "No team-colored unit groups");
      Check(actorMaterials.Any(m => m.GetFloat("_LiveryMode") == 3), "No crest surfaces");
      foreach (var batch in data.Batches)
        if (batch.Asset.ModelPath.EndsWith("horse_knight.GR2", StringComparison.OrdinalIgnoreCase))
          Check(batch.Placements.All(a => a.Type == "Knight"), "Horse lost rider ownership");
      var shader = Shader.Find("OpenSH2/Instanced Actor");
      Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Actor shader errors");
      // Put all real Chapter 8 units out of the preview, then show ten copies of one archer batch.
      foreach (var actor in map.Actors) actors.SetTransform(actor.Id, new Vector3(10000,0,10000), Quaternion.identity, Vector3.one);
      var samples = map.Actors.Where(a => a.Type == "Archer").Take(10).ToArray();
      for (int i = 0; i < samples.Length; i++)
      {
        Set(unknown, "SavedLivery", i + 1);
        actors.SetLivery(samples[i].Id, context.ResolveActor(unknown));
        actors.SetTransform(samples[i].Id, new Vector3((i % 5) * 2.5f,0,(i / 5) * 3), Quaternion.Euler(0,180,0), Vector3.one);
      }
      // A mounted knight demonstrates that horse, shield and rider share the same livery.
      actors.SetTransform(knight.Id, new Vector3(12.5f,0,0), Quaternion.Euler(0,150,0), Vector3.one);
      actors.SetLivery(knight.Id, context.ResolveActor(red));
      foreach (var t in world.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
      var camera = new GameObject("Livery preview").AddComponent<Camera>();
      camera.transform.position = new Vector3(8,9,-16); camera.transform.LookAt(new Vector3(6,1,1.5f));
      camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f,.18f,.2f);
      camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
      var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1,.92f,.8f);
      sun.transform.rotation = Quaternion.Euler(40,-30,0); RenderSettings.sun = sun;
      RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
      var target = new RenderTexture(1200,640,24); var image = new Texture2D(1200,640,TextureFormat.RGB24,false);
      actors.SendMessage("LateUpdate");
      RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
      RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1200,640),0,0); image.Apply();
      File.WriteAllBytes(Output + "/units.png", image.EncodeToPNG());
      var pixels = image.GetPixels();
      Check(pixels.Count(c => c.r > .15f && c.r > c.g * 1.6f && c.r > c.b * 1.6f) > 50, "No red unit pixels");
      Check(pixels.Count(c => c.g > .1f && c.g > c.r * 1.5f && c.g > c.b * 1.5f) > 50, "No green unit pixels");
      foreach (var message in ShaderUtil.GetShaderMessages(shader))
        Check(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
      UnityEngine.Object.DestroyImmediate(actors.gameObject);
      // Building material variants must be bounded by livery combinations and remove exporter placeholders.
      var buildingData = new S2MBuildingAssetLoader().Load(map, Install, converter);
      var buildings = S2MBuildingRenderer.Render(buildingData, world.transform, new S2MTerrainSettings { GenerateMeshColliders = false });
      var buildingMaterials = buildings.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
      Check(context.BuildingMaterialVariantCount > 0, "No building crest/color variants");
      Check(!buildingMaterials.Any(m => m.GetTexture("_BaseMap")?.name.Equals("crest", StringComparison.OrdinalIgnoreCase) == true), "Unreplaced building crest placeholder");
      var playerKeep = map.Keeps.First(k => S2MCampfireLayout.EstateAtBuilding(map,k) == 1);
      foreach (var t in buildings.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
      var center = new Vector3(playerKeep.MapX, playerKeep.RawY / 1024, playerKeep.MapZ);
      camera.transform.position = center + new Vector3(15,12,-18); camera.transform.LookAt(center + Vector3.up * 2);
      RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
      image.ReadPixels(new Rect(0,0,1200,640),0,0); image.Apply(); File.WriteAllBytes(Output + "/buildings.png",image.EncodeToPNG());
      File.WriteAllText(Output + "/owners.txt", string.Join("\n", map.Keeps.Select(k =>
        $"Keep {k.Id} estate {S2MCampfireLayout.EstateAtBuilding(map,k)} crest {context.ResolveBuilding(k).Crest}")));
      int variants = context.BuildingMaterialVariantCount;
      UnityEngine.Object.DestroyImmediate(buildings);
      var crowd = new S2MActorData(); Set(crowd, "Map", map);
      var crowdBatch = data.Batches.First(b => b.Placements[0].Type == "Archer");
      crowdBatch.Placements.Clear(); crowdBatch.Scales.Clear();
      for (int i = 0; i < 5000; i++)
      {
        var actor = new ActorPlacement(); Set(actor, "Id", -10000-i); Set(actor, "Type", "Archer"); Set(actor, "OnMap", true); Set(actor, "SavedLivery", i%10+1);
        crowdBatch.Placements.Add(actor); crowdBatch.Scales.Add(1);
      }
      crowd.Batches.Add(crowdBatch);
      var dense = S2MActorRenderer.Render(crowd, world.transform);
      Check(dense.ActorCount == 5000 && dense.DrawBatchCount == 20, "Dense mixed-livery batch split failed");
      Check(dense.GetComponentsInChildren<Transform>(true).Length < 20, "Per-unit scene objects allocated");
      Check(dense.SetLivery(-10000, context.ResolveActor(red)) && dense.HideActor(-10001), "Dynamic livery/visibility API");
      dense.SendMessage("LateUpdate");
      int materialCount = dense.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count();
      Check(materialCount == crowdBatch.Model.Meshes.Sum(m => m.TriangleGroups.Count) + crowdBatch.Attachments.Sum(a => a.model.Meshes.Sum(m => m.TriangleGroups.Count)), "Per-unit materials allocated");
      var crests = context.CrestArray;
      UnityEngine.Object.DestroyImmediate(world); Check(crests == null, "Crest array leaked");
      RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
      UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(sun.gameObject);
      File.WriteAllText(Output + "/pass.txt", $"PASS: Chapter 8 ownership, packed crests, material classification, GPU color rendering, building placeholders, {variants} shared building variants, 5,000 mixed-color archers in 20 batches with {materialCount} shared materials, cleanup.");
      if (File.Exists(Output + "/failure.txt")) File.Delete(Output + "/failure.txt");
      EditorApplication.Exit(0);
    }
    catch (Exception e) { File.WriteAllText(Output + "/failure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target,value);
  static void Check(bool value, string message) { if (!value) throw new Exception(message); }

  static void CheckSavedPlayerNumbering(Transform parent)
  {
    // Independent serialized fixtures: local human #0, Olaf #1, estates owned via object references.
    byte[] Bytes(Action<BinaryWriter> write) { using var s = new MemoryStream(); using var w = new BinaryWriter(s); write(w); return s.ToArray(); }
    void Field(BinaryWriter w, int id, byte[] body) { w.Write(id); w.Write(body.Length); w.Write(body); }
    RawS2Object Obj(int id, int type, string name, byte[] body)
    { var o = new RawS2Object { Payload = body }; Set(o,"Id",id); Set(o,"TypeIndex",type); Set(o,"Type",name); return o; }
    byte[] Player(int number, bool human, int code) => Bytes(w => {
      Field(w,0,Bytes(v => { v.Write(human); v.Write(human); v.Write(1); v.Write(Encoding.Unicode.GetBytes("P")); v.Write(number); }));
      w.Write(-15963); w.Write(20);
      Field(w,0,Bytes(v => { for(int i=0;i<31;i++)v.Write(0); v.Write(1); v.Write(code); }));
    });
    byte[] Estate(int number, int owner) => Bytes(w => {
      Field(w,0,Bytes(v => {v.Write(number);v.Write(0);}));
      Field(w,2,Bytes(v => v.Write(owner))); w.Write(-15963);w.Write(17);
      Field(w,8,Bytes(v => {v.Write(-1);v.Write(-1);}));
    });
    var map = new S2MFile();
    Set(map,"S2GameObjects",new S2Object[] {
      Obj(777,20,"S2Player",Player(0,true,-1)), Obj(778,20,"S2Player",Player(1,false,8)),
      Obj(100,17,"S2Estate",Estate(1,777)), Obj(101,17,"S2Estate",Estate(2,778)),
      Obj(1,1,"S2Game",Bytes(w => { w.Write(-15963);w.Write(1);Field(w,7,Bytes(v => {for(int i=0;i<11;i++)v.Write(i==0?5:i==1?2:i);})); }))
    });
    var context = S2MLiveryContext.Create(map,Install,parent);
    Check(context.Ownership.Players[0].LocalHuman && context.Ownership.Players[1].Lord==2,"Saved player identity decoding");
    Check(context.ResolveEstate(1).Crest==1 && context.ResolveEstate(2).Crest==2,"Saved estate object references/color order");
    var actor=new ActorPlacement();Set(actor,"SavedLivery",0);
    Check(context.ResolveActor(actor).Crest==1,"Player-zero must remain red/player crest");
    Set(actor,"SavedLivery",1);Check(context.ResolveActor(actor).Crest==2,"Olaf player-one must remain orange");
    UnityEngine.Object.DestroyImmediate(context.gameObject);
  }
}
