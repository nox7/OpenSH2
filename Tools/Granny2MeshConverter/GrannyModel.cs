using System.Collections.Generic;

namespace OpenSH2.Granny2MeshConverter
{
  internal sealed class GrannyModel
  {
    public List<GrannyMesh> Meshes { get; } = new List<GrannyMesh>();
  }

  internal sealed class GrannyMesh
  {
    public string Name { get; set; }
    public float[] Vertices { get; set; }
    public int[] Indices { get; set; }
    public List<GrannyMaterial> Materials { get; } = new List<GrannyMaterial>();
    public List<GrannyTriangleGroup> Groups { get; } = new List<GrannyTriangleGroup>();
  }

  internal sealed class GrannyMaterial
  {
    public string Name { get; set; }
    public string TexturePath { get; set; }
  }

  internal struct GrannyTriangleGroup
  {
    public int MaterialIndex;
    public int TriangleFirst;
    public int TriangleCount;
  }
}
