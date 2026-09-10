using System.IO;
using System.Text;

namespace OpenSH2.Granny2MeshConverter
{
  internal static class GrannyMeshCacheWriter
  {
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OSH2GR2\0");

    public static void Write(string outputPath, string sourcePath, string grannyVersion, GrannyModel model)
    {
      string fullOutputPath = Path.GetFullPath(outputPath);
      string directory = Path.GetDirectoryName(fullOutputPath);
      if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

      string temporaryPath = fullOutputPath + ".tmp";
      using (var stream = File.Create(temporaryPath))
      using (var writer = new BinaryWriter(stream, Encoding.UTF8))
      {
        writer.Write(Magic);
        writer.Write(1);
        writer.Write(Path.GetFullPath(sourcePath));
        writer.Write(grannyVersion ?? string.Empty);
        writer.Write(model.Meshes.Count);
        foreach (GrannyMesh mesh in model.Meshes)
        {
          writer.Write(mesh.Name ?? string.Empty);
          writer.Write(mesh.Vertices.Length / 8);
          foreach (float value in mesh.Vertices) writer.Write(value);
          writer.Write(mesh.Indices.Length);
          foreach (int index in mesh.Indices) writer.Write(index);

          writer.Write(mesh.Materials.Count);
          foreach (GrannyMaterial material in mesh.Materials)
          {
            writer.Write(material.Name ?? string.Empty);
            writer.Write(material.TexturePath ?? string.Empty);
          }

          writer.Write(mesh.Groups.Count);
          foreach (GrannyTriangleGroup group in mesh.Groups)
          {
            writer.Write(group.MaterialIndex);
            writer.Write(group.TriangleFirst);
            writer.Write(group.TriangleCount);
          }
        }
      }

      if (File.Exists(fullOutputPath)) File.Delete(fullOutputPath);
      File.Move(temporaryPath, fullOutputPath);
    }
  }
}
