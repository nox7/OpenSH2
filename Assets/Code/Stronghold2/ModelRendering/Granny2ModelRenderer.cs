using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Renders already loaded OpenSH2 Granny model data without performing file I/O.</summary>
  public static class Granny2ModelRenderer
  {
    public static Bounds GetLocalBounds(Granny2ModelData model, float unitScale)
    {
      if (model == null) throw new ArgumentNullException(nameof(model));
      bool hasVertex = false;
      var bounds = new Bounds();
      foreach (Granny2MeshData mesh in model.Meshes)
      {
        for (int i = 0; i < mesh.VertexData.Length; i += 8)
        {
          var vertex = new Vector3(mesh.VertexData[i], mesh.VertexData[i + 2], mesh.VertexData[i + 1]) * unitScale;
          if (!hasVertex) { bounds = new Bounds(vertex, Vector3.zero); hasVertex = true; }
          else bounds.Encapsulate(vertex);
        }
      }
      return bounds;
    }

    public static GameObject Render(
      Granny2ModelData model,
      Transform parent = null,
      Granny2ModelRenderSettings settings = null,
      string objectName = null)
    {
      if (model == null) throw new ArgumentNullException(nameof(model));
      settings ??= new Granny2ModelRenderSettings();
      if (settings.UnitScale <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.UnitScale));

      var root = new GameObject(string.IsNullOrWhiteSpace(objectName)
        ? Path.GetFileNameWithoutExtension(model.SourcePath)
        : objectName);
      if (parent != null) root.transform.SetParent(parent, false);
      var resources = root.AddComponent<Granny2GeneratedModelResources>();
      List<PreparedMesh> meshes = PrepareMeshes(model, settings, resources);
      AddPreparedMeshes(meshes, root.transform);
      return root;
    }

    /// <summary>Creates many transforms while sharing one generated mesh/material set.</summary>
    public static GameObject RenderInstances(
      Granny2ModelData model,
      IReadOnlyList<Granny2ModelInstance> instances,
      Transform parent = null,
      Granny2ModelRenderSettings settings = null,
      string objectName = null)
    {
      if (model == null) throw new ArgumentNullException(nameof(model));
      if (instances == null) throw new ArgumentNullException(nameof(instances));
      settings ??= new Granny2ModelRenderSettings();
      if (settings.UnitScale <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.UnitScale));

      var root = new GameObject(string.IsNullOrWhiteSpace(objectName)
        ? $"{Path.GetFileNameWithoutExtension(model.SourcePath)} Instances"
        : objectName);
      if (parent != null) root.transform.SetParent(parent, false);
      var resources = root.AddComponent<Granny2GeneratedModelResources>();
      List<PreparedMesh> meshes = PrepareMeshes(model, settings, resources);

      var swayProperties = new MaterialPropertyBlock();
      int swayId = Shader.PropertyToID("_ModelSway");
      for (int i = 0; i < instances.Count; i++)
      {
        Granny2ModelInstance instance = instances[i];
        var instanceObject = new GameObject(string.IsNullOrWhiteSpace(instance.Name) ? $"Instance {i}" : instance.Name);
        instanceObject.transform.SetParent(root.transform, false);
        instanceObject.transform.localPosition = instance.Position;
        instanceObject.transform.localRotation = Quaternion.Euler(instance.EulerAngles);
        instanceObject.transform.localScale = instance.Scale;
        AddPreparedMeshes(meshes, instanceObject.transform);
        if (instance.Sway.y > 0f && settings.UseModelSway)
        {
          swayProperties.SetVector(swayId, instance.Sway);
          foreach (MeshRenderer renderer in instanceObject.GetComponentsInChildren<MeshRenderer>())
          {
            renderer.SetPropertyBlock(swayProperties);
            // The GPU rotates around the root; CPU culling must include that swept volume.
            Bounds bounds = renderer.localBounds;
            Vector3 size = renderer.transform.lossyScale;
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            float ratio = Mathf.Max(size.x, Mathf.Max(size.y, size.z))
              / Mathf.Max(0.000001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            Vector3 center = bounds.center;
            float radius = (new Vector3(Mathf.Abs(center.x), Mathf.Abs(center.y), Mathf.Abs(center.z)) + bounds.extents).magnitude;
            float displacement = 2f * radius * Mathf.Sin(instance.Sway.y * Mathf.Deg2Rad) * ratio;
            bounds.Expand(2f * displacement);
            renderer.localBounds = bounds;
          }
        }
      }
      return root;
    }

    private static PreparedMesh PrepareMesh(
      Granny2MeshData source,
      string sourcePath,
      Granny2ModelRenderSettings settings,
      Granny2GeneratedModelResources resources)
    {
      int vertexCount = source.VertexData.Length / 8;
      var positions = new Vector3[vertexCount];
      var normals = new Vector3[vertexCount];
      var uvs = new Vector2[vertexCount];
      var colors = new Color[vertexCount];
      for (int i = 0; i < vertexCount; i++)
      {
        int offset = i * 8;
        Vector3 position = new(source.VertexData[offset], source.VertexData[offset + 1], source.VertexData[offset + 2]);
        Vector3 normal = new(source.VertexData[offset + 3], source.VertexData[offset + 4], source.VertexData[offset + 5]);
        positions[i] = ConvertAxis(position, settings.ConvertZUpToUnityYUp) * settings.UnitScale;
        normals[i] = ConvertAxis(normal, settings.ConvertZUpToUnityYUp).normalized;
        uvs[i] = new Vector2(source.VertexData[offset + 6], source.VertexData[offset + 7]);
        // The original foliage shader multiplies COLOR0 into the texture. The
        // current PNT converter does not preserve that non-standard channel,
        // so use neutral white until its exact Granny layout is decoded.
        colors[i] = Color.white;
      }

      var mesh = new Mesh
      {
        name = source.Name,
        indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
      };
      mesh.vertices = positions;
      mesh.normals = normals;
      mesh.uv = uvs;
      mesh.colors = colors;
      mesh.subMeshCount = source.TriangleGroups.Count;
      for (int groupIndex = 0; groupIndex < source.TriangleGroups.Count; groupIndex++)
      {
        Granny2TriangleGroup group = source.TriangleGroups[groupIndex];
        int[] triangles = new int[group.TriangleCount * 3];
        Array.Copy(source.Indices, group.TriangleFirst * 3, triangles, 0, triangles.Length);
        if (settings.ConvertZUpToUnityYUp) ReverseWinding(triangles);
        mesh.SetTriangles(triangles, groupIndex, calculateBounds: false);
      }
      mesh.RecalculateBounds();

      var materials = new Material[source.TriangleGroups.Count];
      for (int i = 0; i < materials.Length; i++)
      {
        int materialIndex = source.TriangleGroups[i].MaterialIndex;
        Granny2MaterialData materialData = materialIndex < source.Materials.Count ? source.Materials[materialIndex] : null;
        materials[i] = CreateMaterial(materialData, sourcePath, settings, resources);
        if (settings.Livery != null) settings.Livery.Configure(materials[i], sourcePath, materialData?.TexturePath, i, settings.TeamGroup);
      }
      resources.Add(mesh);
      return new PreparedMesh
      {
        Name = string.IsNullOrWhiteSpace(source.Name) ? "GR2 Mesh" : source.Name,
        Mesh = mesh,
        Materials = materials
      };
    }

    private static List<PreparedMesh> PrepareMeshes(
      Granny2ModelData model,
      Granny2ModelRenderSettings settings,
      Granny2GeneratedModelResources resources)
    {
      var result = new List<PreparedMesh>(model.Meshes.Count);
      foreach (Granny2MeshData sourceMesh in model.Meshes)
        result.Add(PrepareMesh(sourceMesh, model.SourcePath, settings, resources));
      return result;
    }

    private static void AddPreparedMeshes(IReadOnlyList<PreparedMesh> meshes, Transform parent)
    {
      foreach (PreparedMesh prepared in meshes)
      {
        var child = new GameObject(prepared.Name);
        child.transform.SetParent(parent, false);
        child.AddComponent<MeshFilter>().sharedMesh = prepared.Mesh;
        child.AddComponent<MeshRenderer>().sharedMaterials = prepared.Materials;
      }
    }

    private static Material CreateMaterial(
      Granny2MaterialData data,
      string gr2Path,
      Granny2ModelRenderSettings settings,
      Granny2GeneratedModelResources resources)
    {
      bool isFoliage = settings.UseFoliageMaterials && IsFoliageMaterial(data);
      bool alphaTest = isFoliage || (data?.TexturePath?.IndexOf(".alphatest.", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
      Material material;
      if (settings.FallbackMaterial != null)
      {
        material = new Material(settings.FallbackMaterial);
      }
      else if (isFoliage || settings.UseModelSway)
      {
        Shader shader = Shader.Find("OpenSH2/Stronghold 2 Foliage");
        if (shader == null)
          throw new InvalidOperationException("The OpenSH2 foliage shader could not be found.");
        material = new Material(shader);
      }
      else
      {
        // Serialized Unity objects can have a managed wrapper but no native
        // object. ?? does not respect Unity's overloaded null comparison.
        Shader shader = settings.OpaqueShader;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No compatible model shader was found.");
        material = new Material(shader);
        ConfigureImportedOpaqueLighting(material);
      }
      material.name = string.IsNullOrWhiteSpace(data?.Name) ? "GR2 Material" : data.Name;
      material.enableInstancing = true;
      if (material.HasProperty("_Cull")) material.SetFloat("_Cull", alphaTest && settings.DoubleSided ? 0f : 2f);
      if (material.HasProperty("_TextureBrightness")) material.SetFloat("_TextureBrightness", settings.TextureBrightness);

      string texturePath = ResolveTexturePath(gr2Path, data?.TexturePath);
      if (texturePath != null)
      {
        Texture2D texture = resources.LoadSharedTexture(texturePath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
      }
      if (alphaTest)
      {
        ConfigureAlphaTest(material, settings.AlphaCutoff);
        if (settings.DoubleSided && material.HasProperty("_Cull"))
        {
          material.SetFloat("_Cull", (float)CullMode.Off);
          material.doubleSidedGI = true;
        }
      }
      else ConfigureOpaque(material);
      resources.Add(material);
      return material;
    }

    private static bool IsFoliageMaterial(Granny2MaterialData data)
    {
      string textureName = Path.GetFileNameWithoutExtension(data?.TexturePath ?? string.Empty);
      // Bark alpha is not an opacity mask. It must remain opaque, while the
      // remaining landscape-tree texture groups are alpha-tested foliage cards.
      return !textureName.StartsWith("bark", StringComparison.OrdinalIgnoreCase);
    }

    internal static void ConfigureImportedOpaqueLighting(Material material)
    {
      // GR2 imports currently supply diffuse textures, not PBR material data.
      // Lit defaults otherwise add a pale specular/reflection layer to bark and
      // masonry, particularly conspicuous in shadow and near sunset.
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
      if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
      if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
      if (material.HasProperty("_WorkflowMode"))
      {
        material.SetFloat("_WorkflowMode", 0f); // Specular workflow: zero reflectance.
        material.SetColor("_SpecColor", Color.black);
        material.EnableKeyword("_SPECULAR_SETUP");
      }
      if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
      if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
      if (material.HasProperty("_GlossyReflections")) material.SetFloat("_GlossyReflections", 0f);
      material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
      material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
      material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
      // Zero specular color matters as well: URP's reflections-off path can
      // still supply a fallback glossy environment color to its BRDF.
    }

    private static string ResolveTexturePath(string gr2Path, string embeddedPath)
    {
      if (string.IsNullOrWhiteSpace(embeddedPath)) return null;
      string normalized = embeddedPath.Replace('\\', '/');
      string fileName = Path.GetFileNameWithoutExtension(normalized) + ".dds";
      string sibling = Path.Combine(Path.GetDirectoryName(gr2Path) ?? string.Empty, fileName);

      // Open keep interiors reference e.g. meshes/units/models/knight.dds.
      // Rebase the exporter's obsolete drive path onto this model's installation.
      string sourcePath = Path.GetFullPath(gr2Path).Replace('\\', '/');
      int sourceMeshes = sourcePath.LastIndexOf("/meshes/", StringComparison.OrdinalIgnoreCase);
      int embeddedMeshes = normalized.IndexOf("/meshes/", StringComparison.OrdinalIgnoreCase);
      if (sourceMeshes >= 0 && embeddedMeshes >= 0)
      {
        string relative = Path.ChangeExtension(normalized.Substring(embeddedMeshes + 1), ".dds");
        foreach (string part in relative.Split('/'))
          if (part == "." || part == "..") return null;
        string rebased = Path.Combine(sourcePath.Substring(0, sourceMeshes), relative);
        if (File.Exists(rebased)) return rebased;
      }
      return File.Exists(sibling) ? sibling : null;
    }

    private static Vector3 ConvertAxis(Vector3 value, bool convert) => convert
      ? new Vector3(value.x, value.z, value.y)
      : value;

    private static void ReverseWinding(int[] triangles)
    {
      for (int i = 0; i < triangles.Length; i += 3)
        (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
    }

    private static void ConfigureAlphaTest(Material material, float cutoff)
    {
      if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", cutoff);
      if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
      material.EnableKeyword("_ALPHATEST_ON");
      material.renderQueue = (int)RenderQueue.AlphaTest;
    }

    private static void ConfigureOpaque(Material material)
    {
      if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
      material.DisableKeyword("_ALPHATEST_ON");
      material.renderQueue = (int)RenderQueue.Geometry;
    }

    private sealed class PreparedMesh
    {
      public string Name;
      public Mesh Mesh;
      public Material[] Materials;
    }
  }

  public readonly struct Granny2ModelInstance
  {
    public string Name { get; }
    public Vector3 Position { get; }
    public Vector3 EulerAngles { get; }
    public Vector3 Scale { get; }
    /// <summary>Phase (radians), amplitude (degrees), speed multiplier, axis direction (radians).</summary>
    public Vector4 Sway { get; }

    public Granny2ModelInstance(string name, Vector3 position, Vector3 eulerAngles, Vector3 scale, Vector4 sway = default)
    {
      Name = name;
      Position = position;
      EulerAngles = eulerAngles;
      Scale = scale;
      Sway = sway;
    }
  }

  [ExecuteAlways]
  internal sealed class Granny2GeneratedModelResources : MonoBehaviour
  {
    private readonly List<UnityEngine.Object> generated = new();
    private readonly List<string> sharedTextures = new();
    public void Add(UnityEngine.Object value) { if (value != null) generated.Add(value); }

    public Texture2D LoadSharedTexture(string path)
    {
      Texture2D texture = Granny2TextureCache.Acquire(path);
      sharedTextures.Add(path);
      return texture;
    }

    private void OnDestroy()
    {
      foreach (string path in sharedTextures) Granny2TextureCache.Release(path);
      foreach (UnityEngine.Object value in generated)
      {
        if (value == null) continue;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
      }
    }
  }
}
