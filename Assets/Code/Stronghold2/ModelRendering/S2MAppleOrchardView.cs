using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;
using UnityEngine.Rendering;
namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Five shared native meshes/material sets, instanced by stage. No GameObject per tree.</summary>
  public sealed class S2MAppleOrchardView : MonoBehaviour
  {
    private sealed class Part {public Mesh Mesh;public Material[] Materials;public Matrix4x4 Local;}
    private readonly List<Part>[] parts={new(),new(),new(),new(),new()};
    private readonly GameObject[] templates=new GameObject[5];
    private readonly List<Matrix4x4[]>[] matrices={new(),new(),new(),new(),new()};
    private readonly int[] counts=new int[5];
    private readonly Matrix4x4[] scratch=new Matrix4x4[1023];
    private readonly Dictionary<int,Matrix4x4> placements=new();
    private AppleOrchards orchards;private bool dirty=true;
    public int VisibleTrees { get; private set; }
    public void Initialize(AppleOrchards orchards,S2MVegetationData data,S2MVegetationRenderSettings settings,float treeScale,S2MSceneryModelScales scales)
    {
      if(this.orchards!=null)throw new InvalidOperationException("Orchard view already initialized.");
      this.orchards=orchards;orchards.Changed+=OnChanged;
      if(orchards.Trees.Count==0)return;
      if(data.AppleTreeModels.Count!=5)throw new InvalidOperationException("Five native apple tree growth models are required.");
      foreach(var tree in orchards.Trees)
      {
        var source=new SceneryInstance{Kind=SceneryKind.AppleTree,RawX=tree.RenderPosition.X*1024,RawZ=tree.RenderPosition.Z*1024,RawY=tree.RawHeight,RotationDegrees=tree.Heading};
        var instance=S2MVegetationRenderer.ToUnityInstance(source,data.Map,settings,0,treeScale,scales);
        placements.Add(tree.Id,Matrix4x4.TRS(instance.Position,Quaternion.Euler(instance.EulerAngles),instance.Scale));
      }
      for(int stage=0;stage<5;stage++)
      {
        var modelSettings=new Granny2ModelRenderSettings{UnitScale=settings.Model.UnitScale,ConvertZUpToUnityYUp=settings.Model.ConvertZUpToUnityYUp,
          DoubleSided=settings.Model.DoubleSided,UseFoliageMaterials=settings.Model.UseFoliageMaterials,OpaqueShader=settings.Model.OpaqueShader,
          FallbackMaterial=settings.Model.FallbackMaterial,TextureBrightness=settings.Model.TextureBrightness,AlphaCutoff=settings.Model.AlphaCutoff};
        templates[stage]=Granny2ModelRenderer.Render(data.AppleTreeModels[stage],transform,modelSettings,"Shared apple stage "+stage);
        foreach(var filter in templates[stage].GetComponentsInChildren<MeshFilter>())
        {
          var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;foreach(var material in materials)material.enableInstancing=true;
          parts[stage].Add(new Part{Mesh=filter.sharedMesh,Materials=materials,Local=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
        }
        templates[stage].SetActive(false);
        for(int i=0;i<orchards.Trees.Count;i+=1023)matrices[stage].Add(new Matrix4x4[Math.Min(1023,orchards.Trees.Count-i)]);
      }
    }
    private void OnChanged(OrchardTree tree)=>dirty=true;
    private void Rebuild()
    {
      Array.Clear(counts,0,counts.Length);VisibleTrees=0;
      foreach(var tree in orchards.Trees)
      {
        if(!tree.Active || tree.Stage<0)continue;int stage=tree.Stage==5?1:tree.Stage,index=counts[stage]++;
        matrices[stage][index/1023][index%1023]=transform.localToWorldMatrix*placements[tree.Id];VisibleTrees++;
      }
      dirty=false;transform.hasChanged=false;
    }
    private void LateUpdate()=>Draw();
    public void Draw(Camera camera=null)
    {
      if(orchards==null || orchards.Trees.Count==0)return;if(dirty || transform.hasChanged)Rebuild();
      for(int stage=0;stage<5;stage++)foreach(var part in parts[stage])
      {
        for(int start=0;start<counts[stage];start+=1023)
        {
          var chunk=matrices[stage][start/1023];int count=Math.Min(1023,counts[stage]-start);
          if(part.Local!=Matrix4x4.identity){for(int i=0;i<count;i++)scratch[i]=chunk[i]*part.Local;chunk=scratch;}
          for(int sub=0;sub<part.Mesh.subMeshCount;sub++)Graphics.DrawMeshInstanced(part.Mesh,sub,part.Materials[sub],chunk,count,null,ShadowCastingMode.On,true,gameObject.layer,camera,LightProbeUsage.Off);
        }
      }
    }
    private void OnDestroy(){if(orchards!=null)orchards.Changed-=OnChanged;}
  }
}
