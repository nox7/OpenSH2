using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
    /// <summary>Preloads native estate building assets once; committing buildings performs no disk access or conversion.</summary>
    public sealed class S2MAiBuildingPresentation
    {
        private readonly S2MFile map;
        private readonly Transform parent;
        private readonly S2MTerrainSettings terrain;
        private readonly S2MBuildingRenderSettings settings;
        private readonly S2MTerrainEdits terrainEdits;
        private readonly Dictionary<string,S2MPlacedBuildingBatch> templates=new();
        private readonly Dictionary<Granny2ModelData,Granny2ModelRenderer.SharedModel> sharedModels=new();
        public S2MAiBuildingPresentation(S2MFile map,string installation,Transform parent,S2MTerrainSettings terrain,
            S2MBuildingRenderSettings settings,string converter=null)
        {
            terrainEdits=parent!=null?parent.GetComponentInChildren<S2MTerrainEdits>():null;
            this.map=map;this.parent=parent;this.terrain=terrain;this.settings=settings;
            S2MSceneryOcclusion.GetOrCreate(parent,map);
            foreach(string type in new[]{"Granary","Hovel","SawPit","StockPile"})
            {
                var descriptor=S2MBuildingDescriptorReader.Read(installation,type=="Granary"?"meshes/buildings/granary.bdg":type=="Hovel"?"meshes/buildings/hovel1.bdg":type=="SawPit"?"meshes/buildings/saw_pit.bdg":"meshes/buildings/stockpile.bdg");
                var sample=new BuildingPlacement{Id=0,Type=type,HovelAppearance=type=="Hovel"?0:(int?)null,FootprintWidth=descriptor.Width,FootprintHeight=descriptor.Height};
                var request=new S2MFile{MapSize=map.MapSize,BuildingPlacements=new[]{sample}};
                var issues=new List<S2MBuildingAssetIssue>();
                var template=new S2MPlacedBuildingAssetLoader().Load(request,installation,converter,null,issues,settings.AmbientAnimations).SingleOrDefault();
                if(template==null)throw new InvalidOperationException("Unable to prepare "+type+" assets: "+string.Join("; ",issues.Select(i=>i.Reason)));
                templates.Add(type,template);
                Warm(template.Closed,template.Collision==null);Warm(template.Open,false);Warm(template.Collision,true);
                foreach(var part in template.StaticParts)Warm(part,false);
            }
        }
        private void Warm(Granny2ModelData model,bool collision)
        {
            if(model==null)return;
            if(sharedModels.TryGetValue(model,out var existing)){if(collision)existing.BakeCollision();return;}
            sharedModels.Add(model,Granny2ModelRenderer.PrepareShared(model,parent,new Granny2ModelRenderSettings
            {UnitScale=terrain.HorizontalCellSize/S2MBuildingRenderer.ModelUnitsPerCell,UseFoliageMaterials=false,
             FallbackMaterial=Resources.Load<Material>("StaticOriginalModel"),Livery=S2MLiveryContext.Find(parent)},collision));
        }
        public IPreparedAiBuilding Prepare(BuildingPlacement placement)
        {
            var view=(View)BeginPreparation(placement);
            try{while(!view.IsPrepared)view.AdvancePreparation();return view;}
            catch{view.Dispose();throw;}
        }
        public IPreparedAiBuilding BeginPreparation(BuildingPlacement placement)
        {
            if(parent==null)throw new InvalidOperationException("Map render root is unavailable.");
            var container=new GameObject($"AI {placement.Type} ({placement.Id})");container.SetActive(false);container.transform.SetParent(parent,false);
            try
            {
                var template=templates[placement.Type];
                var batch=new S2MPlacedBuildingBatch{Descriptor=template.Descriptor,Placements=new[]{placement},Closed=template.Closed,
                    Open=template.Open,Collision=template.Collision,StaticParts=template.StaticParts,AmbientAnimations=template.AmbientAnimations};
                var data=new S2MBuildingData{Map=map,Keeps=new S2MKeepData{Map=map},Bridges=new S2MBridgeData{Map=map},Batches=new[]{batch},GeneralBuildingCount=1};
                var rendering=S2MBuildingRenderer.BeginRender(data,container.transform,terrain,settings,
                    (model,instances,target,modelSettings,name)=>sharedModels[model].Instantiate(instances,target,name));
                return new View(container,terrainEdits,placement,rendering.Steps);
            }
            catch { Release(container);throw; }
        }
        private sealed class View : IStagedAiBuilding
        {
            private GameObject root;
            private readonly S2MTerrainEdits terrain;private readonly BuildingPlacement placement;
            private IDisposable opening;
            private IEnumerator steps;
            public bool IsPrepared=>steps==null;
            public void AdvancePreparation()
            {
                if(steps==null)return;
                if(!steps.MoveNext()){(steps as IDisposable)?.Dispose();steps=null;}
            }
            public View(GameObject root,S2MTerrainEdits terrain,BuildingPlacement placement,IEnumerator steps){this.root=root;this.terrain=terrain;this.placement=placement;this.steps=steps;}
            public void Show()
            {
                if(root==null || !IsPrepared)throw new InvalidOperationException("Building presentation is not ready.");
                if(placement.Type=="SawPit" && terrain!=null && opening==null)opening=terrain.OpenSawpit(placement);
                root.SetActive(true);
            }
            public void Dispose(){(steps as IDisposable)?.Dispose();steps=null;if(root!=null)root.SetActive(false);opening?.Dispose();opening=null;if(root!=null)Release(root);root=null;}
        }
        private static void Release(GameObject o){o.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
