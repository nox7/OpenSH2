using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2.AssetLoading;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  internal static class Granny2AnimationRendering
  {
    internal static Texture2D CreatePoseTexture(Granny2ActorMotion motion, Granny2GeneratedModelResources resources, string name)
    {
      if (motion.Bones * 4 > SystemInfo.maxTextureSize || motion.Frames > SystemInfo.maxTextureSize)
        throw new NotSupportedException("Animation exceeds GPU texture limits; reduce the sampling rate.");
      var texture = new Texture2D(motion.Bones * 4, motion.Frames, TextureFormat.RGBAFloat, false, true)
      { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
      resources.Add(texture);
      texture.SetPixelData(motion.Matrices, 0); texture.Apply(false, true);
      return texture;
    }

    internal static void ApplySkin(Mesh mesh, Granny2ActorMotion motion, Vector4[] indices, Vector4[] weights, float scale)
    {
      if (mesh.vertexCount != indices.Length || indices.Length != weights.Length) throw new InvalidOperationException("Animation mesh/skin mismatch.");
      mesh.SetUVs(1, new List<Vector4>(indices)); mesh.SetUVs(2, new List<Vector4>(weights));
      mesh.bounds = AnimatedBounds(mesh.vertices, motion, indices, weights, scale);
    }

    internal static void ConfigureMaterial(Material material, Granny2ActorMotion motion, Texture2D texture, float scale)
    {
      material.SetTexture("_ActorPoses", texture);
      material.SetVector("_ActorClip", new Vector4(motion.Bones, motion.Frames, motion.Duration, scale));
    }

    internal static Bounds AnimatedBounds(Vector3[] vertices, Granny2ActorMotion motion, Vector4[] indices, Vector4[] weights, float unitScale)
    {
      var bounds = new Bounds(); bool initialized = false;
      for (int frame = 0; frame < motion.Frames; frame++)
        for (int v = 0; v < vertices.Length; v++)
        {
          Vector3 p = new Vector3(vertices[v].x, vertices[v].z, vertices[v].y) / unitScale, posed = Vector3.zero;
          for (int w = 0; w < 4; w++)
          {
            float weight = weights[v][w]; if (weight == 0) continue;
            int b = (frame * motion.Bones + (int)indices[v][w]) * 16;
            float[] a = motion.Matrices;
            posed += weight * new Vector3(p.x * a[b] + p.y * a[b + 4] + p.z * a[b + 8] + a[b + 12],
              p.x * a[b + 1] + p.y * a[b + 5] + p.z * a[b + 9] + a[b + 13],
              p.x * a[b + 2] + p.y * a[b + 6] + p.z * a[b + 10] + a[b + 14]);
          }
          posed = new Vector3(posed.x, posed.z, posed.y) * unitScale;
          if (!initialized) { bounds = new Bounds(posed, Vector3.zero); initialized = true; } else bounds.Encapsulate(posed);
        }
      bounds.Expand(.05f); return bounds;
    }

  }
}
