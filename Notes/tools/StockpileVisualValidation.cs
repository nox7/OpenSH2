using System;using System.IO;using System.Linq;using System.Reflection;using UnityEditor;using UnityEngine;using Assets.Code.Simulation.Economy;using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.ModelRendering;using Assets.Code.Stronghold2.TerrainRendering;
public static class StockpileVisualValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";const string Output=Repo+"/Temp/StorageValidation";
  public static void Run()
  {
    try
    {
      Directory.CreateDirectory(Output);File.Delete(Output+"/visual-failure.txt");File.Delete(Output+"/visual-pass.txt");ShaderUtil.allowAsyncCompilation=false;
      var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");var inv=S2MStoragePlan.Create(map);
      var stock=map.BuildingPlacements.First(b=>b.Type=="StockPile" && S2MCampfireLayout.EstateAtBuilding(map,b)==11);
      var assets=S2MStockpileAssets.Load("C:/Steam/steamapps/common/Stronghold 2",Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
      var root=new GameObject("Inventory visual validation");var view=S2MStockpileRenderer.Create(map,inv,assets,root.transform);
      for(int i=0;i<30;i++){var point=S2MStockpileRenderer.PalletTile(i);if(point.x==3 || point.x<0 || point.x>6 || point.y<0 || point.y>4)throw new Exception("Pallet overlaps aisle/outside stockpile");}
      if(!inv.TryDeposit(stock.Id,11,Goods.Wood,64))throw new Exception("Wood seeding failed");view.Synchronize();if(view.OccupiedPallets!=2)throw new Exception("Wood pallet count wrong");
      var open=Granny2AssetConverter.LoadOrConvert("C:/Steam/steamapps/common/Stronghold 2","meshes/buildings/stockpile_open.GR2",Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe");
      var floor=Granny2ModelRenderer.Render(open,root.transform,new Granny2ModelRenderSettings{UnitScale=1/204.8f,UseFoliageMaterials=false,FallbackMaterial=Resources.Load<Material>("StaticOriginalModel")});
      floor.transform.localPosition=new Vector3(stock.MapX,stock.RawY/1024,stock.MapZ);floor.transform.localRotation=Quaternion.Euler(0,stock.RotationDegrees,0);
      var light=new GameObject().AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.sun=light;RenderSettings.ambientLight=Color.gray;
      var target=new RenderTexture(1000,800,24);var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
      var draw=(Action)Delegate.CreateDelegate(typeof(Action),view,typeof(S2MStockpileRenderer).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
      void Capture(string name)
      {
        var camera=new GameObject().AddComponent<Camera>();camera.transform.position=new Vector3(stock.MapX+3.5f,stock.RawY/1024+9,stock.MapZ-5);
        camera.transform.LookAt(new Vector3(stock.MapX+3.5f,stock.RawY/1024,stock.MapZ+2.5f));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
        draw();camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
      }
      Capture("Wood64");inv.TryWithdraw(stock.Id,Goods.Wood,64);
      // Fill one side and part of the other, exercising every stockpile resource.
      inv.TryDeposit(stock.Id,11,Goods.Wood,360);
      foreach(var goods in inv.Catalog.All.Where(d=>d.Storage==StorageKind.Stockpile && d.Goods!=Goods.Wood))
        if(!inv.TryDeposit(stock.Id,11,goods.Goods,goods.PalletCapacity))throw new Exception("Goods preview deposit failed: "+goods.Goods);
      view.Synchronize();if(view.OccupiedPallets!=21)throw new Exception("All-goods pallet count wrong");Capture("AllGoods");
      for(int i=0;i<5;i++)draw();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)draw();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
      File.WriteAllText(Output+"/visual-pass.txt",$"PASS: native pallets and wood pile variants; every stockpile good; 30 distinct slots on both sides of a clear aisle; 21 occupied pallets; {view.VisiblePieces} pieces; {view.DrawCalls} shared instanced draw calls; {allocated} managed bytes over 30 unchanged frames.");
      if(allocated!=0)throw new Exception("Steady inventory rendering allocates: "+allocated);
      UnityEngine.Object.DestroyImmediate(root);EditorApplication.Exit(0);
    }
    catch(Exception e){File.WriteAllText(Output+"/visual-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
  }
}
