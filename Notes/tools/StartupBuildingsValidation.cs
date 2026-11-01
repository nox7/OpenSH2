// Copy into the isolated Unity project's Assets/Editor and execute StartupBuildingsValidation.Run.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.MapLoading.Labor;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StartupBuildingsValidation
{
    const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
    const string Install = "C:/Steam/steamapps/common/Stronghold 2";
    const string Output = Repo + "/Temp/StartupValidation";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    static S2MFile Load(string name) => S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/" + name + ".s2m");
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            var first = Load("war_chapter1");
            Check(first.EstateMarkers.Count == 32, "Marker slot count");
            Check(first.EstateMarkers[1].Type == EstateType.Castle && first.EstateMarkers[2].Type == EstateType.None, "Active/inactive marker decoding");
            int originalKeeps = first.Keeps.Count;
            S2MStartupBuildings.Apply(first, Install);
            Check(first.Keeps.Count == originalKeeps && first.StartupBuildings.Count == 0, "Inactive marker or existing keep generated another keep");
            var map = Load("war_chapter8");
            int count = map.BuildingPlacements.Count;
            S2MStartupBuildings.Apply(map, Install);
            File.WriteAllLines(Output + "/placements.txt", map.StartupBuildings.Select(p => $"{p.Type} id={p.Id} estate={p.StartupEstateId} at={p.MapX},{p.MapZ} height={p.RawY} yaw={p.RotationDegrees}").Concat(map.StartupPlacementIssues));
            Check(map.StartupPlacementIssues.Count == 0, "Chapter 8 placement failures: " + string.Join("; ", map.StartupPlacementIssues));
            Check(map.Keeps.Count == 5 && map.StartupBuildings.Count == 12, "Expected six village flags and stockpiles");
            File.WriteAllLines(Output + "/grey-grid.txt", Enumerable.Range(150,28).Select(z => z + " " + new string(Enumerable.Range(202,30).Select(x => {
                if(map.WallLayer.GetCell(x,z).HasWall) return '#';
                var b = map.StartupBuildings.FirstOrDefault(p => x>=p.MapX && x<p.MapX+p.FootprintWidth && z>=p.MapZ && z<p.MapZ+p.FootprintHeight);
                if(b!=null) return b.Type=="StockPile"?'S':'F';
                return map.WalkingLayer.IsGroundSurface(x,z)?'.':'x';
            }).ToArray())));
            File.WriteAllLines(Output + "/grey-actors.txt", map.Actors.Where(a => a.RawX/1024>=205 && a.RawX/1024<=219 && a.RawZ/1024>=157 && a.RawZ/1024<=172).Select(a => $"{a.Type} {a.RawX/1024},{a.RawZ/1024} y={a.RawY} layer={a.NavigationLayer}"));
            Check(!map.StartupBuildings.Any(p => p.Type == "Granary"), "Startup added an AI-built granary");
            Check(!map.BuildingPlacements.Any(p => p.Type == "StockPile" && S2MCampfireLayout.EstateAtBuilding(map, p) == 3), "Kingdom missing stockpile was altered");
            var ids = map.StartupBuildings.Select(p => p.Id).ToArray();
            S2MStartupBuildings.Apply(map, Install);
            Check(map.BuildingPlacements.Count == count + 12 && ids.SequenceEqual(map.StartupBuildings.Select(p => p.Id)), "Startup is not idempotent");
            Check(map.BuildingPlacements.Select(p => p.Id).Distinct().Count() == map.BuildingPlacements.Count, "Duplicate building IDs");
            var inventory = S2MStoragePlan.Create(map);
            var navigation = S2MNavigationBuilder.Build(map, new PopulationSettings());
            foreach (var p in map.StartupBuildings)
            {
                Check(LaborBuildingCatalog.RequiresNoWorkers(p.Type), "Passive building requires workers");
                if (p.Type == "StockPile")
                {
                    Check(navigation.IsWalkable(new MapPoint(p.MapX + 3.5f, p.MapZ + 2.5f)), "Generated stockpile aisle blocked");
                    Check(!navigation.IsWalkable(new MapPoint(p.MapX + 1.5f, p.MapZ + 2.5f)), "Generated stockpile bank walkable");
                    Check(inventory.TryDeposit(p.Id, p.StartupEstateId.Value, Goods.Wood, 16), "Generated stockpile not registered in estate inventory");
                }
                else Check(!navigation.IsWalkable(new MapPoint(p.MapX + .5f, p.MapZ + .5f)), "Generated solid building walkable");
                if (p.Type == "VillageFlagPole")
                {
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dz != 0) Check(navigation.IsWalkable(new MapPoint(p.MapX+dx+.5f, p.MapZ+dz+.5f)), "Pole gathering apron blocked");
                    var stock = map.StartupBuildings.Single(b => b.StartupEstateId == p.StartupEstateId && b.Type == "StockPile");
                    Check(stock.MapX + 7 <= p.MapX - 1 || stock.MapX >= p.MapX + 2 || stock.MapZ + 6 <= p.MapZ - 1 || stock.MapZ - 1 >= p.MapZ + 2, "Stockpile placement apron overlaps pole gathering apron");
                }
            }
            // Controlled open ground proves creation independently of scenario-specific obstructions.
            var missing = new S2MFile { MapSize = 64 };
            Set(missing, "RadarEstateLayer", new EstateLayer { Width = 64, Height = 64, EstateIds = Enumerable.Repeat((byte)2, 64*64).ToArray() });
            Set(missing, "HeightLayer", new HeightLayer { Width = 64, Height = 64, Heights = new float[64*64] });
            var marker = new EstateMarkerFlag(); Set(marker, "EstateId", 2); Set(marker, "X", 32); Set(marker, "Y", 32); Set(marker, "Type", EstateType.Castle);
            Set(missing, "EstateMarkers", new[] { marker });
            int fires = S2MCampfireLayout.Create(missing).Count;
            S2MStartupBuildings.Apply(missing, Install);
            Check(missing.Keeps.Count == 1, "Missing active keep not generated: " + string.Join("; ", missing.StartupPlacementIssues));
            var keep = missing.Keeps.Single(k => k.IsStartupGenerated);
            Check(keep.MapX == 30 && keep.MapZ == 33, "Keep entrance offset is incorrect");
            Check(keep.Size == KeepSize.Medium && S2MCampfireLayout.Create(missing).Count == fires + 1, "Generated keep/campfire integration");
            var disabled = Load("war_chapter8");
            S2MStartupBuildings.Apply(disabled, Install, new StartupBuildingSettings { Enabled = false });
            Check(disabled.StartupBuildings.Count == 0 && !disabled.StartupBuildingsInitialized, "Disabled startup mutated map");
            Visual(map);
            File.WriteAllText(Output + "/pass.txt", "PASS: native 32-slot markers; inactive and existing keeps preserved; missing active keep and campfire; six Chapter 8 estates with 12 unique, idempotent buildings and no added granaries; inventory registration; walkable pole gathering aprons; stockpile placement separation and navigation aisles; 2x2 neutral and owned flag materials and native models.");
            File.Delete(Output + "/failure.txt");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(Output + "/failure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Visual(S2MFile map)
    {
        QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
        ShaderUtil.allowAsyncCompilation = false;
        var data = new S2MBuildingAssetLoader().Load(map, Install, Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
        var flagBatch = data.Batches.Single(b => b.Placements[0].Type == "VillageFlagPole");
        Check(flagBatch.Placements.Count == 6 && flagBatch.StaticParts.Count == 1, "Missing native pole/cloth assets");
        var root = new GameObject("Startup validation");
        var livery = S2MLiveryContext.Create(map, Install, root.transform);
        Check(livery.ResolveBuilding(map.StartupBuildings.First(p => p.Type == "VillageFlagPole" && p.StartupEstateId == 4)).Crest == 13, "Neutral flag has lord crest");
        Check(livery.ResolveBuilding(map.StartupBuildings.First(p => p.Type == "VillageFlagPole" && p.StartupEstateId == 8)).Crest == 10, "Grey estate flag has wrong crest");
        var textures = (List<Texture2D>)typeof(S2MLiveryContext).GetField("crestTextures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(livery);
        for (int z = 0; z < 128; z++) for (int x = 0; x < 128; x++)
            Check(textures[13].GetPixel(x,z).r == ((x/64+z/64)%2), "Neutral flag is not a 2x2 black/white checker");
        var buildings = S2MBuildingRenderer.Render(data, root.transform, new S2MTerrainSettings { GenerateMeshColliders = false });
        S2MWallRenderer.Render(map, Install, root.transform, new S2MTerrainSettings { GenerateMeshColliders = false });
        Check(livery.BuildingMaterialVariantCount > 0, "Flag crests not applied");
        var camera = new GameObject("Flag preview").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f,.25f,.3f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50,-30,0); RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
        var target = new RenderTexture(800,800,24); var image = new Texture2D(800,800,TextureFormat.RGB24,false);
        foreach (int estate in new[] {4,8})
        {
            var p = map.StartupBuildings.First(b => b.Type == "VillageFlagPole" && b.StartupEstateId == estate);
            var center = new Vector3(p.MapX + .5f, p.RawY / 1024 + 1.8f, p.MapZ + .5f);
            camera.transform.position = center + new Vector3(-4,2,-6); camera.transform.LookAt(center);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0,0,800,800),0,0); image.Apply();
            File.WriteAllBytes(Output + "/flag-estate-" + estate + ".png", image.EncodeToPNG());
        }
        camera.transform.position = new Vector3(214,24,137); camera.transform.LookAt(new Vector3(214,0,162));
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; image.ReadPixels(new Rect(0,0,800,800),0,0); image.Apply();
        File.WriteAllBytes(Output + "/grey-walled-estate.png", image.EncodeToPNG());
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(sun.gameObject); UnityEngine.Object.DestroyImmediate(root);
    }
}
