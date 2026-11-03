using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using UnityEngine;
using UnityEditor;
public static class AicBlueprintUnityValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2",Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/AicValidation/native-result.txt";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        try
        {
            AicBlueprintValidation.Run();var report=new List<string>();
            for(int chapter=1;chapter<=12;chapter++)
            {
                var map=S2MFileLoader.Load(Path.Combine(Install,"maps",$"war_chapter{chapter}.s2m"));
                int count=map.BuildingPlacements.Count,keeps=map.Keeps.Count;
                using var runtime=S2MAiRuntime.Create(map,new EstateInventory(),Install);
                var ai=runtime.World.Territories.Values.Where(t=>t.Kind==AiTerritoryKind.Kingdom && t.Controller==AiController.Autonomous).ToArray();
                var library=runtime.CastleBlueprints;
                Check(library.WarChapter==chapter && library.Kingdoms.Count==ai.Length,"Campaign/kingdom coverage "+chapter);
                Check(library.Loaded.Count==43 && library.Issues.Count==4,"Native library failures "+chapter);
                foreach(var territory in ai)
                {
                    var entry=library.Kingdoms[territory.Id];
                    Check(entry.Candidates.Count>0,"No candidate blueprints for Chapter "+chapter+" estate "+territory.Id+" lord "+entry.Lord+": "+entry.Issue);
                    Check(entry.Selected==null || ReferenceEquals(entry.Selected,library.Loaded[entry.Selected.Key]),"Per-estate duplicate cache");
                    Check(territory.BlueprintKey==entry.Selected?.Key,"World key mismatch");
                }
                if(chapter==6)Check(library.Kingdoms[2].Selected.PrimaryKeep?.KeepKind==AicKeepKind.Wooden,"Native Olaf campaign association incorrect");
                Check(map.BuildingPlacements.Count==count && map.Keeps.Count==keeps,"Blueprint loading changed map buildings");
                report.Add($"Chapter {chapter}: {ai.Length} AI kingdoms; selected="+string.Join(",",library.Kingdoms.Values.Where(k=>k.Selected!=null).Select(k=>k.EstateId+":"+k.Selected.Key)));
            }
            report.Add("PASS: all twelve installed campaign maps; AI kingdom coverage, immutable shared cache, correct Chapter 6 wooden keep data, human/village exclusion, no placement side effects.");
            File.WriteAllLines(Output,report);Debug.Log(string.Join("\n",report));EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Output,error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
