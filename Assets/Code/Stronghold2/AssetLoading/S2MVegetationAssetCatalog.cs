using Assets.Code.Stronghold2.S2MReader.Resources;
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
      },
      LandscapeAssets("tree_2", "tree_2b"),
      LandscapeAssets("tree_3", "tree_3b", "tree_3c"),
      LandscapeAssets("tree_4", "tree_4b"),
      LandscapeAssets("tree_5"),
      LandscapeAssets("tree_6", "tree_6b", "tree_6c"),
      LandscapeAssets("tree_7", "tree_7b", "tree_7c", "tree_7d"),
      LandscapeAssets("tree_8"),
      LandscapeAssets("marsh_tree_1", "marsh_tree_2", "marsh_tree_3", "marsh_tree_4", "marsh_tree_5", "marsh_tree_6", "marsh_tree_7"),
      LandscapeAssets("tree_10"),
      LandscapeAssets("tree_11"),
      LandscapeAssets("tree_12", "tree_12b")
    };

    private static readonly string[][] RockAssets =
    {
      LandscapeAssets("rock_4", "rock_5", "rock_6", "rock_9", "rock_12"),
      LandscapeAssets("rock_2", "rock_8", "rock_14"), LandscapeAssets("rock_1", "rock_3"), LandscapeAssets("rock_7", "rock_10", "rock_13"),
      Numbered("rock_flat_s_", 6), Numbered("rock_flat_m_", 6),
      Numbered("rock_flat_l_", 6), Numbered("rock_flat_xl_", 6), Numbered("rock_iron_", 12)
    };

    private static string[] LandscapeAssets(params string[] names)
    {
      var paths = new string[names.Length];
      for (int i = 0; i < names.Length; i++) paths[i] = "meshes/landscape/" + names[i] + ".gr2";
      return paths;
    }

    private static string[] Numbered(string prefix, int count)
    {
      var names = new string[count];
      for (int i = 0; i < count; i++) names[i] = prefix + (i + 1);
      return LandscapeAssets(names);
    }

    public static bool TryGetSceneryAsset(
      SceneryKind kind, ushort family, ushort variant, out string path)
    {
      path = null;
      switch (kind)
      {
        case SceneryKind.Shrubs:
          if (family >= 9 || variant != 0) return false;
          path = "meshes/landscape/shrub_" + (family + 1) + ".gr2";
          return true;
        case SceneryKind.NaturalStone:
          if (family >= 12) return false;
          path = "meshes/landscape/rock_natural_stone_" + (char)('A' + family / 2) + (family % 2 + 1) + ".gr2";
          return true;
        case SceneryKind.Rocks:
          if (family >= RockAssets.Length || variant >= RockAssets[family].Length) return false;
          path = RockAssets[family][variant];
          return true;
        default: return false;
      }
    }

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
      // Only families 0–7 have installed low-detail counterparts. Later families
      // use their full model rather than requesting nonexistent .low files.
      if (lowDetail && family <= 7 && relativePath.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase))
        relativePath = relativePath.Substring(0, relativePath.Length - ".gr2".Length) + ".low.gr2";
      return true;
    }
  }
}
