using System.Collections.Generic;
using UnityEngine;
namespace Assets.Code.Stronghold2.TerrainRendering
{
    /// <summary>Retains per-cell triangle ranges, including subdivided cliffs, to edit holes without regenerating vertices or materials.</summary>
    internal sealed class S2MTerrainChunk : MonoBehaviour
    {
        public int StartX { get; private set; }
        public int StartZ { get; private set; }
        private int width,height;private int[] original,starts,counts;private Mesh mesh;private MeshCollider collision;
        private List<int> active;
        internal void Initialize(Mesh mesh,int x,int z,int width,int height,int[] triangles,int[] starts,int[] counts)
        {this.mesh=mesh;StartX=x;StartZ=z;this.width=width;this.height=height;original=triangles;this.starts=starts;this.counts=counts;active=new List<int>(triangles.Length);collision=GetComponent<MeshCollider>();}
        internal void Refresh(bool[] holes,int mapWidth)
        {
            active.Clear();
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                if(holes[(StartZ+z)*mapWidth+StartX+x])continue;
                int cell=z*width+x,end=starts[cell]+counts[cell];
                for(int i=starts[cell];i<end;i++)active.Add(original[i]);
            }
            if(collision!=null)collision.sharedMesh=null;
            mesh.SetTriangles(active,0,calculateBounds:false);
            if(collision!=null && active.Count>0)collision.sharedMesh=mesh;
        }
    }
}
