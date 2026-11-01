using System;
using System.IO;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class EstateGranaryUnityValidation
{
    const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    const string Output=Repo+"/Temp/GranaryValidation";
    static void Check(bool b,string why){if(!b)throw new Exception(why);}
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"PC"),true);ShaderUtil.allowAsyncCompilation=false;
            var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
            S2MStartupBuildings.Apply(map,Install);
            int original=map.BuildingPlacements.Count(b=>b.Type=="Granary");
            var inventory=S2MStoragePlan.Create(map);
            var navigation=S2MNavigationBuilder.Build(map,new PopulationSettings());
            var root=new GameObject("Granary construction validation");
            S2MLiveryContext.Create(map,Install,root.transform);
            using var runtime=S2MAiRuntime.Create(map,inventory,Install);
            var terrain=new S2MTerrainSettings{GenerateMeshColliders=true};
            var presentation=new S2MAiBuildingPresentation(map,Install,root.transform,terrain,new S2MBuildingRenderSettings(),Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
            using var build=new S2MEstateGranaryConstruction(map,runtime.World,inventory,Install,navigation);
            build.PreparePresentation=presentation.Prepare;
            build.ConstructionFailed+=(estate,error)=>{throw new Exception("Estate "+estate+" presentation failed",error);};
            runtime.Scheduler.SetPlanner(build);
            for(int i=0;i<1200 && build.CompletedCount<6;i++){runtime.Scheduler.Advance(.1);build.CommitPending(runtime.Scheduler.Now);}
            Check(runtime.Scheduler.PlannerFailureCount==0,"Planner exception");
            Check(build.CompletedCount==6,"Expected six estate granaries; got "+build.CompletedCount);
            Check(map.BuildingPlacements.Count(b=>b.Type=="Granary")==original+6,"Existing granary changed/duplicate construction");
            var made=map.BuildingPlacements.Where(b=>b.RuntimeEstateId.HasValue).ToArray();
            Check(made.All(b=>inventory.TryGetBuilding(b.Id,out var store) && store.Kind==StorageKind.Granary),"Granary inventory registration");
            Check(root.GetComponentsInChildren<MeshRenderer>().Length>=6 && root.GetComponentsInChildren<MeshCollider>().Length>=6,"Native rendering or colliders missing");
            foreach(var p in made)
            {
                Check(!navigation.IsWalkable(new Assets.Code.Simulation.Navigation.MapPoint(p.MapX+.5f,p.MapZ+.5f)),"Live footprint not blocked");
                Check(inventory.TryDeposit(p.Id,p.RuntimeEstateId.Value,Goods.Apples,1),"New store cannot accept food");
            }
            File.WriteAllLines(Output+"/placements.txt",made.Select(p=>$"estate={p.RuntimeEstateId} id={p.Id} position={p.MapX},{p.MapZ} height={p.RawY}"));
            var camera=new GameObject("Granary preview").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.2f,.25f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var target=new RenderTexture(800,800,24);var image=new Texture2D(800,800,TextureFormat.RGB24,false);
            var sample=made.First(p=>p.RuntimeEstateId==4);var centre=new Vector3(sample.MapX+1.5f,sample.RawY/1024+1.3f,sample.MapZ+1.5f);
            camera.transform.position=centre+new Vector3(-6,4,-7);camera.transform.LookAt(centre);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();
            File.WriteAllBytes(Output+"/granary.png",image.EncodeToPNG());
            RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);
            File.WriteAllText(Output+"/pass.txt","PASS: six live Chapter 8 estate granaries, native models/colliders, existing granaries preserved, food inventory and dynamic navigation.");
            File.Delete(Output+"/failure.txt");
            build.Dispose();UnityEngine.Object.DestroyImmediate(root);EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Output+"/failure.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
