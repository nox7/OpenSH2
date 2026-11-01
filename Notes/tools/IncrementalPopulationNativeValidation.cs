using System;
using System.IO;
using System.Linq;
using Assets.Code.Simulation;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class IncrementalPopulationNativeValidation
{
    const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/IncrementalPopulationValidation";
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    static BuildingPlacement Pit(int x,int z,int rotation=0)
    {
        var p=new BuildingPlacement();Set(p,"Id",123);Set(p,"Type","SawPit");Set(p,"RawX",x*1024f);Set(p,"RawZ",z*1024f);Set(p,"RotationDegrees",(float)rotation);
        Set(p,"FootprintWidth",rotation%180==0?1:4);Set(p,"FootprintHeight",rotation%180==0?4:1);return p;
    }
    static void TerrainChecks()
    {
        foreach(bool flipX in new[]{false,true})foreach(bool flipZ in new[]{false,true})
        {
            var map=new Assets.Code.Stronghold2.S2MReader.Resources.S2MFile{MapSize=8};
            Set(map,"HeightLayer",new Assets.Code.Stronghold2.S2MReader.Resources.HeightLayer{Width=8,Height=8,Heights=new float[64]});
            var water=new WaterLayer();Set(water,"Width",8);Set(water,"Height",8);Set(water,"SurfaceMaterialIds",new byte[64]);
            var land=new Landscape();Set(land,"Width",8);Set(land,"Height",8);Set(land,"Heights",new float[64]);Set(land,"Water",water);Set(map,"Landscape",land);
            var baseline=new bool[64];baseline[0]=true;
            var settings=new S2MTerrainSettings{CellsPerChunk=4,TerrainCutoutCells=baseline,FlipX=flipX,FlipZ=flipZ};
            var root=S2MTerrainRenderer.Render(map,settings:settings);var edit=root.GetComponent<S2MTerrainEdits>();
            var terrainMeshes=root.GetComponentsInChildren<MeshFilter>().ToArray();
            long Triangles()=>terrainMeshes.Sum(f=>(long)f.sharedMesh.GetIndexCount(0));
            int vertices=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount);
            long original=Triangles();Check(original==63*6,"Unexpected base geometry");
            var p=Pit(3,3);int before=edit.UpdatedChunks;
            using(var opening=edit.OpenSawpit(p))
            {
                Check(Triangles()==original-12 && edit.UpdatedChunks-before==2,"Opening did not update exactly two crossed chunks");
                Check(root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount)==vertices,"Vertices regenerated for cutout");
                Check(edit.IsCutout(3,3) && edit.IsCutout(3,4) && !baseline[3*8+3],"Wrong mutable cutout overlay");
                Vector3 Point(float x,float z)=>new Vector3(flipX?8-x:x,10,flipZ?8-z:z);
                bool Hits(Vector3 point)=>root.GetComponentsInChildren<MeshCollider>().Any(c=>c.Raycast(new Ray(point,Vector3.down),out _,20));
                Physics.SyncTransforms();Check(!Hits(Point(3.5f,3.5f)) && Hits(Point(4.5f,3.5f)),"Invisible terrain collider still caps the pit");
                using(var overlap=edit.OpenSawpit(p))Check(Triangles()==original-12,"Overlapping lease removed extra cells");
                Check(Triangles()==original-12,"Overlapping lease restored occupied pit");
            }
            Check(Triangles()==original && edit.IsCutout(0,0),"Terrain rollback lost geometry or original saved hole");
            foreach(int rotation in new[]{0,90,180,270})using(var opening=edit.OpenSawpit(Pit(4,4,rotation)))Check(Triangles()==original-12,"Rotated opening includes clearance");
            // Exercise the same presentation activation/disposal path used by the construction transaction.
            var presentation=new S2MAiBuildingPresentation(map,Install,root.transform,settings,new S2MBuildingRenderSettings(),Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
            using(var view=presentation.Prepare(Pit(3,3))){view.Show();view.Show();Check(Triangles()<original,"Presentation did not open terrain");}
            Check(Triangles()==original,"Presentation disposal did not restore terrain");
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"PC"),true);ShaderUtil.allowAsyncCompilation=false;
            TerrainChecks();
            var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");S2MStartupBuildings.Apply(map,Install);
            var settings=new PopulationSettings();var nav=S2MNavigationBuilder.Build(map,settings);

            var woodSettings=new WoodcuttingSettings{UnitScale=1/204.8f/.01f};
            var trees=new List<FoliageResource>();
            foreach(var flag in map.BuildingPlacements.Where(b=>b.Type=="VillageFlagPole"))
            {
                var desired=new MapPoint(flag.MapX+10,flag.MapZ+10);
                if(nav.TryNearest(desired,20,out var tree))trees.Add(new FoliageResource(100000+trees.Count,tree,.15f));
            }
            var resources=new FoliageResources(trees,nav,woodSettings);nav.PrepareConnectivity();nav.EnablePathRequests(new Assets.Code.Simulation.Navigation.PathfindingSettings{WorkUnitsPerFrame=2048,MillisecondsPerFrame=1});nav.PreparePathRequests();
            var plans=S2MRecruitmentPlan.Create(map,nav,settings);var jobs=S2MLaborPlan.Create(map,nav);
            var inventory=S2MStoragePlan.Create(map);var buildingData=new S2MBuildingAssetLoader().Load(map,Install,Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
            var terrain=new S2MTerrainSettings{TerrainCutoutCells=buildingData.TerrainCutoutCells,PrimaryHeightCells=buildingData.PrimaryHeightCells,SurfaceOverrides=buildingData.SurfaceOverrides};var renderSettings=new S2MActorRenderSettings();
            var root=new GameObject("Sawpit terrain validation");S2MTerrainRenderer.Render(map,root.transform,terrain);S2MLiveryContext.Create(map,Install,root.transform);
            string converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
            var actors=S2MCampfirePopulationLoader.LoadRecruitsAsync(map,Install,plans,converterPath:converter,jobs:jobs.Jobs,includeRuntimeWoodcutters:true).GetAwaiter().GetResult();
            var controller=root.AddComponent<S2MPopulationController>();var clock=new SimulationClock();
            controller.Initialize(map,nav,plans,actors,terrain,renderSettings,settings,clock,jobs:jobs.Jobs,laborBuildings:jobs.Buildings);
            using var runtime=S2MAiRuntime.Create(map,inventory,Install,labor:controller.Labor);
            S2MStockpileRenderer.Create(map,inventory,S2MStockpileAssets.Load(Install,converter),root.transform,terrain);
            S2MEstateHovelConstruction.InitializeEstateResources(map,runtime.World,inventory);
            var presentation=new S2MAiBuildingPresentation(map,Install,root.transform,terrain,new S2MBuildingRenderSettings(),converter);
            var renderer=root.GetComponentInChildren<S2MActorRenderer>();
            Check(renderer.HasActor(S2MCampfirePopulationLoader.RuntimeCartTemplateId),"Runtime cart template missing");
            int actorTransforms=renderer.GetComponentsInChildren<Transform>(true).Length;
            using var granaries=new S2MEstateGranaryConstruction(map,runtime.World,inventory,Install,nav,controller.Labor);
            using var hovels=new S2MEstateHovelConstruction(map,runtime.World,inventory,Install,nav,controller.Labor);
            granaries.PreparePresentation=presentation.BeginPreparation;hovels.PreparePresentation=presentation.BeginPreparation;
            granaries.AdditionalPlacementRule=controller.PreservesRecruitmentSeats;hovels.AdditionalPlacementRule=controller.PreservesRecruitmentSeats;
            hovels.BuildingCompleted+=controller.AuthorizeHovel;
            granaries.ConstructionFailed+=(estate,error)=>{throw new Exception("Granary "+estate,error);};
            hovels.ConstructionFailed+=(estate,error)=>{throw new Exception("Hovel "+estate,error);};
            using var sawpits=new S2MEstateSawpitConstruction(map,runtime.World,inventory,Install,nav,controller.Labor,foliage:resources);
            sawpits.PreparePresentation=presentation.BeginPreparation;sawpits.AdditionalPlacementRule=controller.PreservesRecruitmentSeats;
            sawpits.PrepareLabor=p=>controller.PrepareSawpit(p,resources,woodSettings,inventory);
            sawpits.ConstructionFailed+=(estate,error)=>Debug.LogWarning("Sawpit "+estate+": "+error.Message);
            var planner=new EstateConstructionPlanner(granaries,hovels,sawpits);runtime.Scheduler.SetPlanner(planner);
            var stepTimes=new List<double>();long maxCells=0;
            for(int i=0;i<12000;i++)
            {
                clock.Advance(.1f);runtime.Scheduler.Advance(clock.DeltaTime);
                bool pending=granaries.PendingCount+hovels.PendingCount+sawpits.PendingCount>0;
                long cells=nav.ConnectivityCellsRebuilt;var sw=System.Diagnostics.Stopwatch.StartNew();
                planner.CommitPending(runtime.Scheduler.Now);sw.Stop();if(pending)stepTimes.Add(sw.Elapsed.TotalMilliseconds);
                maxCells=Math.Max(maxCells,nav.ConnectivityCellsRebuilt-cells);
                Check(nav.ConnectivityCellsRebuilt-cells<=16384,"Building rebuilt nonlocal navigation");
                controller.Advance(clock.DeltaTime);
            }
            stepTimes.Sort();
            File.WriteAllText(Output+"/timings.txt",$"Construction frame actions={stepTimes.Count}; p50={stepTimes[stepTimes.Count/2]:F3}ms p95={stepTimes[(int)(stepTimes.Count*.95)]:F3}ms p99={stepTimes[(int)(stepTimes.Count*.99)]:F3}ms max={stepTimes.Last():F3}ms; maximum navigation cells rebuilt per action={maxCells}.");
            Check(runtime.Scheduler.PlannerFailureCount==0,"Planner fault");
            var houses=root.GetComponentsInChildren<S2MBuildingPresence>(true).Where(v=>map.BuildingPlacements.Any(b=>b.Id==v.BuildingId && b.Type=="Hovel" && b.RuntimeEstateId.HasValue)).ToArray();
            Check(houses.Length==6,"Expected six rendered runtime hovels");
            var sharedHouseMeshes=houses[0].GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            Check(houses.All(h=>h.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).SequenceEqual(sharedHouseMeshes)),"Runtime buildings recreated mesh resources instead of sharing them");
            var estates=runtime.World.Territories.Values.Where(t=>t.Kind==AiTerritoryKind.Estate).ToArray();
            Check(estates.Length==6,"Expected six estates");
            foreach(var estate in estates)
            {
                Check(estate.BuildingCount("SawPit")==estate.SawpitTarget,"Missing/duplicate estate sawpit "+estate.Id);
                Check(estate.BuildingCount("Hovel")>=1 && estate.BuildingCount("Hovel")<=estate.MaximumHovels,"Housing ceiling/initial hovel "+estate.Id);
                var group=controller.Territories.Single(g=>g.Peasants.Count>0 && g.Peasants[0].IsEstateResident && g.Peasants[0].EstateId==estate.Id);
                Check(group.SpawnLimit==estate.BuildingCount("Hovel")*8 && group.SpawnedCount>0,"Housing recruitment authorization "+estate.Id);
                Check(group.Peasants.Take(group.SpawnedCount).All(p=>p.State==PeasantState.Seated || p.State==PeasantState.Working || p.State==PeasantState.WalkingToJob),"Estate recruits did not reach flag/job "+estate.Id);
                var demand=controller.Labor.GetLaborDemand(estate.Id);
                Check(estate.Labor.RequiredWorkers==demand.RequiredWorkers && estate.Labor.AssignedWorkers==demand.AssignedWorkers,"AI staffing index mismatch");
                Check(demand.AssignedWorkers==1 && demand.RequiredWorkers==1,"No runtime estate woodcutter assigned "+estate.Id);
                var wood=controller.Labor.Jobs.OfType<WoodcutterJob>().Single(j=>j.EstateId==estate.Id);
                Check(wood.HarvestEnabled && wood.CompletedDeliveries>0,"Runtime woodcutter failed harvesting/delivery "+estate.Id+" phase="+wood.Phase);
                Check(inventory.Amount(estate.Id,Goods.Wood)==23+wood.DeliveredPlanks,"Production cost/delivery total "+estate.Id);
            }
            var edits=root.GetComponentInChildren<S2MTerrainEdits>();
            foreach(var pit in map.BuildingPlacements.Where(b=>b.Type=="SawPit" && b.RuntimeEstateId.HasValue))
            {
                foreach(int cell in S2MTerrainEdits.SawpitCells(pit,map.MapSize,map.MapSize))
                    Check(edits.IsCutout(cell%map.MapSize,cell/map.MapSize),"Runtime sawpit has no terrain opening");
                Check(trees.Any(t=>MapPoint.Distance(t.Position,new MapPoint(pit.MapX+.5f,pit.MapZ+1))<4),"Native placement did not approach a tree");
            }
            Check(sawpits.CompletedCount==6 && renderer.ClonedActorCount==12,"Runtime equipment registration");
            Check(renderer.GetComponentsInChildren<Transform>(true).Length==actorTransforms,"Per-job actor GameObjects allocated");
            var clones=new List<int>();int originalActors=renderer.ActorCount;
            for(int i=0;i<700;i++){int id=renderer.AllocateEquipmentPair();renderer.CloneActor(S2MCampfirePopulationLoader.RuntimeCartTemplateId,id,4);clones.Add(id);}
            int grown=renderer.DrawBatchCount;
            foreach(int id in clones)Check(renderer.RemoveClonedActor(id),"Clone removal");
            foreach(int id in clones)renderer.CloneActor(S2MCampfirePopulationLoader.RuntimeCartTemplateId,id,4);
            Check(renderer.DrawBatchCount==grown,"Freed draw slots not reused");foreach(int id in clones)renderer.RemoveClonedActor(id);
            Check(renderer.ActorCount==originalActors && renderer.GetComponentsInChildren<Transform>(true).Length==actorTransforms,"Clone scale test leaked actors/objects");
            var made=map.BuildingPlacements.Where(b=>b.RuntimeEstateId.HasValue && b.Type=="SawPit").ToArray();
            Check(made.Length>=6 && root.GetComponentsInChildren<MeshRenderer>().Length>=12,"Native buildings not rendered");
            File.WriteAllLines(Output+"/estates.txt",estates.Select(t=>$"estate={t.Id} tier={t.PopulationLevel} hovels={t.BuildingCount("Hovel")}/{t.MaximumHovels} sawpits={t.BuildingCount("SawPit")}/{t.DesiredSawpits} workers={t.Labor.AssignedWorkers}/{t.Labor.RequiredWorkers} missingBuildings={t.Labor.BuildingsNeedingLabor} wood={inventory.Amount(t.Id,Goods.Wood)}"));
            var sample=made.First();var centre=new Vector3(sample.MapX+1,sample.RawY/1024+1,sample.MapZ+1.5f);
            var camera=new GameObject("Hovel preview").AddComponent<Camera>();camera.transform.position=centre+new Vector3(-5,4,-6);camera.transform.LookAt(centre);camera.backgroundColor=Color.gray;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.sun=light;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var target=new RenderTexture(800,800,24);var image=new Texture2D(800,800,TextureFormat.RGB24,false);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(Output+"/sawpit.png",image.EncodeToPNG());RenderTexture.active=null;
            File.WriteAllText(Output+"/pass.txt","PASS: staged native building preparation and shared resources; localized navigation per commit; six Chapter 8 estate production cycles; terrain openings/rollback and 700 pooled GPU actors.");File.Delete(Output+"/failure.txt");
            EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Output+"/failure.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
