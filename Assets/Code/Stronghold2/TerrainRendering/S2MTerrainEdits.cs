using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.AssetLoading;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
    /// <summary>Reference-counted terrain openings. Only affected chunk index buffers and colliders change.</summary>
    public sealed class S2MTerrainEdits : MonoBehaviour
    {
        private int width,height,chunkSize;
        private bool[] baseline,current;
        private int[] references;
        private readonly Dictionary<(int,int),S2MTerrainChunk> chunks=new();
        public int UpdatedChunks { get; private set; }
        public bool IsCutout(int x,int z)=>current[z*width+x];
        internal void Initialize(S2MFile map,S2MTerrainSettings settings)
        {
            width=map.HeightLayer.Width;height=map.HeightLayer.Height;chunkSize=settings.CellsPerChunk;
            current=settings.TerrainCutoutCells;baseline=(bool[])current.Clone();references=new int[current.Length];
            foreach(var chunk in GetComponentsInChildren<S2MTerrainChunk>())chunks.Add((chunk.StartX/chunkSize,chunk.StartZ/chunkSize),chunk);
        }
        /// <summary>Physical sawpit opening, never the placement apron or larger saved worker-clearance rectangle.</summary>
        public static int[] SawpitCells(BuildingPlacement placement,int width,int height)
        {
            if(placement.Type!="SawPit")return Array.Empty<int>();
            int turn=((int)Math.Round(placement.RotationDegrees/90)%4+4)%4;
            var physical=new BuildingPlacement{Id=placement.Id,RawX=placement.RawX,RawZ=placement.RawZ,RotationDegrees=placement.RotationDegrees,
                FootprintWidth=turn%2==0?1:2,FootprintHeight=turn%2==0?2:1};
            var f=S2MBuildingFootprint.FromPlacement(physical);
            if(f.MinX<0 || f.MinZ<0 || f.MaxXExclusive>width || f.MaxZExclusive>height)throw new ArgumentOutOfRangeException(nameof(placement));
            var cells=new List<int>();
            for(int z=f.MinZ;z<f.MaxZExclusive;z++)for(int x=f.MinX;x<f.MaxXExclusive;x++)cells.Add(z*width+x);
            return cells.ToArray();
        }
        public IDisposable OpenSawpit(BuildingPlacement placement)
        {
            int[] cells=SawpitCells(placement,width,height);
            try{Change(cells,1);return new Opening(this,cells);}
            catch{Change(cells,-1);throw;}
        }
        private void Change(int[] cells,int delta)
        {
            var changed=new HashSet<(int,int)>();
            foreach(int cell in cells)
            {
                references[cell]+=delta;bool value=baseline[cell] || references[cell]>0;
                if(current[cell]==value)continue;current[cell]=value;
                changed.Add((cell%width/chunkSize,cell/width/chunkSize));
            }
            foreach(var key in changed)if(chunks.TryGetValue(key,out var chunk)){chunk.Refresh(current,width);UpdatedChunks++;}
        }
        private sealed class Opening : IDisposable
        {
            private S2MTerrainEdits owner;private readonly int[] cells;
            public Opening(S2MTerrainEdits owner,int[] cells){this.owner=owner;this.cells=cells;}
            public void Dispose(){if(owner!=null)owner.Change(cells,-1);owner=null;}
        }
    }
}
