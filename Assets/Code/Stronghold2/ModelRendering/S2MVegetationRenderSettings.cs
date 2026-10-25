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
    [Tooltip("Whole-model GPU sway for trees and bushes. Settings are applied when the map loads.")]
    public S2MTreeSwaySettings TreeSway = new();
    [Tooltip("Size of grass cards in map units; independent of the tree model calibration.")]
    [Min(0.0001f)] public float GrassSize = 1f;
    [Range(0f, 1f)] public float GrassAlphaCutoff = 0.35f;
  }

  [Serializable]
  public sealed class S2MTreeSwaySettings
  {
    public bool Enabled = true;
    [Tooltip("Sway amplitude multiplier. 1 uses the original renderer's 2–4 degree tree range.")]
    [Range(0f, 5f)] public float Strength = 1f;
    [Tooltip("Cycle speed multiplier. 1 gives a four-second cycle; 0 freezes the motion.")]
    [Range(0f, 5f)] public float Speed = 1f;

    internal void Validate()
    {
      if (!(Strength >= 0f && Strength <= 5f) || !(Speed >= 0f && Speed <= 5f))
        throw new ArgumentOutOfRangeException(nameof(S2MTreeSwaySettings), "Sway strength and speed must be finite and between 0 and 5.");
    }
  }
}
