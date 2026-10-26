using System;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Ground-level walls and gate parts, excluding roofs over usable passages.</summary>
  public static class S2MBuildingObstacleRasterizer
  {
    // Covers gaps between quarter-cell samples and supplies a small body clearance.
    public const float Clearance = .18f;
    private static readonly float[] SliceHeights = { .25f, .6f, 1f, 1.4f };

    public static void Rasterize(BuildingPlacement placement,Granny2ModelData model,bool[] blocked,int size,int resolution,float maximumStepHeight=.4f,float lift=0)
    {
      Quaternion rotation=Quaternion.Euler(0,placement.RotationDegrees,0);
      float ground=placement.RawY/1024;
      foreach(var mesh in model.Meshes)
      {
        var vertices=new Vector3[mesh.VertexData.Length/8];
        for(int v=0;v<vertices.Length;v++)
        {
          int i=v*8;
          vertices[v]=rotation*new Vector3(mesh.VertexData[i],mesh.VertexData[i+2],mesh.VertexData[i+1])/204.8f
            +new Vector3(placement.MapX,ground+lift,placement.MapZ);
        }
        for(int i=0;i<mesh.Indices.Length;i+=3)
        {
          Vector3 a=vertices[mesh.Indices[i]],b=vertices[mesh.Indices[i+1]],c=vertices[mesh.Indices[i+2]];
          Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
          // Walking ramps and short stair risers are surfaces to traverse, not walls to slice.
          if(Math.Abs(normal.y)>.65f || Math.Max(a.y,Math.Max(b.y,c.y))-Math.Min(a.y,Math.Min(b.y,c.y))<=maximumStepHeight)continue;
          foreach(float height in SliceHeights)
          {
            float y=ground+height;
            // A triangle crossed by the horizontal body slice gives a physical barrier segment.
            Vector3 first=default,second=default;int count=0;
            Intersect(a,b,y,ref first,ref second,ref count);
            Intersect(b,c,y,ref first,ref second,ref count);
            Intersect(c,a,y,ref first,ref second,ref count);
            if(count>=2)BlockSegment(first,second,blocked,size,resolution);
          }
        }
      }
    }

    private static void Intersect(Vector3 a,Vector3 b,float y,ref Vector3 first,ref Vector3 second,ref int count)
    {
      if((a.y<y && b.y<y) || (a.y>y && b.y>y) || Math.Abs(a.y-b.y)<.000001f)return;
      Vector3 point=Vector3.LerpUnclamped(a,b,(y-a.y)/(b.y-a.y));
      if(count>0 && (point-first).sqrMagnitude<.00000001f)return;
      if(count==0)first=point;else second=point;count++;
    }

    private static void BlockSegment(Vector3 a,Vector3 b,bool[] blocked,int size,int resolution)
    {
      int minX=Math.Max(0,(int)Math.Floor((Math.Min(a.x,b.x)-Clearance)*resolution));
      int maxX=Math.Min(size-1,(int)Math.Floor((Math.Max(a.x,b.x)+Clearance)*resolution));
      int minZ=Math.Max(0,(int)Math.Floor((Math.Min(a.z,b.z)-Clearance)*resolution));
      int maxZ=Math.Min(size-1,(int)Math.Floor((Math.Max(a.z,b.z)+Clearance)*resolution));
      Vector2 start=new(a.x,a.z),delta=new(b.x-a.x,b.z-a.z);float length=delta.sqrMagnitude;
      for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
      {
        Vector2 point=new((x+.5f)/resolution,(z+.5f)/resolution);
        float t=length>0 ? Mathf.Clamp01(Vector2.Dot(point-start,delta)/length) : 0;
        if((point-start-delta*t).sqrMagnitude<=Clearance*Clearance)blocked[z*size+x]=true;
      }
    }
  }
}
