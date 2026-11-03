using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.AI;
using Assets.Code.Stronghold2.S2MReader.Enums;
using UnityEngine;
using UnityEditor;
public static class BlueprintKeepUnityValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2",Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    static S2MFile Fixture(bool blocked=false)
    {
        var map=new S2MFile{MapSize=64};
        Set(map,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});
        Set(map,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
        var marker=new EstateMarkerFlag();Set(marker,"EstateId",2);Set(marker,"Type",EstateType.Castle);Set(marker,"X",17);Set(marker,"Y",17);Set(map,"EstateMarkers",new[]{marker});
        if(blocked)
        {var building=new BuildingPlacement();Set(building,"Id",99);Set(building,"Type","StockPile");Set(building,"FootprintWidth",64);Set(building,"FootprintHeight",64);Set(building,"StartupEstateId",(int?)2);Set(map,"BuildingPlacements",new[]{building});}
        return map;
    }
    static void EdgeCases()
    {
        foreach(bool blocked in new[]{false,true})
        {
            var map=Fixture(blocked);var world=new AiWorld();world.Add(new AiTerritory(2,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("scenario-lord",2)));
            using var library=new S2MCastleBlueprints(map,world,Install,warChapter:6);
            S2MStartupBuildings.Apply(map,Install,new StartupBuildingSettings{BlueprintKeepSearchRadius=0},library);
            if(blocked)Check(map.Keeps.Count==0 && map.StartupPlacementIssues.Count>0,"Blocked keep forced through a building");
            else Check(map.Keeps.Single().Size==KeepSize.Wooden && map.Keeps.Single().MapX==17 && map.StartupPlacementIssues.Any(s=>s.Contains("general keep site")),"Ordinary-site fallback changed the selected keep type");
        }
        {
            var map=Fixture();var world=new AiWorld();world.Add(new AiTerritory(2,AiTerritoryKind.Kingdom,AiController.Human,new AiFaction("scenario-lord",1)));
            using var library=new S2MCastleBlueprints(map,world,Install,warChapter:6);S2MStartupBuildings.Apply(map,Install,blueprints:library);
            Check(map.Keeps.Single().Size==KeepSize.Medium && library.Layouts.Count==0,"Human keep incorrectly followed AI blueprint");
        }
        {
            var map=Fixture();var keep=new Keep();Set(keep,"Id",98);Set(keep,"Type","Keep3");Set(keep,"Size",KeepSize.Medium);Set(keep,"RawX",32*1024f);Set(keep,"RawZ",32*1024f);Set(keep,"FootprintWidth",8);Set(keep,"FootprintHeight",9);Set(map,"Keeps",new[]{keep});
            var world=new AiWorld();world.Add(new AiTerritory(2,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("scenario-lord",2)));
            using var library=new S2MCastleBlueprints(map,world,Install,warChapter:6);S2MStartupBuildings.Apply(map,Install,blueprints:library);
            Check(map.Keeps.Count==1 && ReferenceEquals(map.Keeps[0],keep) && keep.Size==KeepSize.Medium,"Selected blueprint replaced an existing keep");
        }
    }
    public static void Run()
    {
        var report=new List<string>();
        try
        {
            EdgeCases();
            for(int chapter=1;chapter<=12;chapter++)
            {
                var map=S2MFileLoader.Load(Path.Combine(Install,"maps",$"war_chapter{chapter}.s2m"));var original=map.Keeps.ToArray();
                using var blueprints=S2MAiRuntime.PrepareCastleBlueprints(map,Install);
                S2MStartupBuildings.Apply(map,Install,blueprints:blueprints);
                Check(original.All(k=>map.Keeps.Any(p=>ReferenceEquals(k,p))),"Replaced an authored keep chapter "+chapter);
                var added=map.Keeps.Where(k=>!original.Contains(k)).ToArray();
                foreach(var keep in added)
                {
                    int estate=S2MCampfireLayout.EstateAtBuilding(map,keep);
                    if(!blueprints.Kingdoms.TryGetValue(estate,out var selection) || selection.Selected==null)continue;
                    var item=selection.Selected.PrimaryKeep.Value;
                    Check(keep.Size==(KeepSize)((int)item.KeepKind.Value-20),"Wrong blueprint keep family "+chapter+":"+estate);
                    Check(blueprints.Layouts.TryGetValue(estate,out var layout),"Resolved anchor not retained");
                    var descriptor=S2MBuildingDescriptorReader.Read(Install,"meshes/buildings/"+(keep.Size==KeepSize.Wooden?"keep_wooden":keep.Size==KeepSize.Small?"keep_small":keep.Size==KeepSize.Large?"keep_large":"keep_medium")+".bdg");
                    var site=layout.Project(item,descriptor.Width,descriptor.Height);
                    Check(keep.MapX==site.ModelX && keep.MapZ==site.ModelZ && keep.RotationDegrees==site.RotationQuarterTurns*90,"Placement differs from retained blueprint transform");
                    report.Add($"Chapter {chapter}, estate {estate}: {keep.Type} at ({keep.MapX},{keep.MapZ}), yaw {keep.RotationDegrees}; {selection.Selected.Key}, layout origin ({layout.OriginX},{layout.OriginZ}).");
                }
                Check(map.StartupBuildings.All(b=>b is Keep || b.Type=="StockPile" || b.Type=="VillageFlagPole"),"Other blueprint construction activated");
                int count=map.BuildingPlacements.Count;S2MStartupBuildings.Apply(map,Install,blueprints:blueprints);Check(map.BuildingPlacements.Count==count,"Startup duplicated keeps");
                using var runtime=S2MAiRuntime.Create(map,new EstateInventory(),Install,preparedBlueprints:blueprints);
                Check(ReferenceEquals(runtime.CastleBlueprints,blueprints),"Startup reloaded the blueprint library");
                foreach(var pair in map.StartupCastleLayouts)Check(ReferenceEquals(runtime.CastleBlueprints.Layouts[pair.Key].Blueprint,runtime.CastleBlueprints.Kingdoms[pair.Key].Selected),"Anchor/selected design objects duplicated");
                if(chapter==6)
                {
                    var olaf=map.Keeps.Single(k=>S2MCampfireLayout.EstateAtBuilding(map,k)==2);
                    Check(olaf.Type=="Keep1" && olaf.Size==KeepSize.Wooden && olaf.FootprintWidth==4 && olaf.FootprintHeight==9,"Olaf remains a medium keep");
                    var data=new S2MKeepAssetLoader().Load(map,Install,Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
                    Check(data.Batches.Single(b=>b.Size==KeepSize.Wooden).Keeps.Contains(olaf),"Olaf not using wooden GR2 asset batch");
                }
                if(map.StartupPlacementIssues.Count>0)report.Add("Chapter "+chapter+" startup notices: "+string.Join("; ",map.StartupPlacementIssues));
            }
            report.Add("PASS: all twelve native campaigns; correct selected keep family/transform, authored keep preservation, one shared library, idempotent startup, actual wooden assets for Olaf, no additional castle construction.");
            File.WriteAllLines(Repo+"/Temp/AicValidation/keep-result.txt",report);Debug.Log(string.Join("\n",report));EditorApplication.Exit(0);
        }
        catch(Exception error){report.Add(error.ToString());File.WriteAllLines(Repo+"/Temp/AicValidation/keep-result.txt",report);Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
