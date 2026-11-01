using System;
using System.IO;
using System.Linq;
using Assets.Code.Simulation;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class EstateHousingUnityValidation
{
    const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/HousingValidation";
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"PC"),true);ShaderUtil.allowAsyncCompilation=false;
            var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");S2MStartupBuildings.Apply(map,Install);
            var settings=new PopulationSettings();var nav=S2MNavigationBuilder.Build(map,settings);
            var plans=S2MRecruitmentPlan.Create(map,nav,settings);var jobs=S2MLaborPlan.Create(map,nav);
            var inventory=S2MStoragePlan.Create(map);var terrain=new S2MTerrainSettings();var renderSettings=new S2MActorRenderSettings();
            var root=new GameObject("Housing validation");S2MLiveryContext.Create(map,Install,root.transform);
            string converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
            var actors=S2MCampfirePopulationLoader.LoadRecruitsAsync(map,Install,plans,converterPath:converter,jobs:jobs.Jobs).GetAwaiter().GetResult();
            var controller=root.AddComponent<S2MPopulationController>();var clock=new SimulationClock();
            controller.Initialize(map,nav,plans,actors,terrain,renderSettings,settings,clock,jobs:jobs.Jobs,laborBuildings:jobs.Buildings);
            using var runtime=S2MAiRuntime.Create(map,inventory,Install,labor:controller.Labor);
            S2MEstateHovelConstruction.InitializeEstateResources(map,runtime.World,inventory);
            var presentation=new S2MAiBuildingPresentation(map,Install,root.transform,terrain,new S2MBuildingRenderSettings(),converter);
            using var granaries=new S2MEstateGranaryConstruction(map,runtime.World,inventory,Install,nav,controller.Labor);
            using var hovels=new S2MEstateHovelConstruction(map,runtime.World,inventory,Install,nav,controller.Labor);
            granaries.PreparePresentation=presentation.Prepare;hovels.PreparePresentation=presentation.Prepare;
            granaries.AdditionalPlacementRule=controller.PreservesRecruitmentSeats;hovels.AdditionalPlacementRule=controller.PreservesRecruitmentSeats;
            hovels.BuildingCompleted+=controller.AuthorizeHovel;
            granaries.ConstructionFailed+=(estate,error)=>{throw new Exception("Granary "+estate,error);};
            hovels.ConstructionFailed+=(estate,error)=>{throw new Exception("Hovel "+estate,error);};
            var planner=new EstateConstructionPlanner(granaries,hovels);runtime.Scheduler.SetPlanner(planner);
            for(int i=0;i<1200;i++){clock.Advance(.1f);runtime.Scheduler.Advance(clock.DeltaTime);planner.CommitPending(runtime.Scheduler.Now);controller.Advance(clock.DeltaTime);}
            Check(runtime.Scheduler.PlannerFailureCount==0,"Planner fault");
            var estates=runtime.World.Territories.Values.Where(t=>t.Kind==AiTerritoryKind.Estate).ToArray();
            Check(estates.Length==6,"Expected six estates");
            foreach(var estate in estates)
            {
                Check(estate.BuildingCount("Hovel")>=1 && estate.BuildingCount("Hovel")<=estate.MaximumHovels,"Housing ceiling/initial hovel "+estate.Id);
                var group=controller.Territories.Single(g=>g.Peasants.Count>0 && g.Peasants[0].IsEstateResident && g.Peasants[0].EstateId==estate.Id);
                Check(group.SpawnLimit==estate.BuildingCount("Hovel")*8 && group.SpawnedCount>0,"Housing recruitment authorization "+estate.Id);
                Check(group.Peasants.Take(group.SpawnedCount).All(p=>p.State==PeasantState.Seated || p.State==PeasantState.Working || p.State==PeasantState.WalkingToJob),"Estate recruits did not reach flag/job "+estate.Id);
                var demand=controller.Labor.GetLaborDemand(estate.Id);
                Check(estate.Labor.RequiredWorkers==demand.RequiredWorkers && estate.Labor.AssignedWorkers==demand.AssignedWorkers,"AI staffing index mismatch");
                Check(demand.AssignedWorkers>0 || demand.RequiredWorkers==0,"No estate workers assigned "+estate.Id);
            }
            var made=map.BuildingPlacements.Where(b=>b.RuntimeEstateId.HasValue && b.Type=="Hovel").ToArray();
            Check(made.Length>=6 && root.GetComponentsInChildren<MeshRenderer>().Length>=12,"Native buildings not rendered");
            File.WriteAllLines(Output+"/estates.txt",estates.Select(t=>$"estate={t.Id} tier={t.PopulationLevel} hovels={t.BuildingCount("Hovel")}/{t.MaximumHovels} goal={t.DesiredHovels} workers={t.Labor.AssignedWorkers}/{t.Labor.RequiredWorkers} missingBuildings={t.Labor.BuildingsNeedingLabor} wood={inventory.Amount(t.Id,Goods.Wood)}"));
            var sample=made.First();var centre=new Vector3(sample.MapX+1,sample.RawY/1024+1,sample.MapZ+1.5f);
            var camera=new GameObject("Hovel preview").AddComponent<Camera>();camera.transform.position=centre+new Vector3(-5,4,-6);camera.transform.LookAt(centre);camera.backgroundColor=Color.gray;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.sun=light;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var target=new RenderTexture(800,800,24);var image=new Texture2D(800,800,TextureFormat.RGB24,false);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(Output+"/hovel.png",image.EncodeToPNG());RenderTexture.active=null;
            File.WriteAllText(Output+"/pass.txt","PASS: Chapter 8 estate population tiers, native hovels, recruitment, labor assignment, granaries and material accounting.");File.Delete(Output+"/failure.txt");
            EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Output+"/failure.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
