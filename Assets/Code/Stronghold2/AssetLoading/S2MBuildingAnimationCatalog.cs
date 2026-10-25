using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  [Serializable]
  public sealed class S2MBuildingAmbientLoop
  {
    public string ObjectType;
    public string ModelPath;
    [Tooltip("Leave empty to use the animation embedded in the model.")]
    public string AnimationPath;
    public bool Enabled = true;
    internal Granny2AnimationRequest Request => new(ModelPath, string.IsNullOrWhiteSpace(AnimationPath) ? ModelPath : AnimationPath);
  }

  [Serializable]
  public sealed class S2MBuildingAnimationSettings
  {
    public bool Enabled = true;
    [Range(1, 60)] public int SamplesPerSecond = 15;
    [Min(0)] public float PlaybackSpeed = 1;
    [Tooltip("Use the same per-building time offset for all its animated parts.")]
    public bool RandomizePhase;
    [Tooltip("Replace defaults by building type and model path, or add additional ambient parts. Reload the map after changing these definitions.")]
    public S2MBuildingAmbientLoop[] Overrides = Array.Empty<S2MBuildingAmbientLoop>();
    internal void Validate()
    {
      if (SamplesPerSecond < 1 || SamplesPerSecond > 60 || !(PlaybackSpeed >= 0) || float.IsInfinity(PlaybackSpeed))
        throw new ArgumentOutOfRangeException(nameof(S2MBuildingAnimationSettings));
    }
  }

  /// <summary>Only verified decorative loops belong here; production and interaction state is supplied later.</summary>
  public static class S2MBuildingAnimationCatalog
  {
    public static IReadOnlyList<S2MBuildingAmbientLoop> GetLoops(string type, S2MBuildingAnimationSettings settings)
    {
      var loops = new Dictionary<string, S2MBuildingAmbientLoop>(StringComparer.OrdinalIgnoreCase);
      if (type == "Mill")
      {
        Add("windmill_wings.GR2"); Add("windmill_shaft.GR2");
      }
      if (settings?.Overrides != null) foreach (var loop in settings.Overrides)
        if (loop != null && string.Equals(loop.ObjectType, type, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(loop.ModelPath))
          loops[loop.ModelPath.Replace('\\', '/')] = loop;
      var result = new List<S2MBuildingAmbientLoop>();
      if (settings == null || settings.Enabled) foreach (var loop in loops.Values) if (loop.Enabled) result.Add(loop);
      return result;

      void Add(string file)
      {
        string path = "meshes/buildings/" + file;
        loops[path] = new S2MBuildingAmbientLoop { ObjectType = type, ModelPath = path };
      }
    }
  }
}
