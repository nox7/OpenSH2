using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Renders already loaded vegetation data without reading map or GR2 files.</summary>
  public static class S2MVegetationRenderer
  {
    public static GameObject Render(
      S2MVegetationData vegetation,
      Transform parent = null,
      S2MVegetationRenderSettings settings = null,
      string objectName = "S2M Vegetation",
      float treeModelScale = 1f,
      S2MSceneryModelScales modelScales = null,
      float naturalStoneTextureBrightness = 1f)
    {
      if (vegetation == null) throw new ArgumentNullException(nameof(vegetation));
      if (!(treeModelScale > 0f) || float.IsInfinity(treeModelScale))
        throw new ArgumentOutOfRangeException(nameof(treeModelScale), "Tree model scale must be finite and positive.");
      settings ??= new S2MVegetationRenderSettings();
      settings.TreeSway?.Validate();
      modelScales ??= new S2MSceneryModelScales();
      modelScales.Validate();
      if (!(naturalStoneTextureBrightness >= 0f) || float.IsInfinity(naturalStoneTextureBrightness))
        throw new ArgumentOutOfRangeException(nameof(naturalStoneTextureBrightness), "Texture brightness must be finite and nonnegative.");
      if (settings.AssetScaleMultiplier <= 0f)
      {
        throw new ArgumentOutOfRangeException(
          nameof(settings),
          "AssetScaleMultiplier must be greater than zero.");
      }
      var root = new GameObject(objectName);
      if (parent != null) root.transform.SetParent(parent, false);
      S2MNaturalStoneVisibility stoneVisibility = null;

      foreach (S2MVegetationBatch batch in vegetation.Batches)
      {
        var transforms = new List<Granny2ModelInstance>(batch.Instances.Count);
        for (int i = 0; i < batch.Instances.Count; i++)
        {
          SceneryInstance source = batch.Instances[i];
          transforms.Add(ToUnityInstance(source, vegetation.Map, settings, i, treeModelScale, modelScales));
        }
        bool rocks = batch.Instances.Count > 0 && (batch.Instances[0].Kind == SceneryKind.Rocks
          || batch.Instances[0].Kind == SceneryKind.NaturalStone);
        Granny2ModelRenderSettings modelSettings = rocks ? new Granny2ModelRenderSettings
        {
          UnitScale = settings.Model.UnitScale, ConvertZUpToUnityYUp = settings.Model.ConvertZUpToUnityYUp,
          DoubleSided = settings.Model.DoubleSided, UseFoliageMaterials = false,
          OpaqueShader = settings.Model.OpaqueShader != null ? settings.Model.OpaqueShader : (batch.Instances[0].Kind == SceneryKind.NaturalStone
            ? Shader.Find("OpenSH2/Stronghold 2 Natural Stone") : null),
          FallbackMaterial = settings.Model.FallbackMaterial,
          TextureBrightness = batch.Instances[0].Kind == SceneryKind.NaturalStone
            ? naturalStoneTextureBrightness : settings.Model.TextureBrightness
        } : new Granny2ModelRenderSettings
        {
          UnitScale = settings.Model.UnitScale, ConvertZUpToUnityYUp = settings.Model.ConvertZUpToUnityYUp,
          DoubleSided = settings.Model.DoubleSided, UseFoliageMaterials = settings.Model.UseFoliageMaterials,
          UseModelSway = transforms.Exists(p => p.Sway.y > 0f),
          OpaqueShader = settings.Model.OpaqueShader, FallbackMaterial = settings.Model.FallbackMaterial,
          TextureBrightness = settings.Model.TextureBrightness, AlphaCutoff = settings.Model.AlphaCutoff
        };
        if (rocks && batch.Instances[0].Kind == SceneryKind.NaturalStone
          && modelSettings.OpaqueShader == null && modelSettings.FallbackMaterial == null)
          throw new InvalidOperationException("Stronghold 2 natural stone shader is missing.");
        GameObject renderedBatch = Granny2ModelRenderer.RenderInstances(
          batch.Model,
          transforms,
          root.transform,
          modelSettings,
          batch.AssetRelativePath.Replace('\\', '/'));
        if (vegetation.Map != null)
          for (int i = 0; i < batch.Instances.Count; i++)
            if (batch.Instances[i].Kind == SceneryKind.NaturalStone)
            {
              if (stoneVisibility == null)
              {
                stoneVisibility = root.AddComponent<S2MNaturalStoneVisibility>();
                stoneVisibility.Initialize(S2MSceneryOcclusion.GetOrCreate(parent != null ? parent : root.transform, vegetation.Map));
              }
              stoneVisibility.Register(batch.Instances[i], renderedBatch.transform.GetChild(i).gameObject);
            }
      }
      S2MGrassRenderer.Render(vegetation, root.transform, settings, modelScales.Grass);
      S2MWheatRenderer.Render(vegetation, root.transform, settings, modelScales.Crops);
      return root;
    }

    private static Granny2ModelInstance ToUnityInstance(
      SceneryInstance source,
      S2MFile map,
      S2MVegetationRenderSettings settings,
      int index,
      float treeModelScale,
      S2MSceneryModelScales modelScales)
    {
      float x = source.MapX;
      float z = source.MapZ;
      float yaw = source.RotationDegrees;
      int width = map?.HeightLayer?.Width ?? map?.MapSize ?? 0;
      int height = map?.HeightLayer?.Height ?? map?.MapSize ?? 0;
      if (settings.FlipX && width > 0) { x = width - x; yaw = 180f - yaw; }
      if (settings.FlipZ && height > 0) { z = height - z; yaw = -yaw; }

      float appearanceScale = settings.ApplyAppearanceScale && source.AppearanceScale > 0f
        ? source.AppearanceScale
        : 1f;
      var scale = new Vector3(source.ScaleX, source.ScaleY, source.ScaleX)
        * appearanceScale
        * settings.AssetScaleMultiplier
        * modelScales.For(source.Kind)
        * (source.Kind == SceneryKind.Forest || source.Kind == SceneryKind.AppleTree ? treeModelScale : 1f);
      float rawY = source.SampleTerrainHeight ? SampleTerrainHeight(map, source.MapX, source.MapZ) : source.RawY;
      return new Granny2ModelInstance(
        $"{source.Kind} Family {source.Family} Variant {source.Variant} ({index})",
        new Vector3(x * settings.HorizontalCellSize, rawY * settings.HeightUnitScale, z * settings.HorizontalCellSize),
        new Vector3(0f, yaw, 0f),
        scale,
        GetSway(source, settings.TreeSway));
    }

    private static Vector4 GetSway(SceneryInstance source, S2MTreeSwaySettings settings)
    {
      if (settings == null || !settings.Enabled || settings.Strength == 0f
        || (source.Kind != SceneryKind.Forest && source.Kind != SceneryKind.Shrubs)) return Vector4.zero;
      // Position-based avalanche hash: stable across loads and independent of batch order.
      uint seed = unchecked((uint)Mathf.RoundToInt(source.RawX) * 73856093u
        ^ (uint)Mathf.RoundToInt(source.RawZ) * 19349663u ^ (uint)source.Family * 83492791u);
      seed ^= seed >> 16; seed *= 0x7feb352du; seed ^= seed >> 15; seed *= 0x846ca68bu; seed ^= seed >> 16;
      float phase = (seed & 65535u) * (Mathf.PI * 2f / 65536f);
      float direction = (seed >> 16) * (Mathf.PI * 2f / 65536f);
      float amplitude = source.Kind == SceneryKind.Shrubs || (source.Family >= 2 && source.Family <= 6)
        || source.Family == 13 ? 1f : 2f;
      return new Vector4(phase, amplitude * settings.Strength, settings.Speed, direction);
    }

    internal static float SampleTerrainHeight(S2MFile map, float x, float z)
    {
      HeightLayer layer = map?.HeightLayer;
      if (layer?.Heights == null) throw new InvalidOperationException("Scenery requires loaded terrain heights.");
      int cx = Mathf.Clamp(Mathf.FloorToInt(x), 0, layer.Width - 1);
      int cz = Mathf.Clamp(Mathf.FloorToInt(z), 0, layer.Height - 1);
      float tx = Mathf.Clamp01(x - cx), tz = Mathf.Clamp01(z - cz);
      // Sample the ground grid, not elevated building/bridge walking surfaces.
      float h00 = layer.Heights[cz * layer.Width + cx];
      float h10 = layer.Heights[cz * layer.Width + Math.Min(cx + 1, layer.Width - 1)];
      float h01 = layer.Heights[Math.Min(cz + 1, layer.Height - 1) * layer.Width + cx];
      float h11 = layer.Heights[Math.Min(cz + 1, layer.Height - 1) * layer.Width + Math.Min(cx + 1, layer.Width - 1)];
      return tx + tz <= 1f ? h00 + (h10 - h00) * tx + (h01 - h00) * tz
        : h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
    }
  }
}
