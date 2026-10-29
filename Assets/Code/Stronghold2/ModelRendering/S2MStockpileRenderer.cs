using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;
using UnityEngine.Rendering;
namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Shared native meshes, change-driven layout, instanced draws. No per-pallet GameObjects or updates.</summary>
  public sealed class S2MStockpileRenderer : MonoBehaviour
  {
    private sealed class Part
    {public Mesh Mesh;public Material Material;public int Submesh;public Matrix4x4 Local;public readonly List<Matrix4x4> Instances=new();public readonly Matrix4x4[] World=new Matrix4x4[256];}
    private sealed class Model {public Bounds Bounds;public readonly List<Part> Parts=new();}
    private readonly Dictionary<string,Model> models=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int,BuildingPlacement> placements=new();
    private readonly List<Part> batches=new();
    private EstateInventory inventory;private S2MStockpileAssets assets;private S2MTerrainSettings terrain;
    private bool dirty=true;private MaterialPropertyBlock properties;
    public int OccupiedPallets { get; private set; }
    public int VisiblePieces { get; private set; }
    public int DrawCalls { get; private set; }
    public static S2MStockpileRenderer Create(S2MFile map,EstateInventory inventory,S2MStockpileAssets assets,Transform parent,S2MTerrainSettings terrain=null)
    {
      var root=new GameObject("Stockpile inventory");root.transform.SetParent(parent,false);
      var view=root.AddComponent<S2MStockpileRenderer>();view.inventory=inventory;view.assets=assets;view.terrain=terrain??new S2MTerrainSettings();
      root.transform.localPosition=new Vector3(view.terrain.FlipX?map.MapSize*view.terrain.HorizontalCellSize:0,0,view.terrain.FlipZ?map.MapSize*view.terrain.HorizontalCellSize:0);
      root.transform.localScale=new Vector3(view.terrain.FlipX?-1:1,1,view.terrain.FlipZ?-1:1);
      foreach(var building in map.BuildingPlacements)if(building.Type=="StockPile")view.placements.Add(building.Id,building);
      view.properties=new MaterialPropertyBlock();var colors=new Vector4[256];for(int i=0;i<colors.Length;i++)colors[i]=new Vector4(1,1,1,0);
      view.properties.SetVectorArray("_ActorLivery",colors);view.properties.SetVectorArray("_ActorPlayback",new Vector4[256]);
      view.properties.SetFloat("_ActorReceiveShadows",1);
      var templates=new GameObject("Shared goods geometry");templates.transform.SetParent(root.transform,false);
      foreach(var entry in assets.Models)
      {
        var template=Granny2ModelRenderer.Render(entry.Value,templates.transform,new Granny2ModelRenderSettings{
          UnitScale=view.terrain.HorizontalCellSize/S2MBuildingRenderer.ModelUnitsPerCell,UseFoliageMaterials=false,
          FallbackMaterial=Resources.Load<Material>("StaticOriginalModel")});
        var model=new Model{Bounds=Granny2ModelRenderer.GetLocalBounds(entry.Value,1/S2MBuildingRenderer.ModelUnitsPerCell)};
        foreach(var filter in template.GetComponentsInChildren<MeshFilter>())
        {
          var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
          for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
          {
            materials[sub].enableInstancing=true;
            var part=new Part{Mesh=filter.sharedMesh,Material=materials[sub],Submesh=sub,Local=template.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix};
            model.Parts.Add(part);view.batches.Add(part);
          }
        }
        view.models.Add(entry.Key,model);
      }
      templates.SetActive(false);inventory.Changed+=view.OnChanged;view.Synchronize();return view;
    }
    public void RegisterPlacement(BuildingPlacement building)
    {if(building.Type!="StockPile")throw new ArgumentException(nameof(building));placements[building.Id]=building;dirty=true;}
    public void RemovePlacement(int buildingId){placements.Remove(buildingId);dirty=true;}
    private void OnChanged(BuildingStorage store){if(store.Kind==StorageKind.Stockpile)dirty=true;}
    public static Vector2 PalletTile(int index)
    {
      if(index<0 || index>=30)throw new ArgumentOutOfRangeException(nameof(index));
      int n=index%15;
      return index<15?new Vector2(2-n%3,n/3):new Vector2(4+n%3,4-n/3);
    }
    public void Synchronize()
    {
      if(!dirty)return;dirty=false;OccupiedPallets=0;VisiblePieces=0;
      foreach(var batch in batches)batch.Instances.Clear();
      foreach(var store in inventory.Buildings)
      {
        if(store.Kind!=StorageKind.Stockpile || !placements.TryGetValue(store.BuildingId,out var building))continue;
        for(int i=0;i<store.PalletCount;i++)
        {
          var pallet=store.GetPallet(i);if(!pallet.Goods.HasValue || pallet.Amount==0)continue;
          var tile=PalletTile(i);var point=S2MNavigationBuilder.BuildingPoint(building,tile.x+.5f,tile.y+.5f);
          float y=building.RawY; // building ground, independent of actor size
          var anchor=new Vector3(point.X*terrain.HorizontalCellSize,y*terrain.HeightUnitScale,point.Z*terrain.HorizontalCellSize);
          OccupiedPallets++;Add(S2MStockpileAssets.PalletPath,anchor,Vector3.zero,1);
          Goods goods=pallet.Goods.Value;
          if(!assets.Custom.Contains(goods) && (goods==Goods.Wood || goods==Goods.Iron))
            NativeStack(goods,pallet.Amount,anchor);
          else if(!assets.Custom.Contains(goods) && goods==Goods.Cloth)
          {
            int left=pallet.Amount;float lift=0;
            while(left>0){int take=Math.Min(25,left);string path=S2MStockpileAssets.Folder+"cloth_"+take+".GR2";Add(path,anchor,new Vector3(0,lift,0),1);lift+=models[path].Bounds.size.y;left-=take;}
          }
          else Grid(goods,pallet.Amount,anchor);
        }
      }
    }
    private void NativeStack(Goods goods,int amount,Vector3 anchor)
    {
      string stem=goods==Goods.Wood?"wood":"iron";int per=goods==Goods.Wood?6:12,max=goods==Goods.Wood?6:3;
      float lift=0;int left=amount;
      while(left>0)
      {
        int take=Math.Min(per*max,left),layers=take/per,remainder=take%per;float top=lift;
        string partial=S2MStockpileAssets.Folder+stem+"_"+Math.Max(1,remainder)+".GR2";
        if(layers>0)
        {
          string full=S2MStockpileAssets.Folder+stem+"_layer_"+layers+".GR2";Add(full,anchor,new Vector3(0,lift,0),1);
          top+=models[full].Bounds.max.y-models[partial].Bounds.min.y;
        }
        if(remainder>0)Add(partial,anchor,new Vector3(0,top,0),1);
        lift+=models[S2MStockpileAssets.Folder+stem+"_layer_"+max+".GR2"].Bounds.size.y;left-=take;
      }
    }
    private void Grid(Goods goods,int amount,Vector3 anchor)
    {
      var visual=assets.Visuals[goods];var bounds=models[visual.ModelPath].Bounds;
      float sx=.9f/visual.Columns,sz=.9f/visual.Rows;
      float scale=Mathf.Min(1,Mathf.Min(sx/Mathf.Max(.001f,bounds.size.x),sz/Mathf.Max(.001f,bounds.size.z)))*visual.Scale;
      int per=visual.Columns*visual.Rows;float baseY=models[S2MStockpileAssets.PalletPath].Bounds.max.y;
      for(int item=0;item<amount;item++)
      {
        int n=item%per;
        var offset=new Vector3((n%visual.Columns+.5f)*sx-.45f-bounds.center.x*scale,
          baseY-bounds.min.y*scale+(item/per)*(bounds.size.y*scale+.01f),
          (n/visual.Columns+.5f)*sz-.45f-bounds.center.z*scale);
        Add(visual.ModelPath,anchor,offset,scale);
      }
    }
    private void Add(string path,Vector3 anchor,Vector3 offset,float scale)
    {
      VisiblePieces++;float vertical=terrain.HeightUnitScale*1024/terrain.HorizontalCellSize;
      offset*=terrain.HorizontalCellSize;offset.y*=vertical;
      var matrix=Matrix4x4.TRS(anchor+offset,Quaternion.identity,new Vector3(scale,scale*vertical,scale));
      foreach(var part in models[path].Parts)part.Instances.Add(matrix*part.Local);
    }
    private void LateUpdate()
    {
      Synchronize();DrawCalls=0;var root=transform.localToWorldMatrix;
      foreach(var part in batches)for(int start=0;start<part.Instances.Count;start+=256)
      {
        int count=Math.Min(256,part.Instances.Count-start);
        for(int i=0;i<count;i++)part.World[i]=root*part.Instances[start+i];
        Graphics.DrawMeshInstanced(part.Mesh,part.Submesh,part.Material,part.World,count,properties,ShadowCastingMode.On,true,gameObject.layer,null,LightProbeUsage.Off);DrawCalls++;
      }
    }
    private void OnDestroy(){if(inventory!=null)inventory.Changed-=OnChanged;}
  }
}
