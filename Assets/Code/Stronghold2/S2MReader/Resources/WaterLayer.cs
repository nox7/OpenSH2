using Assets.Code.Stronghold2.S2MReader.Enums;
using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Decoded water and water-like surface data from the Landscape object.</summary>
  public sealed class WaterLayer
  {
    public const int FlowDirectionStepCount = 32;

    public int Width { get; internal set; }
    public int Height { get; internal set; }
    /// <summary>
    /// Raw general landscape-feature values from block tag 19, transposed to row-major order.
    /// This grid also describes trees, traps, and structures; it is not a water bit field.
    /// </summary>
    public byte[] LandscapeFeatureValues { get; internal set; }
    /// <summary>Flow direction indices from 0 through 31, transposed to row-major order.</summary>
    public byte[] FlowDirections { get; internal set; }
    /// <summary>The first grid in Landscape block tag 27, transposed to row-major order.</summary>
    public byte[] SurfaceMaterialIds { get; internal set; }

    public bool HasWater(int x, int z)
    {
      S2MWaterType type = GetWaterType(x, z);
      return type == S2MWaterType.River || type == S2MWaterType.Sea;
    }

    public bool HasWaterLikeSurface(int x, int z)
    {
      S2MWaterType type = GetWaterType(x, z);
      return type != S2MWaterType.None && type != S2MWaterType.Unknown;
    }

    public byte GetRawLandscapeFeatureValue(int x, int z)
    {
      return LandscapeFeatureValues[GetIndex(x, z)];
    }

    public byte GetFlowDirection(int x, int z)
    {
      return FlowDirections[GetIndex(x, z)];
    }

    public float GetFlowAngleDegrees(int x, int z)
    {
      return GetFlowDirection(x, z) * (360f / FlowDirectionStepCount);
    }

    public S2MWaterType GetWaterType(int x, int z)
    {
      // These are exact categorical values established with controlled maps.
      // Do not mask the low bits: tree value 33 was previously misread as river.
      return GetRawLandscapeFeatureValue(x, z) switch
      {
        0 => S2MWaterType.None,
        1 => S2MWaterType.River,
        3 => S2MWaterType.Sea,
        5 => S2MWaterType.PitchSwamp,
        7 => S2MWaterType.Moat,
        _ => S2MWaterType.Unknown
      };
    }

    private int GetIndex(int x, int z)
    {
      if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
      if (z < 0 || z >= Height) throw new ArgumentOutOfRangeException(nameof(z));
      return z * Width + x;
    }
  }
}
