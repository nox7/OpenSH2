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

    [Tooltip("Vertical offset from the decoded river plane. A negative value seats water below the nearby terrain edge.")]
    public float RiverSurfaceOffset = -0.25f;

    [Tooltip("Vertical offset from the decoded sea plane. A negative value seats water below the nearby terrain edge.")]
    public float SeaSurfaceOffset = -0.25f;

    [Tooltip("Vertical offset from the decoded pitch/swamp plane. A negative value seats water below the nearby terrain edge.")]
    public float PitchSwampSurfaceOffset = -0.25f;

    [Tooltip("Vertical offset from the decoded moat plane. A negative value seats water below the nearby terrain edge.")]
    public float MoatSurfaceOffset = -0.0f;

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
    [Tooltip("Animated river-video repeats per S2M cell. Lower values stretch the video farther across the map.")]
    public float RiverTextureScale = 0.475f;

    [Min(0.0001f)]
    [Tooltip("Animated sea-video repeats per S2M cell. Lower values stretch the video farther across the map.")]
    public float SeaTextureScale = 0.415f;

    [Min(0.0001f)]
    [Tooltip("Static marsh-texture repeats per S2M cell.")]
    public float PitchSwampTextureScale = 0.5f;

    [Min(0.0001f)]
    [Tooltip("Animated moat-video repeats per S2M cell. Lower values stretch the video farther across the map.")]
    public float MoatTextureScale = 0.5f;

    [Tooltip("Root of the legally owned Stronghold 2 installation. When set, water videos and marsh textures are loaded from it.")]
    public string GameInstallPath;

    [Tooltip("Looping sea-surface Bink video relative to the game installation.")]
    public string SeaVideoRelativePath = "terrain/river/sea.bik";

    [Tooltip("Looping river-surface Bink video relative to the game installation.")]
    public string RiverVideoRelativePath = "terrain/river/river_full_.bik";

    [Tooltip("Looping moat-surface Bink video relative to the game installation.")]
    public string MoatVideoRelativePath = "terrain/river/moat.bik";

    [Tooltip("Static swamp surface texture relative to the game installation.")]
    public string MarshTextureRelativePath = "terrain/marsh.DDS";

    [Min(0f)]
    [Tooltip("River-video playback multiplier. Zero holds the first decoded frame.")]
    public float RiverPlaybackSpeed = 1.15f;

    [Min(0f)]
    [Tooltip("Sea-video playback multiplier. Zero holds the first decoded frame.")]
    public float SeaPlaybackSpeed = 1.15f;

    [Min(0f)]
    [Tooltip("Moat-video playback multiplier. Zero holds the first decoded frame.")]
    public float MoatPlaybackSpeed = 1f;

    [Tooltip("Multiplicative colour grade applied to animated river water.")]
    public Color RiverTint = new(0.65f, 0.85f, 1f, 1f);

    [Tooltip("Multiplicative colour grade applied to animated sea water.")]
    public Color SeaTint = new(0.58f, 0.75f, 1f, 1f);

    [Tooltip("Multiplicative colour grade applied to static pitch/swamp water.")]
    public Color PitchSwampTint = new(0.8f, 0.8f, 0.8f, 1f);

    [Tooltip("Multiplicative colour grade applied to animated moat water.")]
    public Color MoatTint = new(0.42f, 0.62f, 0.82f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Opacity of animated river water.")]
    public float RiverOpacity = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Opacity of animated sea water.")]
    public float SeaOpacity = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Opacity of animated moat water.")]
    public float MoatOpacity = 1.0f;

    [Min(0f)]
    [Tooltip("Diffuse light multiplier shared by generated water surfaces. Increase this if the source video appears too dark.")]
    public float SurfaceBrightness = 2.0f;

    [Min(0f)]
    [Tooltip("Strength of the view-dependent directional-light glint on animated water.")]
    public float SunGlintIntensity = 1.75f;

    [Range(4f, 128f)]
    [Tooltip("Higher values make animated-water highlights smaller and sharper.")]
    public float SunGlintSharpness = 24f;

    [Min(0f)]
    [Tooltip("How strongly brightness variation in the water video bends the simulated wave normal.")]
    public float WaveNormalStrength = 8f;

    public Material RiverMaterial;
    public Material SeaMaterial;
    public Material PitchSwampMaterial;
    public Material MoatMaterial;
  }
}
