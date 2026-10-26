// Copy to Assets/Editor in the isolated Unity validation project.
// Unity -batchmode -executeMethod SimulationValidation.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;

public static class SimulationValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  const string Output = Repo + "/Temp/SimulationValidation";
  const string Converter = Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  public static void Run()
  {
    try
    {
      Directory.CreateDirectory(Output);
      var clock = new SimulationClock(); clock.Advance(.5f); clock.IsPaused = true; clock.Advance(10);
      Check(clock.DeltaTime == 0 && clock.ElapsedSeconds == .5, "Pause advanced game time");
      clock.IsPaused = false; clock.Advance(.25f);
      Check(clock.ElapsedSeconds == .75 && clock.DeltaTime == .25f, "Resume caught up paused time");
      var population = new CampfirePopulation(1, 1, 3);
      Check(population.Capacity == 32 && population.Seats.Count(s => s.Ring == 0) == 8
        && population.Seats.Count(s => s.Ring == 1) == 14 && population.Seats.Count(s => s.Ring == 2) == 10, "Ring population");
      foreach (var seat in population.Seats)
      {
        Vector3 forward = Quaternion.Euler(0, seat.HeadingDegrees, 0) * Vector3.forward;
        Check(Vector3.Dot(forward, -new Vector3(seat.X, 0, seat.Z).normalized) > .999, "Seat faces away from fire");
      }
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Check(map.Keeps.All(k => k.HasCampfirePosition), "Missing serialized campfire transform");
      var layouts = S2MCampfireLayout.Create(map);
      Check(layouts.Select(c => c.Population.Capacity).SequenceEqual(new[] { 8,48,16,88,40 }), "Estate hovel count leaked across kingdoms");
      var wooden = layouts.First(); Check(wooden.ModelOrigin == new Vector2(60,187) && wooden.Center == new Vector2(60.5f,187.5f), "Wooden campfire coordinate");
      var elevated = layouts[1]; Check(elevated.ModelOrigin == new Vector2(33,32) && elevated.RawHeight == 1706, "Rotated elevated campfire coordinate");
      var actors = S2MCampfirePopulationLoader.CreatePlacements(map);
      Check(actors.Count == 200 && actors.Select(a => a.Id).Distinct().Count() == 200, "Peasant population/IDs");
      Check(map.Actors.Count == 792, "Simulation mutated saved map actors");
      int auditedKeeps = 0;
      foreach (string path in Directory.EnumerateFiles(Install + "/maps", "*.s2m")
        .Concat(Directory.EnumerateFiles("C:/Users/garet/Documents/Stronghold 2/Maps", "*.s2m")))
      {
        var other = S2MFileLoader.Load(path);
        foreach (var keep in other.Keeps)
        {
          Check(keep.HasCampfirePosition, "Missing campfire anchor: " + path);
          Check(keep.CampfireRawX >= 0 && keep.CampfireRawZ >= 0
            && keep.CampfireRawX < other.MapSize * 1024 && keep.CampfireRawZ < other.MapSize * 1024,
            "Campfire anchor outside map: " + path);
          auditedKeeps++;
        }
      }
      File.WriteAllText(Output + "/map-audit.txt", $"Validated {auditedKeeps} keep campfire anchors across local maps.");
      var data = new S2MActorAssetLoader().Load(map, Install, S2MCampfirePopulationLoader.Overrides(),
        converterPath: Converter, cacheDirectory: Output + "/Cache", placements: actors);
      Check(data.Issues.Count == 0 && data.PlacedCount == actors.Count && data.Batches.Count == 3, "Peasant asset loading: " + string.Join(";", data.Issues));
      Check(data.Batches.All(b => b.Motion.Frames > 1 && b.Motion.Duration > 0), "Missing seated clips");
      var renderer = S2MActorRenderer.Render(data); renderer.Clock = clock;
      var tick = typeof(S2MActorRenderer).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
      var clockField = typeof(S2MActorRenderer).GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
      clock.IsPaused = true; clock.Advance(1); tick.Invoke(renderer, null);
      Check((float)clockField.GetValue(renderer) == 0, "Paused actor clock advanced");
      clock.IsPaused = false; clock.Advance(.5f); tick.Invoke(renderer, null);
      Check((float)clockField.GetValue(renderer) == .5f, "Actor resume failed");
      var keeps = new S2MKeepAssetLoader().Load(map, Install, Converter, Output + "/Models");
      var fire = S2MCampfireRenderer.Render(keeps, null, new S2MTerrainSettings()); fire.Clock = clock;
      var fireTick = typeof(S2MCampfireRenderer).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
      var fireTime = typeof(S2MCampfireRenderer).GetField("time", BindingFlags.NonPublic | BindingFlags.Instance);
      clock.IsPaused = true; clock.Advance(2); fireTick.Invoke(fire, null); Check((float)fireTime.GetValue(fire) == 0, "Paused fire advanced");
      clock.IsPaused = false; clock.Advance(.5f); fireTick.Invoke(fire, null); Check((float)fireTime.GetValue(fire) == .5f, "Fire resume failed");
      var shader = Resources.Load<Shader>("Campfire"); Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Campfire shader failed");
      var texture = TGALoader.LoadTGA(Install + "/meshes/misc/fire_orange.tga");
      File.WriteAllBytes(Output + "/fire-texture.png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
      var campBounds = Granny2ModelRenderer.GetLocalBounds(keeps.Campfire, 1 / S2MKeepRenderer.ModelUnitsPerCell);
      File.WriteAllText(Output + "/bounds.txt", campBounds.ToString("F3"));
      var keepRoot = S2MKeepRenderer.Render(keeps, settings: new S2MBuildingRenderSettings { ViewMode = BuildingViewMode.Closed });
      var camera = new GameObject("Campfire validation camera").AddComponent<Camera>();
      camera.transform.position = new Vector3(66, 7, 178); camera.transform.LookAt(new Vector3(60,0,188));
      camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f,.18f,.2f);
      var light = new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(45,-25,0);
      RenderSettings.ambientLight = Color.gray;
      // Hide duplicate fire bases used by pause tests.
      fire.gameObject.SetActive(false);
      var target = new RenderTexture(900,700,24); camera.targetTexture=target;
      tick.Invoke(renderer,null); camera.Render(); RenderTexture.active=target;
      var image=new Texture2D(900,700,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,900,700),0,0); image.Apply();
      File.WriteAllBytes(Output+"/campfire.png",image.EncodeToPNG()); RenderTexture.active=null; camera.targetTexture=null;
      UnityEngine.Object.DestroyImmediate(keepRoot); UnityEngine.Object.DestroyImmediate(renderer.gameObject);
      UnityEngine.Object.DestroyImmediate(fire.gameObject); UnityEngine.Object.DestroyImmediate(camera.gameObject);
      UnityEngine.Object.DestroyImmediate(light.gameObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
      File.WriteAllText(Output + "/pass.txt", "PASS: pause/resume clocks, 8/16/24 seat rings facing inward; five serialized campfire positions; estate capacities 8/48/16/88/40; 200 peasants in three seated animation batches; original map actors preserved; campfire model/texture/shader loaded and rendered.");
    }
    catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output+"/failure.txt",e.ToString()); throw; }
  }
  static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
}
