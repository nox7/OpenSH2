using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Map tag 14, in its original row-major backing-grid dimensions.</summary>
  public sealed class WallLayer
  {
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public WallCell[] Cells { get; internal set; }

    public WallCell GetCell(int x, int z)
    {
      if (x < 0 || x >= Width || z < 0 || z >= Height) throw new ArgumentOutOfRangeException();
      return Cells[z * Width + x];
    }
  }

  /// <summary>One packed 24-byte wall/surface record; unknown bytes are preserved.</summary>
  public readonly struct WallCell
  {
    public readonly float Height0, Height1, Height2, Height3;
    public readonly byte Shape, Flags, TopEdges, SideEdges, Auxiliary, Unknown21, Unknown22, Owner;
    public bool HasWall => Shape >= 1 && Shape <= 5;
    public bool IsWood => (Flags & 2) != 0;

    public WallCell(float h0, float h1, float h2, float h3, byte shape, byte flags,
      byte topEdges, byte sideEdges, byte auxiliary, byte unknown21, byte unknown22, byte owner)
    {
      Height0 = h0; Height1 = h1; Height2 = h2; Height3 = h3;
      Shape = shape; Flags = flags; TopEdges = topEdges; SideEdges = sideEdges;
      Auxiliary = auxiliary; Unknown21 = unknown21; Unknown22 = unknown22; Owner = owner;
    }

    /// <summary>
    /// Height in HeightLayer geometric corner order: +Z pair, then -Z pair.
    /// Map tag 14 stores the -Z pair first, unlike HeightLayer plane zero.
    /// The Height0..Height3 fields retain the original serialized order.
    /// </summary>
    public float GetHeight(int corner) => corner switch
    {
      0 => Height2, 1 => Height3, 2 => Height0, 3 => Height1,
      _ => throw new ArgumentOutOfRangeException(nameof(corner))
    };
  }
}
