using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using Assets.Code.Simulation.Population;
public static class KeepForecourtUnityValidation
{
    const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/ForecourtValidation";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(float a,float b,string message)=>Check(Math.Abs(a-b)<.001f,message+": "+a+" != "+b);
    public static void Run()
    {
        var report=new List<string>();
        try
        {
            KeepForecourtValidation.Run();int saved=0,generated=0;
            for(int chapter=1;chapter<=12;chapter++)
            {
                var map=S2MFileLoader.Load(Path.Combine(Install,"maps",$"war_chapter{chapter}.s2m"));
                foreach(var camp in S2MCampfireLayout.Create(map))
                {
                    Check(camp.Keep.HasCampfirePosition,"Native keep lacks saved anchor");saved++;
                    Near(camp.Center.x,camp.Keep.CampfireRawX/1024,"Saved fire X shifted");Near(camp.Center.y,camp.Keep.CampfireRawZ/1024,"Saved fire Z shifted");
                    var calculated=S2MKeepForecourt.CalculatedCenter(camp.Keep);
                    Near(camp.Center.x,calculated.X,"Native-derived centre X mismatch");Near(camp.Center.y,calculated.Z,"Native-derived centre Z mismatch");
                }
                using var blueprints=S2MAiRuntime.PrepareCastleBlueprints(map,Install);S2MStartupBuildings.Apply(map,Install,blueprints:blueprints);
                foreach(var camp in S2MCampfireLayout.Create(map).Where(c=>c.Keep.IsStartupGenerated))
                {generated++;report.Add($"Chapter {chapter}, estate {camp.Population.EstateId}: {camp.Keep.Type} origin ({camp.Keep.MapX},{camp.Keep.MapZ}) fire centre ({camp.Center.x},{camp.Center.y}) mesh pivot ({camp.ModelOrigin.x},{camp.ModelOrigin.y}).");}
                if(chapter==6 || chapter==8)
                {
                    var nav=S2MNavigationBuilder.Build(map,new PopulationSettings());nav.PrepareConnectivity();
                    var plans=S2MRecruitmentPlan.Create(map,nav,new PopulationSettings());Check(plans.Count>=map.Keeps.Count,"Keep seating missing");
                    Check(plans.SelectMany(g=>g).All(p=>nav.IsWalkable(p.Seat)),"Peasant seat blocked after centre correction");
                }
                if(chapter==6)RenderChecks(map);
            }
            report.Add($"PASS: {saved} authored keep centres match native entrance calculation; {generated} generated keeps; Chapter 6/8 recruitment; real fire geometry/flame pivots under all axis flips; all family/rotation/reservation tests.");
            File.WriteAllLines(Output+"/native-result.txt",report);Debug.Log(string.Join("\n",report));EditorApplication.Exit(0);
        }
        catch(Exception error){report.Add(error.ToString());File.WriteAllLines(Output+"/native-result.txt",report);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void RenderChecks(S2MFile map)
    {
        var data=new S2MKeepAssetLoader().Load(map,Install,Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",Output+"/Models");
        var camps=S2MCampfireLayout.Create(map);
        foreach(bool flipX in new[]{false,true})foreach(bool flipZ in new[]{false,true})
        {
            var parent=new GameObject("Forecourt render check");parent.transform.localScale=Vector3.one*2;
            var settings=new S2MTerrainSettings{FlipX=flipX,FlipZ=flipZ};
            var view=S2MCampfireRenderer.Render(data,parent.transform,settings);
            var flames=view.GetComponentsInChildren<Transform>().Where(t=>t.name=="Animated Fire").ToArray();Check(flames.Length==camps.Count,"Flame count differs from keep count");
            for(int i=0;i<camps.Count;i++)
            {
                var c=camps[i];float x=(flipX?map.MapSize-c.Center.x:c.Center.x)*settings.HorizontalCellSize,z=(flipZ?map.MapSize-c.Center.y:c.Center.y)*settings.HorizontalCellSize;
                var expected=parent.transform.TransformPoint(new Vector3(x,c.RawHeight*settings.HeightUnitScale+.28f*settings.HorizontalCellSize,z));
                Near(flames[i].position.x,expected.x,"Rendered flame X");Near(flames[i].position.z,expected.z,"Rendered flame Z");Near(flames[i].position.y,expected.y,"Rendered flame height");
                var mesh=view.GetComponentsInChildren<Transform>().Single(t=>t.name=="Campfire "+c.Keep.Id);
                var centre=mesh.TransformPoint(new Vector3(.5f*settings.HorizontalCellSize,0,.5f*settings.HorizontalCellSize));
                Near(centre.x,expected.x,"Wood/stone mesh centre differs from flame X");Near(centre.z,expected.z,"Wood/stone mesh centre differs from flame Z");
            }
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }
}
