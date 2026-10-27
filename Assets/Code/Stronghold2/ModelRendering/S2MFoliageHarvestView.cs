using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.AssetLoading;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Map-level bridge for harvestable foliage. Only active falls receive per-frame transform updates.</summary>
  [ExecuteAlways]
  public sealed class S2MFoliageHarvestView : MonoBehaviour
  {
    private sealed class Entry
    {
      public SceneryInstance Source;
      public Transform Transform;
      public Quaternion Rotation;
      public MeshRenderer[] Renderers;
      public FoliageResource Resource;
      public bool Removed;
      public float MapScale;
      public GameObject[] Logs;
    }
    public FoliageResources Resources { get; private set; }
    public int RegisteredCount => entries.Count;
    private readonly Dictionary<int,Entry> entries=new();
    private readonly List<Entry> active=new();
    private MaterialPropertyBlock properties;
    private bool flipX,flipZ;
    private int nextId=2000000;
    private GameObject[] logTemplates;
    private float cellSize=1,pileScale=1;
    internal void PrepareLogs(IReadOnlyList<Granny2ModelData> models,float horizontalCellSize)
    {
      cellSize=horizontalCellSize;
      if(models.Count==0)return;
      if(models.Count!=6)throw new InvalidOperationException("Six original chopped-log models are required.");
      logTemplates=new GameObject[6];
      for(int i=0;i<6;i++)
      {
        logTemplates[i]=Granny2ModelRenderer.Render(models[i],transform,new Granny2ModelRenderSettings
          {UseFoliageMaterials=false,UnitScale=.01f,FallbackMaterial=UnityEngine.Resources.Load<Material>("StaticOriginalModel")},"Shared log "+(i+1));
        logTemplates[i].SetActive(false);
      }
    }
    internal void Register(SceneryInstance source,Transform instance,bool flipX,bool flipZ,float modelUnitsPerMapUnit)
    {
      if(!source.Enabled || (source.Kind!=SceneryKind.Forest && source.Kind!=SceneryKind.Shrubs)
        || Mathf.Abs(instance.localScale.x)<.0001f || Mathf.Abs(instance.localScale.y)<.0001f)return;
      this.flipX=flipX;this.flipZ=flipZ;
      if(source.ResourceId<0)source.ResourceId=nextId++;
      entries.Add(source.ResourceId,new Entry{Source=source,Transform=instance,Rotation=instance.localRotation,
        MapScale=Mathf.Abs(instance.localScale.x)*modelUnitsPerMapUnit/.01f,
        Renderers=instance.GetComponentsInChildren<MeshRenderer>(true)});
    }
    public FoliageResources Initialize(NavigationGrid navigation,WoodcuttingSettings settings)
    {
      if(Resources!=null)throw new InvalidOperationException("Foliage harvesting was already initialized.");
      properties=new MaterialPropertyBlock();
      pileScale=settings.LogPileScale*settings.UnitScale*cellSize;
      var resources=new List<FoliageResource>(entries.Count);
      foreach(var pair in entries)
      {
        var source=pair.Value.Source;
        float scale=Mathf.Clamp(pair.Value.MapScale,.05f,8);
        var resource=new FoliageResource(pair.Key,new MapPoint(source.MapX,source.MapZ),
          (source.Kind==SceneryKind.Shrubs?settings.BushRadius:settings.TreeTrunkRadius)*scale,source.Kind==SceneryKind.Shrubs);
        pair.Value.Resource=resource;resources.Add(resource);
      }
      Resources=new FoliageResources(resources,navigation,settings);Resources.Changed+=Changed;
      return Resources;
    }
    private void Changed(FoliageResource resource)
    {
      var entry=entries[resource.Id];
      if(resource.State==FoliageState.LogPile)
      {
        if(entry.Logs==null)CreatePile(entry);
        for(int i=0;i<entry.Logs.Length;i++)entry.Logs[i].SetActive(i<resource.LogsRemaining);
      }
      if(resource.State==FoliageState.Falling)
      {
        foreach(var renderer in entry.Renderers)
        {renderer.GetPropertyBlock(properties);properties.SetVector("_ModelSway",Vector4.zero);renderer.SetPropertyBlock(properties);}
        active.Add(entry);
      }
      if(resource.State==FoliageState.Removed)
      {
        entry.Removed=true;ConsumePlacement(entry);
        if(entry.Transform!=null)ReleaseInstance(entry.Transform.gameObject);
        active.Remove(entry);
      }
    }
    private void CreatePile(Entry entry)
    {
      if(logTemplates==null)throw new InvalidOperationException("Original log-pile models were not loaded.");
      var position=entry.Transform.localPosition;
      ReleaseInstance(entry.Transform.gameObject);ConsumePlacement(entry);active.Remove(entry);
      var pile=new GameObject("Log pile "+entry.Resource.Id);pile.transform.SetParent(transform,false);
      pile.transform.localPosition=position;
      float angle=entry.Resource.FallHeading*Mathf.Deg2Rad;
      float x=Mathf.Sin(angle)*(flipX?-1:1),z=Mathf.Cos(angle)*(flipZ?-1:1);
      pile.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(x,z)*Mathf.Rad2Deg,0);
      pile.transform.localScale=Vector3.one*pileScale;entry.Transform=pile.transform;
      entry.Logs=new GameObject[6];
      for(int i=0;i<6;i++)
      {
        var log=new GameObject("Log "+(i+1));log.transform.SetParent(pile.transform,false);entry.Logs[i]=log;
        foreach(var source in logTemplates[i].GetComponentsInChildren<MeshFilter>(true))
        {
          var mesh=new GameObject(source.name);mesh.transform.SetParent(log.transform,false);
          mesh.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
          mesh.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
        }
      }
      foreach(var child in pile.GetComponentsInChildren<Transform>(true))child.gameObject.layer=gameObject.layer;
    }
    private static void ConsumePlacement(Entry entry)
    {entry.Source.Enabled=false;if(entry.Source.ForestSource!=null)entry.Source.ForestSource.Variant=0xFF;}
    private static void ReleaseInstance(GameObject instance)
    {instance.SetActive(false);if(Application.isPlaying)Destroy(instance);else DestroyImmediate(instance);}
    public void Synchronize()
    {
      for(int i=active.Count-1;i>=0;i--)
      {
        var entry=active[i];
        if(entry.Removed)
        {
          if(entry.Transform!=null)
          {entry.Transform.gameObject.SetActive(false);if(Application.isPlaying)Destroy(entry.Transform.gameObject);else DestroyImmediate(entry.Transform.gameObject);}
          active.RemoveAt(i);continue;
        }
        float angle=entry.Resource.FallHeading*Mathf.Deg2Rad;
        var direction=new Vector3(Mathf.Sin(angle)*(flipX?-1:1),0,Mathf.Cos(angle)*(flipZ?-1:1));
        var axis=Vector3.Cross(Vector3.up,direction);
        entry.Transform.localRotation=Quaternion.AngleAxis(Mathf.SmoothStep(0,1,entry.Resource.FallProgress)*90,axis)*entry.Rotation;
      }
    }
    private void OnDestroy() {if(Resources!=null)Resources.Changed-=Changed;}
  }
}
