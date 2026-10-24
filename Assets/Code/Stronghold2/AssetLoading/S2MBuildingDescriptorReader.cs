using System;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class S2MBuildingDescriptor
  {
    public string RelativePath { get; internal set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public string ClosedModel { get; internal set; }
    public string OpenModel { get; internal set; }
    public string CollisionModel { get; internal set; }
  }

  /// <summary>The verified model-reference prefix of version-13 BDGs, not their entire schema.</summary>
  public static class S2MBuildingDescriptorReader
  {
    public static S2MBuildingDescriptor Read(string gameInstallPath, string relativePath)
    {
      byte[] bytes = File.ReadAllBytes(Path.Combine(gameInstallPath, relativePath));
      if (bytes.Length < 195 || BitConverter.ToInt32(bytes, 0) != 13)
        throw new InvalidDataException($"Unsupported BDG prefix: {relativePath}.");
      if (bytes[64] > 1) throw new InvalidDataException($"Invalid BDG open-view flag: {relativePath}.");
      string closed = ReadModelName(bytes, 32);
      if (closed == null) throw new InvalidDataException($"BDG has no exterior model: {relativePath}.");
      // Inactive fields may contain stale exporter strings; honor the flag.
      string open = bytes[64] == 1 ? ReadModelName(bytes, 65) : null;
      if (bytes[64] == 1 && open == null)
        throw new InvalidDataException($"BDG enables an invalid open model: {relativePath}.");
      return new S2MBuildingDescriptor
      {
        RelativePath = relativePath, Width = BitConverter.ToUInt16(bytes, 8),
        Height = BitConverter.ToUInt16(bytes, 10), ClosedModel = closed,
        OpenModel = open, CollisionModel = ReadModelName(bytes, 163)
      };
    }

    private static string ReadModelName(byte[] bytes, int offset)
    {
      string value = Encoding.ASCII.GetString(bytes, offset, 32).Split('\0')[0];
      if (string.IsNullOrWhiteSpace(value)) return null;
      if (value.IndexOfAny(new[] { '/', '\\', ':' }) >= 0
        || !value.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase)) return null;
      return "meshes/buildings/" + value;
    }
  }
}
