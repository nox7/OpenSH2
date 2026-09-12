using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class S2MVegetationRenderSettings
  {
    [Min(0.0001f)] public float HorizontalCellSize = 1f;
    [Min(0.000001f)] public float HeightUnitScale = 1f / 1024f;
    public bool FlipX;
    public bool FlipZ;
    public bool ApplyAppearanceScale = true;
    [Tooltip("Uniform multiplier for every GR2 model placed by the S2M map. Does not change map coordinates.")]
    [Min(0.000001f)] public float AssetScaleMultiplier = 1f;
    public Granny2ModelRenderSettings Model = new();
  }
}
