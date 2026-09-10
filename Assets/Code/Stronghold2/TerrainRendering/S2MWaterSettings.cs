using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public sealed class S2MWaterSettings
  {
    [Min(0.0001f)]
    public float HorizontalCellSize = 1f;

    [Min(0.000001f)]
    public float HeightUnitScale = 1f / 1024f;

    [Tooltip("Small vertical separation from the decoded water surface to avoid z-fighting.")]
    public float SurfaceOffset = 0.01f;

    [Tooltip("HeightLayer corner plane used for the water surface. Controlled flat-water probes identify plane 1.")]
    [Range(0, 2)]
    public int SurfaceHeightPlane = 1;

    [Tooltip("HeightLayer corner plane used for moats. Controlled probes identify moats as ground-hugging overlays.")]
    [Range(0, 2)]
    public int MoatHeightPlane = 0;

    [Tooltip("Rotates all decoded flow directions. Code 0 is treated as +X; the original compass orientation is not known yet.")]
    public float FlowAngleOffsetDegrees;

    public bool FlipX;
    public bool FlipZ;

    [Min(0.0001f)]
    public float TextureScale = 1f;

    public Material RiverMaterial;
    public Material SeaMaterial;
    public Material PitchSwampMaterial;
    public Material MoatMaterial;
  }
}
