using System.IO;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Maps the three controlled Forest families to Stronghold 2 landscape meshes.</summary>
  public static class S2MVegetationAssetCatalog
  {
    public static bool TryGetTreeAsset(ushort family, ushort variant, bool lowDetail, out string relativePath)
    {
      string suffix = null;
      switch (family)
      {
        case 0:
          suffix = variant switch { 0 => "", 1 => "b", 2 => "c", 3 => "d", _ => "" };
          break;
        case 1:
          // Tree 2 has two geometry variants although the record field uses four values.
          suffix = variant == 1 || variant == 3 ? "b" : "";
          break;
        case 2:
          suffix = variant switch { 0 => "", 1 => "b", 2 => "c", _ => "" };
          break;
        default:
          relativePath = null;
          return false;
      }

      string detail = lowDetail ? ".low" : string.Empty;
      relativePath = Path.Combine("meshes", "landscape", $"tree_{family + 1}{suffix}{detail}.gr2");
      return true;
    }
  }
}
