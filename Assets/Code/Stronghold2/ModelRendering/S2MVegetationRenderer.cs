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
      string objectName = "S2M Vegetation")
    {
      if (vegetation == null) throw new ArgumentNullException(nameof(vegetation));
      settings ??= new S2MVegetationRenderSettings();
      var root = new GameObject(objectName);
      if (parent != null) root.transform.SetParent(parent, false);

      foreach (S2MVegetationBatch batch in vegetation.Batches)
      {
        var transforms = new List<Granny2ModelInstance>(batch.Instances.Count);
        for (int i = 0; i < batch.Instances.Count; i++)
        {
          ForestInstance source = batch.Instances[i];
          transforms.Add(ToUnityInstance(source, vegetation.Map, settings, i));
        }
        Granny2ModelRenderer.RenderInstances(
          batch.Model,
          transforms,
          root.transform,
          settings.Model,
          batch.AssetRelativePath.Replace('\\', '/'));
      }
      return root;
    }

    private static Granny2ModelInstance ToUnityInstance(
      ForestInstance source,
      S2MFile map,
      S2MVegetationRenderSettings settings,
      int index)
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
      var scale = new Vector3(source.ScaleX, source.ScaleY, source.ScaleX) * appearanceScale;
      return new Granny2ModelInstance(
        $"Tree {source.Family + 1} ({index})",
        new Vector3(x * settings.HorizontalCellSize, source.RawY * settings.HeightUnitScale, z * settings.HorizontalCellSize),
        new Vector3(0f, yaw, 0f),
        scale);
    }
  }
}
