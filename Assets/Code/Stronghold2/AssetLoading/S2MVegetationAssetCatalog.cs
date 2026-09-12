using System;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Authoritative lookup for known Stronghold 2 Forest family and variant assets.</summary>
  public static class S2MVegetationAssetCatalog
  {
    // Indexed as [ForestInstance.Family][ForestInstance.Variant]. Paths are relative
    // to the user-supplied, legally owned Stronghold 2 installation.
    private static readonly string[][] ForestTreeAssets =
    {
      new[]
      {
        "meshes/landscape/forest_tree_1.gr2",
        "meshes/landscape/forest_tree_2.gr2",
        "meshes/landscape/forest_tree_3.gr2",
        "meshes/landscape/forest_tree_4.gr2"
      },
      new[]
      {
        "meshes/landscape/winter_tree_1.gr2",
        "meshes/landscape/winter_tree_2.gr2",
        "meshes/landscape/winter_tree_3.gr2",
        "meshes/landscape/winter_tree_4.gr2"
      },
      new[]
      {
        "meshes/landscape/tree_1.gr2",
        "meshes/landscape/tree_1b.gr2",
        "meshes/landscape/tree_1c.gr2",
        "meshes/landscape/tree_1d.gr2"
      }
    };

    public static bool TryGetTreeAsset(
      ushort family,
      ushort variant,
      bool lowDetail,
      out string relativePath)
    {
      relativePath = null;
      if (family >= ForestTreeAssets.Length || variant >= ForestTreeAssets[family].Length)
        return false;

      relativePath = ForestTreeAssets[family][variant];
      if (lowDetail && relativePath.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase))
        relativePath = relativePath.Substring(0, relativePath.Length - ".gr2".Length) + ".low.gr2";
      return true;
    }
  }
}
