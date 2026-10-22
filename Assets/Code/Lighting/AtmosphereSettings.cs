using System;
using UnityEngine;

namespace Assets.Code.Lighting
{
  [Serializable]
  public sealed class AtmosphereSettings
  {
    [Header("Sun path (06:00 sunrise, 18:00 sunset)")]
    [Range(0f, 360f)] public float SunAzimuth = 240f;
    [Min(0f)] public float SunIntensity = 2.5f;
    public Gradient SunColor = Colors(new Color(1f, 0.28f, 0.08f), new Color(1f, 0.95f, 0.82f));
    public AnimationCurve SunStrength = new AnimationCurve(
      new Keyframe(0f, 0f), new Keyframe(0.23f, 0f), new Keyframe(0.27f, 0.35f),
      new Keyframe(0.5f, 1f), new Keyframe(0.73f, 0.35f), new Keyframe(0.77f, 0f), new Keyframe(1f, 0f));

    [Header("Sky and ambient light")]
    public Gradient ZenithColor = Colors(new Color(0.015f, 0.025f, 0.07f), new Color(0.12f, 0.35f, 0.65f));
    public Gradient HorizonColor = Colors(new Color(0.04f, 0.05f, 0.1f), new Color(0.65f, 0.78f, 0.9f));
    public Color SunsetColor = new Color(1f, 0.3f, 0.07f);
    public Gradient AmbientColor = Colors(new Color(0.025f, 0.035f, 0.07f), new Color(0.5f, 0.6f, 0.75f));
    [Range(0f, 2f)] public float AmbientIntensity = 0.7f;
    [Range(0.1f, 5f)] public float SunDiscSize = 0.65f;

    [Header("Height fog (world units)")]
    public bool FogEnabled = true;
    public Gradient FogColor = Colors(new Color(0.035f, 0.045f, 0.08f), new Color(0.55f, 0.67f, 0.8f));
    [Min(0f)] public float FogDensity = 0.007f;
    public float FogBaseHeight = 2f;
    [Min(0.001f)] public float FogHeightFalloff = 0.12f;
    [Min(0f)] public float FogStartDistance = 8f;
    [Min(1f)] public float MaxDistance = 250f;
    [Range(0f, 1f)] public float MaxFogOpacity = 0.92f;

    [Header("Sun rays")]
    public bool SunRaysEnabled = true;
    [Range(0f, 5f)] public float SunRayIntensity = 1.2f;
    [Range(0f, 0.95f)] public float Anisotropy = 0.65f;
    [Range(8, 96)] public int RaySteps = 32;
    [Tooltip("Also apply the atmosphere in the Scene view while this component is enabled.")]
    public bool ShowInSceneView = true;

    // Gradients are keyed by daylight/elevation, so dawn and dusk share a palette.
    private static Gradient Colors(Color night, Color day) => new Gradient
    {
      colorKeys = new[] { new GradientColorKey(night, 0f), new GradientColorKey(day, 1f) },
      alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };
  }
}
