// Run in an isolated Unity project with OpenSH2's Code:
// Unity -batchmode -executeMethod HeightLayerHeaderValidation.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;

public static class HeightLayerHeaderValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Output = Repo + "/Temp/Chapter5Probe";
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      HeightLayer shortHeader=Decode(Fixture(12)), longHeader=Decode(Fixture(76));
      Check(shortHeader.Heights.SequenceEqual(longHeader.Heights),"Header length changed primary heights");
      Check(shortHeader.CornerHeightPlanes.Length==3 && longHeader.CornerHeightPlanes.Length==3,"Missing corner planes");
      for(int p=0;p<3;p++) Check(shortHeader.CornerHeightPlanes[p].SequenceEqual(longHeader.CornerHeightPlanes[p]),"Header length changed corner heights");
      foreach(int length in new[]{-1,int.MaxValue})
      {
        byte[] broken=Fixture(12); Array.Copy(BitConverter.GetBytes(length),0,broken,4,4);
        bool rejected=false;try{Decode(broken);}catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException;}
        Check(rejected,"Invalid header length accepted");
      }
      for(int chapter=1;chapter<=12;chapter++)
      {
        var campaign=S2MFileLoader.Load($"C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter{chapter}.s2m");
        Check(campaign.HeightLayer!=null,"Missing campaign HeightLayer: "+chapter);
        int count=campaign.WaterLayer.Width*campaign.WaterLayer.Height;
        Check(campaign.HeightLayer.Heights.Length==count,"Campaign grid mismatch: "+chapter);
        Check(S2MTerrainSurfaceMaterialGrid.Create(campaign,new bool[count]).Length==count,"Campaign material grid mismatch");
      }
      var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter5.s2m");
      Check(map.MapSize==128 && map.HeightLayer.Width==128 && map.HeightLayer.Heights.Length==16384,"Chapter 5 playable dimensions");
      const string install="C:/Steam/steamapps/common/Stronghold 2";
      var buildings=new S2MBuildingAssetLoader().Load(map,install,
        Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",Output+"/Cache",
        new S2MBuildingAnimationSettings {Enabled=false});
      Check(buildings.PrimaryHeightCells.Length==16384,"Chapter 5 primary mask dimensions");
      var textures=new S2MTerrainTextureAssetLoader().Load(map,install,buildings.PrimaryHeightCells,buildings.SurfaceOverrides);
      var settings=new S2MTerrainSettings {PrimaryHeightCells=buildings.PrimaryHeightCells,
        TerrainCutoutCells=buildings.TerrainCutoutCells,SurfaceOverrides=buildings.SurfaceOverrides,UsePerCellCornerHeights=true};
      var root=new GameObject("Chapter 5 regression");
      var terrain=S2MTerrainRenderer.Render(map,root.transform,settings);
      S2MTerrainTextureRenderer.Apply(terrain,textures);
      S2MWaterRenderer.Render(map,root.transform,new S2MWaterSettings {GameInstallPath=install});
      S2MBuildingRenderer.Render(buildings,root.transform,settings);
      Check(terrain.GetComponentsInChildren<MeshFilter>().Length>0,"No chapter 5 terrain geometry");
      UnityEngine.Object.DestroyImmediate(root);
      File.WriteAllText(Output+"/validation.txt","PASS: 12/76-byte headers preserve primary and all three corner planes; malformed lengths rejected; all 12 campaign maps have matching height/material grids; chapter 5 building masks, terrain textures, terrain/water/building rendering succeed at 128x128.\n");
      Debug.Log("HEIGHT_LAYER_HEADER_VALIDATION_PASS");
    }
    catch(Exception e){File.WriteAllText(Output+"/validation.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static HeightLayer Decode(byte[] bytes)
  {
    Type type=typeof(S2MFile).Assembly.GetType("Assets.Code.Stronghold2.S2MReader.ObjectReaders.HeightLayerReader");
    object reader=Activator.CreateInstance(type,new object[]{new S2Object()});
    using var input=new BinaryReader(new MemoryStream(bytes));
    return (HeightLayer)type.GetMethod("Read").Invoke(reader,new object[]{input});
  }
  static byte[] Fixture(int headerLength)
  {
    using var data=new MemoryStream();using var w=new BinaryWriter(data);
    w.Write(3);w.Write(headerLength);for(int i=0;i<headerLength;i++)w.Write((byte)(i+1));
    w.Write(-15963);w.Write(77);w.Write(1);w.Write(20);w.Write(4);
    for(int i=0;i<4;i++)w.Write(i*100f);
    w.Write(2);w.Write(3*(4+16*4));
    for(int p=0;p<3;p++){w.Write(4);for(int i=0;i<16;i++)w.Write(p*1000f+i);}
    w.Write(-57681);return data.ToArray();
  }
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
