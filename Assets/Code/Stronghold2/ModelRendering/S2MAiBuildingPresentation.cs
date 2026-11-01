using System;
using System.Collections.Generic;
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
        public S2MAiBuildingPresentation(S2MFile map,string installation,Transform parent,S2MTerrainSettings terrain,
            S2MBuildingRenderSettings settings,string converter=null)
        {
            terrainEdits=parent!=null?parent.GetComponentInChildren<S2MTerrainEdits>():null;
            this.map=map;this.parent=parent;this.terrain=terrain;this.settings=settings;
            foreach(string type in new[]{"Granary","Hovel","SawPit"})
            {
                var descriptor=S2MBuildingDescriptorReader.Read(installation,type=="Granary"?"meshes/buildings/granary.bdg":type=="Hovel"?"meshes/buildings/hovel1.bdg":"meshes/buildings/saw_pit.bdg");
                var sample=new BuildingPlacement{Id=0,Type=type,HovelAppearance=type=="Hovel"?0:(int?)null,FootprintWidth=descriptor.Width,FootprintHeight=descriptor.Height};
                var request=new S2MFile{MapSize=map.MapSize,BuildingPlacements=new[]{sample}};
                var issues=new List<S2MBuildingAssetIssue>();
                var template=new S2MPlacedBuildingAssetLoader().Load(request,installation,converter,null,issues,settings.AmbientAnimations).SingleOrDefault();
                if(template==null)throw new InvalidOperationException("Unable to prepare "+type+" assets: "+string.Join("; ",issues.Select(i=>i.Reason)));
                templates.Add(type,template);
            }
        }
        public IPreparedAiBuilding Prepare(BuildingPlacement placement)
        {
            if(parent==null)throw new InvalidOperationException("Map render root is unavailable.");
            var container=new GameObject($"AI {placement.Type} ({placement.Id})");container.SetActive(false);container.transform.SetParent(parent,false);
            try
            {
                var template=templates[placement.Type];
                var batch=new S2MPlacedBuildingBatch{Descriptor=template.Descriptor,Placements=new[]{placement},Closed=template.Closed,
                    Open=template.Open,Collision=template.Collision,StaticParts=template.StaticParts,AmbientAnimations=template.AmbientAnimations};
                var data=new S2MBuildingData{Map=map,Keeps=new S2MKeepData{Map=map},Bridges=new S2MBridgeData{Map=map},Batches=new[]{batch},GeneralBuildingCount=1};
                S2MBuildingRenderer.Render(data,container.transform,terrain,settings);
                return new View(container,terrainEdits,placement);
            }
            catch { Release(container);throw; }
        }
        private sealed class View : IPreparedAiBuilding
        {
            private GameObject root;
            private readonly S2MTerrainEdits terrain;private readonly BuildingPlacement placement;
            private IDisposable opening;
            public View(GameObject root,S2MTerrainEdits terrain,BuildingPlacement placement){this.root=root;this.terrain=terrain;this.placement=placement;}
            public void Show()
            {
                if(root==null)throw new InvalidOperationException("Prepared building was destroyed.");
                if(placement.Type=="SawPit" && terrain!=null && opening==null)opening=terrain.OpenSawpit(placement);
                root.SetActive(true);
            }
            public void Dispose(){if(root!=null)root.SetActive(false);opening?.Dispose();opening=null;if(root!=null)Release(root);root=null;}
        }
        private static void Release(GameObject o){o.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
