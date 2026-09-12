using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Video;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Creates a water-surface mesh from an already loaded S2M map.</summary>
  public static class S2MWaterRenderer
  {
    public static GameObject Render(
      S2MFile map,
      Transform parent = null,
      S2MWaterSettings settings = null,
      string waterName = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (map.WaterLayer == null)
        throw new InvalidDataException("The loaded S2M map does not contain a decodable Landscape water layer.");

      settings ??= new S2MWaterSettings();
      Validate(map, settings);

      WaterLayer water = map.WaterLayer;
      HeightLayer heights = map.HeightLayer;
      var vertices = new List<Vector3>();
      var localTextureCoordinates = new List<Vector2>();
      var worldTextureCoordinates = new List<Vector2>();
      var flowVectors = new List<Vector2>();
      var cellData = new List<Color32>();
      var riverTriangles = new List<int>();
      var seaTriangles = new List<int>();
      var pitchSwampTriangles = new List<int>();
      var moatTriangles = new List<int>();

      for (int z = 0; z < water.Height; z++)
      {
        for (int x = 0; x < water.Width; x++)
        {
          S2MWaterType waterType = water.GetWaterType(x, z);
          if (!water.HasWaterLikeSurface(x, z)) continue;

          int cellIndex = z * water.Width + x;
          int heightPlane = waterType == S2MWaterType.Moat
            ? settings.MoatHeightPlane
            : settings.SurfaceHeightPlane;
          float surfaceOffset = GetSurfaceOffset(settings, waterType);
          float textureScale = GetTextureScale(settings, waterType);
          int vertexStart = vertices.Count;
          AddVertex(vertices, heights, settings, heightPlane, surfaceOffset, cellIndex, x, z, corner: 2);
          AddVertex(vertices, heights, settings, heightPlane, surfaceOffset, cellIndex, x + 1, z, corner: 3);
          AddVertex(vertices, heights, settings, heightPlane, surfaceOffset, cellIndex, x, z + 1, corner: 0);
          AddVertex(vertices, heights, settings, heightPlane, surfaceOffset, cellIndex, x + 1, z + 1, corner: 1);

          AddTextureCoordinates(localTextureCoordinates, worldTextureCoordinates, x, z, textureScale);

          float angle = (water.GetFlowAngleDegrees(x, z) + settings.FlowAngleOffsetDegrees) * Mathf.Deg2Rad;
          var flow = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
          if (settings.FlipX) flow.x = -flow.x;
          if (settings.FlipZ) flow.y = -flow.y;
          // S2M direction tags point opposite to the visible Stronghold 2 video flow.
          flow = -flow;
          for (int i = 0; i < 4; i++) flowVectors.Add(flow);

          byte rawWater = water.GetRawLandscapeFeatureValue(x, z);
          byte materialId = water.SurfaceMaterialIds[cellIndex];
          byte direction = water.GetFlowDirection(x, z);
          var encodedCell = new Color32(rawWater, materialId, direction, 255);
          for (int i = 0; i < 4; i++) cellData.Add(encodedCell);

          List<int> triangles = waterType switch
          {
            S2MWaterType.River => riverTriangles,
            S2MWaterType.Sea => seaTriangles,
            S2MWaterType.PitchSwamp => pitchSwampTriangles,
            S2MWaterType.Moat => moatTriangles,
            _ => throw new InvalidDataException($"Unexpected renderable landscape surface type {waterType}.")
          };
          AddCellTriangles(triangles, vertexStart, settings.FlipX ^ settings.FlipZ);
        }
      }

      var mesh = new Mesh
      {
        name = "S2M Water Mesh",
        indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
      };
      mesh.SetVertices(vertices);
      mesh.SetUVs(0, localTextureCoordinates);
      mesh.SetUVs(1, flowVectors);
      mesh.SetUVs(2, worldTextureCoordinates);
      mesh.SetColors(cellData);
      mesh.subMeshCount = 4;
      mesh.SetTriangles(riverTriangles, 0);
      mesh.SetTriangles(seaTriangles, 1);
      mesh.SetTriangles(pitchSwampTriangles, 2);
      mesh.SetTriangles(moatTriangles, 3);
      mesh.RecalculateNormals();
      mesh.RecalculateBounds();

      var waterObject = new GameObject(string.IsNullOrWhiteSpace(waterName) ? "S2M Water" : waterName);
      if (parent != null) waterObject.transform.SetParent(parent, worldPositionStays: false);
      waterObject.AddComponent<MeshFilter>().sharedMesh = mesh;
      MeshRenderer meshRenderer = waterObject.AddComponent<MeshRenderer>();

      var generatedMaterials = new List<Material>();
      var generatedTextures = new List<Texture2D>();
      Texture2D marshTexture = LoadGameTexture(
        settings.GameInstallPath,
        settings.MarshTextureRelativePath,
        generatedTextures) ?? Texture2D.whiteTexture;
      meshRenderer.sharedMaterials = new[]
      {
        GetMaterial(settings.RiverMaterial, "S2M River", settings.RiverTint, Texture2D.blackTexture, settings.RiverTextureScale, settings.RiverOpacity, flowProjectedVideo: true, settings: settings, generatedMaterials: generatedMaterials),
        GetMaterial(settings.SeaMaterial, "S2M Sea", settings.SeaTint, Texture2D.blackTexture, settings.SeaTextureScale, settings.SeaOpacity, flowProjectedVideo: true, settings: settings, generatedMaterials: generatedMaterials),
        GetMaterial(settings.PitchSwampMaterial, "S2M Pitch Swamp", settings.PitchSwampTint, marshTexture, settings.PitchSwampTextureScale, 1f, flowProjectedVideo: false, settings: settings, generatedMaterials: generatedMaterials),
        GetMaterial(settings.MoatMaterial, "S2M Moat", settings.MoatTint, Texture2D.blackTexture, settings.MoatTextureScale, settings.MoatOpacity, flowProjectedVideo: true, settings: settings, generatedMaterials: generatedMaterials)
      };

      waterObject.AddComponent<S2MGeneratedWaterResources>().Initialize(mesh, generatedMaterials, generatedTextures);
      var videoBindings = new List<S2MWaterVideoBinding>();
      AddVideoBinding(videoBindings, meshRenderer.sharedMaterials[0], settings.GameInstallPath, settings.RiverVideoRelativePath, settings.RiverPlaybackSpeed);
      AddVideoBinding(videoBindings, meshRenderer.sharedMaterials[1], settings.GameInstallPath, settings.SeaVideoRelativePath, settings.SeaPlaybackSpeed);
      AddVideoBinding(videoBindings, meshRenderer.sharedMaterials[3], settings.GameInstallPath, settings.MoatVideoRelativePath, settings.MoatPlaybackSpeed);
      if (videoBindings.Count > 0)
        waterObject.AddComponent<S2MWaterVideoPlayer>().Initialize(videoBindings);
      return waterObject;
    }

    private static void AddVertex(
      List<Vector3> vertices,
      HeightLayer heights,
      S2MWaterSettings settings,
      int heightPlane,
      float surfaceOffset,
      int cellIndex,
      int gridX,
      int gridZ,
      int corner)
    {
      float x = settings.FlipX ? heights.Width - gridX : gridX;
      float z = settings.FlipZ ? heights.Height - gridZ : gridZ;
      float y = heights.GetCornerHeight(heightPlane, cellIndex, corner)
        * settings.HeightUnitScale + surfaceOffset;
      vertices.Add(new Vector3(x * settings.HorizontalCellSize, y, z * settings.HorizontalCellSize));
    }

    private static void AddTextureCoordinates(
      List<Vector2> localCoordinates,
      List<Vector2> worldCoordinates,
      int x,
      int z,
      float scale)
    {
      localCoordinates.Add(new Vector2(0f, 0f));
      localCoordinates.Add(new Vector2(1f, 0f));
      localCoordinates.Add(new Vector2(0f, 1f));
      localCoordinates.Add(new Vector2(1f, 1f));
      worldCoordinates.Add(new Vector2(x, z) * scale);
      worldCoordinates.Add(new Vector2(x + 1, z) * scale);
      worldCoordinates.Add(new Vector2(x, z + 1) * scale);
      worldCoordinates.Add(new Vector2(x + 1, z + 1) * scale);
    }

    private static float GetSurfaceOffset(S2MWaterSettings settings, S2MWaterType waterType)
    {
      return waterType switch
      {
        S2MWaterType.River => settings.RiverSurfaceOffset,
        S2MWaterType.Sea => settings.SeaSurfaceOffset,
        S2MWaterType.PitchSwamp => settings.PitchSwampSurfaceOffset,
        S2MWaterType.Moat => settings.MoatSurfaceOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(waterType))
      };
    }

    private static float GetTextureScale(S2MWaterSettings settings, S2MWaterType waterType)
    {
      return waterType switch
      {
        S2MWaterType.River => settings.RiverTextureScale,
        S2MWaterType.Sea => settings.SeaTextureScale,
        S2MWaterType.PitchSwamp => settings.PitchSwampTextureScale,
        S2MWaterType.Moat => settings.MoatTextureScale,
        _ => throw new ArgumentOutOfRangeException(nameof(waterType))
      };
    }

    private static void AddCellTriangles(List<int> triangles, int start, bool reverseWinding)
    {
      if (!reverseWinding)
      {
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
      }
      else
      {
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
      }
    }

    private static Material GetMaterial(
      Material configuredMaterial,
      string name,
      Color color,
      Texture surfaceTexture,
      float textureScale,
      float opacity,
      bool flowProjectedVideo,
      S2MWaterSettings settings,
      List<Material> generatedMaterials)
    {
      if (configuredMaterial != null) return configuredMaterial;

      Shader shader = Shader.Find("OpenSH2/Stronghold 2 Water");
      if (shader == null) throw new InvalidOperationException("The OpenSH2 water shader was not found.");

      var material = new Material(shader) { name = name };
      material.SetColor("_BaseColor", color);
      material.SetTexture("_SurfaceTexture", surfaceTexture);
      material.SetFloat("_SurfaceTextureScale", textureScale);
      material.SetFloat("_Opacity", opacity);
      material.SetFloat("_UseFlowProjectedVideoUvs", flowProjectedVideo ? 1f : 0f);
      material.SetFloat("_UseWaveHighlights", flowProjectedVideo ? 1f : 0f);
      material.SetFloat("_SurfaceBrightness", settings.SurfaceBrightness);
      material.SetFloat("_SunGlintIntensity", settings.SunGlintIntensity);
      material.SetFloat("_SunGlintSharpness", settings.SunGlintSharpness);
      material.SetFloat("_WaveNormalStrength", settings.WaveNormalStrength);
      generatedMaterials.Add(material);
      return material;
    }

    private static void AddVideoBinding(
      List<S2MWaterVideoBinding> bindings,
      Material material,
      string gameInstallPath,
      string relativePath,
      float playbackSpeed)
    {
      if (material == null || !material.HasProperty("_SurfaceTexture")
        || string.IsNullOrWhiteSpace(gameInstallPath) || string.IsNullOrWhiteSpace(relativePath)) return;
      bindings.Add(new S2MWaterVideoBinding(
        material,
        GetCachedWaterVideoPath(gameInstallPath, relativePath),
        playbackSpeed));
    }

    private static string GetCachedWaterVideoPath(string gameInstallPath, string relativePath)
    {
      string sourcePath = ResolveGameAssetPath(gameInstallPath, relativePath);
      string cachePath = VideoFilePaths.GetCachedVideoPath(sourcePath);
      if (File.Exists(cachePath)) return cachePath;

      Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
      Converter.Convert(sourcePath, cachePath, Converter.OutputFormatMp4);
      if (!File.Exists(cachePath))
        throw new InvalidOperationException($"Failed to cache Stronghold 2 water video: {relativePath}");
      return cachePath;
    }

    private static Texture2D LoadGameTexture(string gameInstallPath, string relativePath, List<Texture2D> generatedTextures)
    {
      if (string.IsNullOrWhiteSpace(gameInstallPath) || string.IsNullOrWhiteSpace(relativePath)) return null;
      string directPath = ResolveGameAssetPath(gameInstallPath, relativePath);

      Texture2D texture = DdsTextureLoader.Load(directPath);
      generatedTextures.Add(texture);
      return texture;
    }

    private static string ResolveGameAssetPath(string gameInstallPath, string relativePath)
    {
      string directPath = Path.Combine(gameInstallPath, relativePath);
      if (File.Exists(directPath)) return directPath;

      string directory = Path.GetDirectoryName(directPath);
      string fileName = Path.GetFileName(directPath);
      if (!Directory.Exists(directory))
        throw new DirectoryNotFoundException($"Stronghold 2 water-asset directory was not found: {directory}");
      foreach (string candidate in Directory.EnumerateFiles(directory))
      {
        if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase)) return candidate;
      }
      throw new FileNotFoundException($"Stronghold 2 water asset was not found: {relativePath}", directPath);
    }

    private static void Validate(S2MFile map, S2MWaterSettings settings)
    {
      if (settings.HorizontalCellSize <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HorizontalCellSize));
      if (settings.HeightUnitScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HeightUnitScale));
      if (settings.RiverTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.RiverTextureScale));
      if (settings.SeaTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.SeaTextureScale));
      if (settings.PitchSwampTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.PitchSwampTextureScale));
      if (settings.MoatTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.MoatTextureScale));
      if (settings.RiverPlaybackSpeed < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.RiverPlaybackSpeed));
      if (settings.SeaPlaybackSpeed < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.SeaPlaybackSpeed));
      if (settings.MoatPlaybackSpeed < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.MoatPlaybackSpeed));
      if (settings.SurfaceBrightness < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.SurfaceBrightness));
      if (settings.SunGlintIntensity < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.SunGlintIntensity));
      if (settings.SunGlintSharpness < 4f)
        throw new ArgumentOutOfRangeException(nameof(settings.SunGlintSharpness));
      if (settings.WaveNormalStrength < 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.WaveNormalStrength));
      if (map.HeightLayer?.CornerHeightPlanes == null
        || settings.SurfaceHeightPlane < 0
        || settings.SurfaceHeightPlane >= map.HeightLayer.CornerHeightPlanes.Length)
        throw new InvalidDataException("The selected water-surface corner-height plane is unavailable.");
      if (settings.MoatHeightPlane < 0 || settings.MoatHeightPlane >= map.HeightLayer.CornerHeightPlanes.Length)
        throw new InvalidDataException("The selected moat corner-height plane is unavailable.");
      if (map.WaterLayer.Width != map.HeightLayer.Width || map.WaterLayer.Height != map.HeightLayer.Height)
        throw new InvalidDataException("Landscape and HeightLayer dimensions do not match.");
    }
  }

  internal readonly struct S2MWaterVideoBinding
  {
    public S2MWaterVideoBinding(Material material, string cachedVideoPath, float playbackSpeed)
    {
      Material = material ?? throw new ArgumentNullException(nameof(material));
      CachedVideoPath = cachedVideoPath ?? throw new ArgumentNullException(nameof(cachedVideoPath));
      PlaybackSpeed = playbackSpeed;
    }

    public Material Material { get; }
    public string CachedVideoPath { get; }
    public float PlaybackSpeed { get; }
  }

  /// <summary>Loops cached Bink-derived MP4 water videos and assigns their current frame to water materials.</summary>
  internal sealed class S2MWaterVideoPlayer : MonoBehaviour
  {
    private readonly List<VideoPlayer> players = new();

    public void Initialize(IReadOnlyList<S2MWaterVideoBinding> bindings)
    {
      if (bindings == null) throw new ArgumentNullException(nameof(bindings));
      foreach (S2MWaterVideoBinding binding in bindings)
      {
        if (!File.Exists(binding.CachedVideoPath))
          throw new FileNotFoundException("Cached Stronghold 2 water video was not found.", binding.CachedVideoPath);

        VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.waitForFirstFrame = true;
        player.skipOnDrop = false;
        // Decode continuously without external-time seeking. Game-time updates avoid
        // unscaled wall-clock catch-up after a loading stall.
        player.playbackSpeed = binding.PlaybackSpeed;
        player.timeReference = VideoTimeReference.Freerun;
        player.timeUpdateMode = VideoTimeUpdateMode.GameTime;
        player.source = VideoSource.Url;
        player.url = new Uri(binding.CachedVideoPath).AbsoluteUri;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.prepareCompleted += source => source.Play();
        player.frameReady += (source, _) =>
        {
          if (source.texture != null) binding.Material.SetTexture("_SurfaceTexture", source.texture);
        };
        player.sendFrameReadyEvents = true;
        player.Prepare();
        players.Add(player);
      }
    }

    private void OnDestroy()
    {
      foreach (VideoPlayer player in players)
      {
        if (player != null) player.Stop();
      }
    }
  }

  internal sealed class S2MGeneratedWaterResources : MonoBehaviour
  {
    private Mesh GeneratedMesh { get; set; }
    private IReadOnlyList<Material> GeneratedMaterials { get; set; }
    private IReadOnlyList<Texture2D> GeneratedTextures { get; set; }

    public void Initialize(Mesh mesh, IReadOnlyList<Material> materials, IReadOnlyList<Texture2D> textures)
    {
      GeneratedMesh = mesh;
      GeneratedMaterials = materials;
      GeneratedTextures = textures;
    }

    private void OnDestroy()
    {
      DestroyGenerated(GeneratedMesh);
      if (GeneratedMaterials != null)
        foreach (Material material in GeneratedMaterials) DestroyGenerated(material);
      if (GeneratedTextures != null)
        foreach (Texture2D texture in GeneratedTextures) DestroyGenerated(texture);
    }

    private static void DestroyGenerated(UnityEngine.Object generatedObject)
    {
      if (generatedObject == null) return;
      if (Application.isPlaying) Destroy(generatedObject);
      else DestroyImmediate(generatedObject);
    }
  }
}
