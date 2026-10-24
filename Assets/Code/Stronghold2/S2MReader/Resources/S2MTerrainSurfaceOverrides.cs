using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>A removable building-owned surface patch; indices use row-major map coordinates.</summary>
  public sealed class S2MBuildingSurfacePatch
  {
    public int ObjectId { get; }
    public byte MaterialId { get; }
    public byte TextureRotation { get; }
    public IReadOnlyList<int> CellIndices { get; }

    public S2MBuildingSurfacePatch(int objectId, byte materialId, byte textureRotation, IEnumerable<int> cellIndices)
    {
      if (materialId == 0) throw new ArgumentOutOfRangeException(nameof(materialId));
      if (textureRotation > 3) throw new ArgumentOutOfRangeException(nameof(textureRotation));
      ObjectId = objectId;
      MaterialId = materialId;
      TextureRotation = textureRotation;
      CellIndices = new List<int>(cellIndices).AsReadOnly();
    }
  }

  /// <summary>Derived appearance only. Rebuild from remaining patches when a building is removed;
  /// the source material, orientation and height grids are never overwritten.</summary>
  public sealed class S2MTerrainSurfaceOverrides
  {
    private readonly byte[] materialIds;
    private readonly byte[] rotations;
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<S2MBuildingSurfacePatch> Patches { get; }

    public S2MTerrainSurfaceOverrides(int width, int height, IEnumerable<S2MBuildingSurfacePatch> patches)
    {
      if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
      if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
      Width = width;
      Height = height;
      materialIds = new byte[checked(width * height)];
      rotations = new byte[materialIds.Length];
      var retained = new List<S2MBuildingSurfacePatch>(patches);
      foreach (S2MBuildingSurfacePatch patch in retained)
        foreach (int index in patch.CellIndices)
        {
          if (index < 0 || index >= materialIds.Length)
            throw new ArgumentException("Surface patch cell is outside the map.", nameof(patches));
          materialIds[index] = patch.MaterialId;
          rotations[index] = patch.TextureRotation;
        }
      Patches = retained.AsReadOnly();
    }

    /// <summary>Zero means retain the ordinary terrain material.</summary>
    public byte GetMaterialId(int cellIndex) => materialIds[cellIndex];

    public bool TryGetTextureRotation(int cellIndex, out byte rotation)
    {
      rotation = rotations[cellIndex];
      return materialIds[cellIndex] != 0;
    }
  }
}
