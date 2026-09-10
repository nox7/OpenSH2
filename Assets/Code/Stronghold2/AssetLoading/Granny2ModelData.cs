using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class Granny2ModelData
  {
    public string SourcePath { get; internal set; }
    public string GrannyVersion { get; internal set; }
    public IReadOnlyList<Granny2MeshData> Meshes { get; internal set; } = Array.Empty<Granny2MeshData>();
  }

  public sealed class Granny2MeshData
  {
    public string Name { get; internal set; }
    /// <summary>Eight floats per vertex: position XYZ, normal XYZ, UV.</summary>
    public float[] VertexData { get; internal set; }
    public int[] Indices { get; internal set; }
    public IReadOnlyList<Granny2MaterialData> Materials { get; internal set; } = Array.Empty<Granny2MaterialData>();
    public IReadOnlyList<Granny2TriangleGroup> TriangleGroups { get; internal set; } = Array.Empty<Granny2TriangleGroup>();
  }

  public sealed class Granny2MaterialData
  {
    public string Name { get; internal set; }
    /// <summary>Original exporter texture path retained inside the GR2 file.</summary>
    public string TexturePath { get; internal set; }
  }

  public readonly struct Granny2TriangleGroup
  {
    public int MaterialIndex { get; }
    public int TriangleFirst { get; }
    public int TriangleCount { get; }

    public Granny2TriangleGroup(int materialIndex, int triangleFirst, int triangleCount)
    {
      MaterialIndex = materialIndex;
      TriangleFirst = triangleFirst;
      TriangleCount = triangleCount;
    }
  }
}
