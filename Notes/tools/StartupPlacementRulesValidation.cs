// Isolated Unity editor entry point: StartupPlacementRulesValidation.Run.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;
using UnityEditor;
using UnityEngine;

public static class StartupPlacementRulesValidation
{
    const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    const string Maps="C:/Users/garet/Documents/Stronghold 2/Maps/";
    const string Saves="C:/Users/garet/Documents/Stronghold 2/Saves/";
    const string Output=Repo+"/Temp/StartupValidation";
    static void Set(object o,string p,object v)=>o.GetType().GetProperty(p).SetValue(o,v);
    static void Check(bool v,string why){if(!v)throw new Exception(why);}
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            var rows=new List<string>();
            string[] suffixes={"sim","sim-2","sim-3","sim-4","sim-final"};
            int[] xs={231,231,231,237,241}, zs={100,106,99,109,96}, sx={230,230,230,230,230}, sz={103,99,103,102,103}, counts={1,2,3,5,6};
            for(int i=0;i<suffixes.Length;i++)
            {
                var saved=S2MFileLoader.Load(Saves+"estate-spawn-test-"+suffixes[i]+".sh2");
                var soldiers=saved.Actors.Where(a=>a.IsSoldier && a.OnMap).ToArray();
                Check(soldiers.Length==counts[i],"Soldier inheritance/count: "+suffixes[i]+" got "+soldiers.Length);
                Check(saved.Actors.Where(a=>a.Type=="Lord").All(a=>!a.IsSoldier),"Lord incorrectly classified as a soldier");
                var map=S2MFileLoader.Load(Maps+"estate-spawn-test.s2m"); Set(map,"Actors",soldiers);
                S2MStartupBuildings.Apply(map,Install);
                Check(map.StartupPlacementIssues.Count==0,"Placement failures: "+string.Join("; ",map.StartupPlacementIssues));
                Verify(map,"VillageFlagPole",8,xs[i],zs[i]); Verify(map,"StockPile",8,sx[i],sz[i]);
                Check(!map.StartupBuildings.Any(p=>p.Type=="Granary"),"Granary startup regression");
                rows.Add(suffixes[i]+": pole="+xs[i]+","+zs[i]+" stock="+sx[i]+","+sz[i]);
            }
            var chapter=S2MFileLoader.Load(Maps+"war_chapter8.s2m");
            S2MStartupBuildings.Apply(chapter,Install);
            Verify(chapter,"VillageFlagPole",8,220,158); Verify(chapter,"StockPile",8,209,164);
            int count=chapter.BuildingPlacements.Count;
            S2MStartupBuildings.Apply(chapter,Install); Check(chapter.BuildingPlacements.Count==count,"Repeated startup duplicated buildings");
            var candidates=(List<(int X,int Z)>)typeof(S2MStartupBuildings).GetMethod("Candidates",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1,false});
            Check(candidates.Take(5).SequenceEqual(new[]{(0,0),(0,-1),(1,0),(0,1),(-1,0)}),"N/E/S/W cardinal tie order");
            Check(candidates.Skip(5).SequenceEqual(new[]{(1,-1),(1,1),(-1,1),(-1,-1)}),"Clockwise diagonal tie order");
            foreach(int owner in new[]{1,10,30})
            {
                var actor=Soldier(36.5f,33.5f); Set(actor,"PendingPlayer",owner); Set(actor,"EstateId",owner);
                var m=Fixture(actor); S2MStartupBuildings.Apply(m,Install);
                var pole=m.StartupBuildings.Single(p=>p.Type=="VillageFlagPole");
                Check(pole.MapX!=32 || pole.MapZ!=32,"Allegiance incorrectly exempts troop");
            }
            var boundary=Fixture(Soldier(40,34)); S2MStartupBuildings.Apply(boundary,Install); Verify(boundary,"VillageFlagPole",2,32,32);
            var inside=Fixture(Soldier(39.99f,34)); S2MStartupBuildings.Apply(inside,Install);
            Check(inside.StartupBuildings.First(p=>p.Type=="VillageFlagPole").MapX!=32 || inside.StartupBuildings.First(p=>p.Type=="VillageFlagPole").MapZ!=32,"Inside-radius boundary accepted");
            var inactive=Soldier(34,34); Set(inactive,"OnMap",false);
            var offMap=Fixture(inactive); S2MStartupBuildings.Apply(offMap,Install); Verify(offMap,"VillageFlagPole",2,32,32);
            var dense=Fixture(Enumerable.Range(0,8000).Select(i=>Soldier(2+i%10*.1f,2+i%10*.1f)).ToArray());
            var timer=System.Diagnostics.Stopwatch.StartNew(); S2MStartupBuildings.Apply(dense,Install); timer.Stop();
            Verify(dense,"VillageFlagPole",2,32,32);
            File.WriteAllLines(Output+"/rules-pass.txt",rows.Concat(new[]{"Chapter 8 native coordinates; idempotence; Soldier classification; wall layers; allegiance independence; strict radius boundary; inactive troops; N/E/S/W ties.","8000-troop startup: "+timer.ElapsedMilliseconds+" ms"}));
            File.Delete(Output+"/rules-failure.txt"); EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"/rules-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Verify(S2MFile m,string type,int estate,int x,int z)
    {
        var p=m.StartupBuildings.Single(b=>b.Type==type && b.StartupEstateId==estate);
        Check(p.MapX==x && p.MapZ==z,$"{type} estate {estate}: expected {x},{z}, got {p.MapX},{p.MapZ}");
    }
    static ActorPlacement Soldier(float x,float z)
    {
        var a=new ActorPlacement(); Set(a,"Type","Archer");Set(a,"IsSoldier",true);Set(a,"OnMap",true);
        Set(a,"RawX",x*1024);Set(a,"RawZ",z*1024);Set(a,"RawY",2048f);Set(a,"NavigationLayer",(byte)1);return a;
    }
    static S2MFile Fixture(params ActorPlacement[] actors)
    {
        var m=new S2MFile{MapSize=64};
        Set(m,"RadarEstateLayer",new EstateLayer{Width=64,Height=64,EstateIds=Enumerable.Repeat((byte)2,4096).ToArray()});
        Set(m,"HeightLayer",new HeightLayer{Width=64,Height=64,Heights=new float[4096]});
        var marker=new EstateMarkerFlag(); Set(marker,"EstateId",2);Set(marker,"X",32);Set(marker,"Y",32);Set(marker,"Type",EstateType.Estate);
        Set(m,"EstateMarkers",new[]{marker});Set(m,"Actors",actors);return m;
    }
}
