using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class HeightLayer : S2Object
  {
    public const int CornersPerCell = 4;

    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>The first, confirmed float32 height plane in row-major file order.</summary>
    public float[] Heights { get; set; }
    /// <summary>
    /// Three separately serialized planes containing four corner heights per
    /// cell. Plane zero follows the base terrain. The purposes of the two
    /// sparse upper planes are not known yet.
    /// </summary>
    public float[][] CornerHeightPlanes { get; set; }

    /// <summary>
    /// Returns one of the four serialized corner heights for a cell. Corner
    /// ordering is documented in Notes/FileFormats/S2M/S2GameSegment/HeightLayer.md.
    /// </summary>
    public float GetCornerHeight(int plane, int cellIndex, int corner)
    {
      if (CornerHeightPlanes == null) throw new InvalidOperationException("Corner height planes have not been loaded.");
      if (plane < 0 || plane >= CornerHeightPlanes.Length) throw new ArgumentOutOfRangeException(nameof(plane));
      if (cellIndex < 0 || cellIndex >= Width * Height) throw new ArgumentOutOfRangeException(nameof(cellIndex));
      if (corner < 0 || corner >= CornersPerCell) throw new ArgumentOutOfRangeException(nameof(corner));

      return CornerHeightPlanes[plane][cellIndex * CornersPerCell + corner];
    }
  }
}
