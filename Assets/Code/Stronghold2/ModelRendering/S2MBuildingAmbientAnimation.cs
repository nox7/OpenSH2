using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>One clock and shared pose texture per building part batch, with optional per-building pause.</summary>
  public sealed class S2MBuildingAmbientAnimation : MonoBehaviour
  {
    private sealed class Instance
    {
      public MeshRenderer[] Renderers;
      public float Offset;
      public bool Playing = true;
    }
    private readonly Dictionary<int, Instance> instances = new();
    private MaterialPropertyBlock properties;
    private Material[] materials;
    private S2MBuildingAnimationSettings settings;
    public float AnimationTime { get; private set; }
    public int InstanceCount => instances.Count;

    internal static GameObject Render(Granny2ModelData model, Granny2ActorMotion motion,
      IReadOnlyList<Granny2ModelInstance> placements, IReadOnlyList<BuildingPlacement> buildings,
      Transform parent, Granny2ModelRenderSettings modelSettings, S2MBuildingAnimationSettings settings)
    {
      settings.Validate();
      if (!SystemInfo.supportsInstancing || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
        throw new NotSupportedException("Building animation requires instancing and RGBAFloat vertex textures.");
      Material template = Resources.Load<Material>("InstancedActor");
      if (template == null || !template.shader.isSupported) throw new NotSupportedException("Animated model shader is unavailable.");
      var root = Granny2ModelRenderer.RenderInstances(model, placements, parent, new Granny2ModelRenderSettings
      { UnitScale = modelSettings.UnitScale, UseFoliageMaterials = false, FallbackMaterial = template });
      try
      {
        var resources = root.GetComponent<Granny2GeneratedModelResources>();
        var texture = Granny2AnimationRendering.CreatePoseTexture(motion, resources, model.SourcePath);
        Transform prototype = root.transform.GetChild(0);
        var filters = prototype.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != motion.BoneIndices.Count) throw new InvalidOperationException("Building animation mesh/skin mismatch.");
        for (int i = 0; i < filters.Length; i++)
          Granny2AnimationRendering.ApplySkin(filters[i].sharedMesh, motion, motion.BoneIndices[i], motion.BoneWeights[i], modelSettings.UnitScale);
        var unique = new HashSet<Material>();
        foreach (var r in prototype.GetComponentsInChildren<MeshRenderer>(true)) foreach (var material in r.sharedMaterials)
          if (unique.Add(material))
          {
            Granny2AnimationRendering.ConfigureMaterial(material, motion, texture, modelSettings.UnitScale);
            material.SetFloat("_ActorReceiveShadows", 1);
          }
        var clock = root.AddComponent<S2MBuildingAmbientAnimation>();
        clock.properties = new MaterialPropertyBlock();
        clock.settings = settings; clock.materials = new Material[unique.Count]; unique.CopyTo(clock.materials);
        for (int i = 0; i < buildings.Count; i++)
        {
          var instance = new Instance { Renderers = root.transform.GetChild(i).GetComponentsInChildren<MeshRenderer>(true),
            Offset = settings.RandomizePhase ? (unchecked((uint)buildings[i].Id * 2654435761u) & 65535) / 65536f : 0 };
          clock.instances.Add(buildings[i].Id, instance); clock.Apply(instance);
        }
        clock.SetTime(0);
        return root;
      }
      catch { if (Application.isPlaying) Destroy(root); else DestroyImmediate(root); throw; }
    }

    public bool SetPlaying(int buildingId, bool playing)
    {
      if (!instances.TryGetValue(buildingId, out var instance)) return false;
      if (instance.Playing == playing) return true;
      instance.Offset += playing ? -AnimationTime : AnimationTime;
      instance.Playing = playing; Apply(instance); return true;
    }

    private void Apply(Instance instance)
    {
      foreach (var renderer in instance.Renderers)
      {
        renderer.GetPropertyBlock(properties);
        properties.SetVector("_ActorPlayback", new Vector4(instance.Offset, instance.Playing ? 1 : 0, 1, 0));
        renderer.SetPropertyBlock(properties);
      }
    }

    public void SetTime(float seconds)
    {
      if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
      AnimationTime = seconds;
      foreach (var material in materials) material.SetFloat("_ActorClock", AnimationTime);
    }

    public void Advance(float seconds)
    {
      if (!(seconds >= 0) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
      if (settings.Enabled) SetTime(AnimationTime + seconds * settings.PlaybackSpeed);
    }
    private void LateUpdate() { if (settings != null) Advance(Time.deltaTime); }
  }
}
