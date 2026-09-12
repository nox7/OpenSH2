using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public sealed class Landscape : S2Object
  {
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int LogicalMapSize { get; internal set; }
    /// <summary>The first Landscape float grid, transposed to row-major order.</summary>
    public float[] Heights { get; internal set; }
    public WaterLayer Water { get; internal set; }
    /// <summary>
    /// Per-cell terrain tint decoded from the first RGBA grid in Landscape tag 22,
    /// transposed to row-major order. Null when the source variant has no such grid.
    /// </summary>
    public Color32[] TerrainTintColors { get; internal set; }
    /// <summary>
    /// Candidate per-cell terrain texture orientation values decoded from the first
    /// 256x256 grid in Landscape tag 30. Values observed in shipped and controlled
    /// maps are 0 through 3 and are interpreted as quarter-turns by the terrain
    /// renderer. The surrounding tag-27 tail remains preserved as raw data because
    /// the probe changes a corresponding byte there as well.
    /// </summary>
    public byte[] TerrainTextureRotationValues { get; internal set; }
    /// <summary>Complete type-specific payload excluding the object trailer.</summary>
    public byte[] RawPayload { get; internal set; }
  }

  /// <summary>
  /// Produces the effective terrain-material grid without mutating decoded S2M data.
  /// Forest records occupy a two-by-two cell area anchored at their coarse cell coordinate.
  /// </summary>
  public static class S2MTerrainSurfaceMaterialGrid
  {
    public const byte ForestFloorMaterialId = 0x1F;
    public const byte RockSlopeMaterialId = 0x1E;
    public const byte CliffFaceMaterialId = 0x27;
    // The controlled probe produced 905.3849 raw height units for the first
    // rock-producing slope. Cliff cells are selected explicitly by material 0x27;
    // deriving that selector from height incorrectly converts diagonal corner
    // slopes before the original game does.
    public const float RockSlopeHeightDifference = 900f;
    private const int ForestFootprintSize = 2;

    public static byte[] Create(S2MFile map)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      WaterLayer water = map.WaterLayer;
      if (water?.SurfaceMaterialIds == null || water.SurfaceMaterialIds.Length != water.Width * water.Height)
        throw new InvalidOperationException("The map does not contain a complete terrain material grid.");

      var materialIds = (byte[])water.SurfaceMaterialIds.Clone();
      ApplySlopeOverrides(map, materialIds);
      if (map.Forest?.Instances == null) return materialIds;

      foreach (ForestInstance tree in map.Forest.Instances)
      {
        int startX = Math.Min(tree.CellX * ForestFootprintSize, water.Width - 1);
        int startZ = Math.Min(tree.CellZ * ForestFootprintSize, water.Height - 1);
        for (int z = startZ; z < Math.Min(startZ + ForestFootprintSize, water.Height); z++)
        {
          for (int x = startX; x < Math.Min(startX + ForestFootprintSize, water.Width); x++)
            materialIds[z * water.Width + x] = ForestFloorMaterialId;
        }
      }
      return materialIds;
    }

    private static void ApplySlopeOverrides(S2MFile map, byte[] materialIds)
    {
      HeightLayer heightLayer = map.HeightLayer;
      if (heightLayer?.Heights == null || heightLayer.Width <= 0 || heightLayer.Height <= 0)
        return;
      if (materialIds.Length < heightLayer.Width * heightLayer.Height)
        return;

      for (int z = 0; z < heightLayer.Height; z++)
      {
        for (int x = 0; x < heightLayer.Width; x++)
        {
          int index = z * heightLayer.Width + x;
          // An authored cliff selector is authoritative. Height alone forces the
          // ordinary rock slope, but never invents cliff subdivision geometry.
          if (materialIds[index] == CliffFaceMaterialId) continue;
          float difference = GetCellHeightDifference(heightLayer, index, x, z);
          if (difference >= RockSlopeHeightDifference) materialIds[index] = RockSlopeMaterialId;
        }
      }
    }

    private static float GetCellHeightDifference(HeightLayer heightLayer, int cellIndex, int x, int z)
    {
      float c0 = GetCornerHeight(heightLayer, cellIndex, 0, x, z + 1);
      float c1 = GetCornerHeight(heightLayer, cellIndex, 1, x + 1, z + 1);
      float c2 = GetCornerHeight(heightLayer, cellIndex, 2, x, z);
      float c3 = GetCornerHeight(heightLayer, cellIndex, 3, x + 1, z);
      return Math.Max(
        Math.Max(Math.Abs(c0 - c1), Math.Abs(c0 - c2)),
        Math.Max(Math.Abs(c1 - c3), Math.Abs(c2 - c3)));
    }

    private static float GetCornerHeight(HeightLayer heightLayer, int cellIndex, int corner, int x, int z)
    {
      if (heightLayer.CornerHeightPlanes?.Length > 0
        && heightLayer.CornerHeightPlanes[0]?.Length == heightLayer.Width * heightLayer.Height * HeightLayer.CornersPerCell)
        return heightLayer.CornerHeightPlanes[0][cellIndex * HeightLayer.CornersPerCell + corner];
      x = Math.Max(0, Math.Min(x, heightLayer.Width - 1));
      z = Math.Max(0, Math.Min(z, heightLayer.Height - 1));
      return heightLayer.Heights[z * heightLayer.Width + x];
    }
  }
}
