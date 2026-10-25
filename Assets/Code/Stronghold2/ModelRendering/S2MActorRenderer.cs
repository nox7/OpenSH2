using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class S2MActorRenderSettings
  {
    [Range(1, 60)] public int AnimationSamplesPerSecond = 15;
    [Min(.000001f)] public float ModelUnitScale = .01f;
    [Min(.0001f)] public float ModelScale = 1;
    [Min(1)] public int CellsPerBatch = 16;
    public bool Animate = true;
    public bool CastShadows = true;
    public bool ReceiveShadows = true;
    public bool RandomizeLoopPhase = true;
    public S2MActorOverride[] Overrides = Array.Empty<S2MActorOverride>();
    [Tooltip("Assign in builds so Unity retains the runtime actor shader.")]
    public Shader Shader;

    internal void Validate()
    {
      if (!(ModelUnitScale > 0) || float.IsInfinity(ModelUnitScale) || !(ModelScale > 0) || float.IsInfinity(ModelScale)
        || CellsPerBatch < 1 || AnimationSamplesPerSecond < 1 || AnimationSamplesPerSecond > 60)
        throw new ArgumentOutOfRangeException(nameof(S2MActorRenderSettings));
    }
  }

  /// <summary>One update for a population. Shared GPU poses, meshes and instanced draws; no per-actor GameObjects.</summary>
  public sealed class S2MActorRenderer : MonoBehaviour
  {
    private readonly List<DrawBatch> draws = new();
    private readonly Dictionary<int, List<(DrawBatch batch, int index)>> actors = new();
    private S2MActorRenderSettings settings;
    private Material templateMaterial;
    private float modelScaleMultiplier;
    private float clock;
    public int ActorCount => actors.Count;
    public int DrawBatchCount => draws.Count;

    public static S2MActorRenderer Render(S2MActorData data, Transform parent = null,
      S2MTerrainSettings terrain = null, S2MActorRenderSettings settings = null, float modelScaleMultiplier = 1f)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      if (!(modelScaleMultiplier > 0) || float.IsInfinity(modelScaleMultiplier)) throw new ArgumentOutOfRangeException(nameof(modelScaleMultiplier));
      settings ??= new S2MActorRenderSettings(); settings.Validate(); terrain ??= new S2MTerrainSettings();
      // A Resources material both supplies defaults and preserves instancing variants in player builds.
      Material material = settings.Shader == null ? Resources.Load<Material>("InstancedActor") : null;
      Shader shader = settings.Shader != null ? settings.Shader : material != null ? material.shader : UnityEngine.Shader.Find("OpenSH2/Instanced Actor");
      if (shader == null || !shader.isSupported || !SystemInfo.supportsInstancing || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
        throw new NotSupportedException("Actor rendering requires the instanced actor shader and RGBAFloat vertex textures.");
      var root = new GameObject("S2M Placed Actors"); root.transform.SetParent(parent, false);
      var renderer = root.AddComponent<S2MActorRenderer>(); renderer.settings = settings; renderer.templateMaterial = material;
      renderer.modelScaleMultiplier = modelScaleMultiplier;
      try
      {
        foreach (var batch in data.Batches) renderer.Prepare(batch, data.Map, terrain, shader);
        return renderer;
      }
      catch { if (Application.isPlaying) Destroy(root); else DestroyImmediate(root); throw; }
    }

    private void Prepare(S2MActorBatchData batch, S2MFile map, S2MTerrainSettings terrain, Shader shader)
    {
      var motion = batch.Motion;
      if (motion.Bones * 4 > SystemInfo.maxTextureSize || motion.Frames > SystemInfo.maxTextureSize)
        throw new NotSupportedException("Actor clip exceeds GPU texture limits; reduce animation sampling rate.");
      var template = Granny2ModelRenderer.Render(batch.Model, transform, new Granny2ModelRenderSettings
      { UnitScale = settings.ModelUnitScale, UseFoliageMaterials = false, OpaqueShader = shader, FallbackMaterial = templateMaterial }, "Shared " + batch.Asset.ModelPath);
      template.SetActive(false);
      var resources = template.GetComponent<Granny2GeneratedModelResources>();
      var texture = new Texture2D(motion.Bones * 4, motion.Frames, TextureFormat.RGBAFloat, false, true)
      { name = batch.Asset.Action, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
      resources.Add(texture);
      texture.SetPixelData(motion.Matrices, 0); texture.Apply(false, true);
      var filters = new List<MeshFilter>(template.GetComponentsInChildren<MeshFilter>(true));
      var boneIndices = new List<Vector4[]>(motion.BoneIndices);
      var boneWeights = new List<Vector4[]>(motion.BoneWeights);
      if (filters.Count != motion.BoneIndices.Count) throw new InvalidOperationException("Actor mesh/skin cache mismatch.");
      foreach (var attachment in batch.Attachments)
      {
        int bone = AttachmentBone(motion.BoneNames, attachment.point);
        if (!string.IsNullOrWhiteSpace(attachment.boneName))
        {
          bone = motion.BoneNames.FindIndex(n => string.Equals(n, attachment.boneName, StringComparison.OrdinalIgnoreCase));
          if (bone < 0) throw new InvalidOperationException("Actor attachment bone not found: " + attachment.boneName);
        }
        GameObject part = Granny2ModelRenderer.Render(attachment.model, template.transform,
          new Granny2ModelRenderSettings { UnitScale = settings.ModelUnitScale, UseFoliageMaterials = false, OpaqueShader = shader, FallbackMaterial = templateMaterial });
        foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
        {
          int count = filter.sharedMesh.vertexCount;
          var indices = new Vector4[count]; var weights = new Vector4[count];
          for (int v = 0; v < count; v++) { indices[v] = new Vector4(bone + motion.BoneNames.Count, 0, 0, 0); weights[v] = new Vector4(1, 0, 0, 0); }
          filters.Add(filter); boneIndices.Add(indices); boneWeights.Add(weights);
        }
      }
      for (int m = 0; m < filters.Count; m++)
      {
        Mesh mesh = filters[m].sharedMesh;
        if (mesh.vertexCount != boneIndices[m].Length) throw new InvalidOperationException("Actor vertex/skin cache mismatch.");
        mesh.SetUVs(1, new List<Vector4>(boneIndices[m]));
        mesh.SetUVs(2, new List<Vector4>(boneWeights[m]));
        // Derive bounds across every sampled pose, not merely the bind pose.
        mesh.bounds = AnimatedBounds(mesh.vertices, motion, boneIndices[m], boneWeights[m], settings.ModelUnitScale);
      }
      var materials = template.GetComponentsInChildren<MeshRenderer>(true);
      foreach (var r in materials) foreach (var material in r.sharedMaterials)
      {
        material.SetTexture("_ActorPoses", texture);
        material.SetVector("_ActorClip", new Vector4(motion.Bones, motion.Frames, motion.Duration, settings.ModelUnitScale));
      }
      var groups = new Dictionary<(int x, int z), List<int>>();
      for (int i = 0; i < batch.Placements.Count; i++)
      {
        var p = batch.Placements[i];
        var key = (Mathf.FloorToInt(p.RawX / 1024 / settings.CellsPerBatch), Mathf.FloorToInt(p.RawZ / 1024 / settings.CellsPerBatch));
        if (!groups.TryGetValue(key, out var indices)) groups[key] = indices = new List<int>();
        indices.Add(i);
      }
      Mesh[] sharedMeshes = filters.ConvertAll(f => f.sharedMesh).ToArray();
      Material[][] sharedMaterials = Array.ConvertAll(materials, r => r.sharedMaterials);
      foreach (var group in groups.Values)
        for (int start = 0; start < group.Count; start += 1023)
        {
          int count = Math.Min(1023, group.Count - start);
          var draw = new DrawBatch { Local = new Matrix4x4[count], World = new Matrix4x4[count], Playback = new Vector4[count],
            PartTransform = Matrix4x4.TRS(batch.Asset.LocalOffset, Quaternion.Euler(batch.Asset.LocalEulerAngles), Vector3.one),
            Meshes = sharedMeshes, Materials = sharedMaterials, Properties = new MaterialPropertyBlock() };
          for (int j = 0; j < count; j++)
          {
            int index = group[start + j]; var p = batch.Placements[index];
            float x = p.RawX / 1024, z = p.RawZ / 1024, yaw = p.HeadingDegrees;
            if (terrain.FlipX) { x = map.MapSize - x; yaw = 180 - yaw; }
            if (terrain.FlipZ) { z = map.MapSize - z; yaw = -yaw; }
            draw.Local[j] = Matrix4x4.TRS(new Vector3(x * terrain.HorizontalCellSize, p.RawY * terrain.HeightUnitScale, z * terrain.HorizontalCellSize),
              Quaternion.Euler(0, yaw, 0), Vector3.one * settings.ModelScale * modelScaleMultiplier * batch.Scales[index]) * draw.PartTransform;
            float phase = settings.RandomizeLoopPhase && p.AnimationLoops ? (unchecked((uint)p.Id * 2654435761u) & 65535) / 65536f * motion.Duration : 0;
            draw.Playback[j] = new Vector4(batch.Asset.HoldPose ? 0 : p.AnimationTime + phase, batch.Asset.HoldPose ? 0 : p.AnimationRate, p.AnimationLoops ? 1 : 0, 0);
            if (!actors.TryGetValue(p.Id, out var pieces)) actors[p.Id] = pieces = new List<(DrawBatch batch, int index)>();
            pieces.Add((draw, j));
          }
          draw.Properties.SetVectorArray("_ActorPlayback", draw.Playback);
          draws.Add(draw);
        }
    }

    /// <summary>Changes an existing placed actor without allocating a skeleton or a scene object. Position is local to the map root.</summary>
    public bool SetTransform(int id, Vector3 position, Quaternion rotation, Vector3 scale)
    {
      if (!actors.TryGetValue(id, out var actor)) return false;
      foreach (var piece in actor) piece.batch.Local[piece.index] = Matrix4x4.TRS(position, rotation, scale) * piece.batch.PartTransform;
      return true;
    }

    public bool SetPlayback(int id, float time, float rate = 1, bool loop = true)
    {
      if (float.IsNaN(time) || float.IsInfinity(time) || float.IsNaN(rate) || float.IsInfinity(rate)) throw new ArgumentOutOfRangeException();
      if (!actors.TryGetValue(id, out var actor)) return false;
      foreach (var piece in actor)
      {
        piece.batch.Playback[piece.index] = new Vector4(time - clock * rate, rate, loop ? 1 : 0, 0);
        piece.batch.PlaybackDirty = true;
      }
      return true;
    }

    private void LateUpdate()
    {
      if (settings == null) return;
      if (settings.Animate) clock += Time.deltaTime;
      Matrix4x4 root = transform.localToWorldMatrix;
      foreach (var draw in draws)
      {
        for (int i = 0; i < draw.Local.Length; i++) draw.World[i] = root * draw.Local[i];
        if (draw.PlaybackDirty)
        {
          draw.Properties.SetVectorArray("_ActorPlayback", draw.Playback);
          draw.PlaybackDirty = false;
        }
        draw.Properties.SetFloat("_ActorClock", clock);
        draw.Properties.SetFloat("_ActorReceiveShadows", settings.ReceiveShadows ? 1 : 0);
        for (int m = 0; m < draw.Meshes.Length; m++)
        {
          Mesh mesh = draw.Meshes[m]; Material[] materials = draw.Materials[m];
          for (int s = 0; s < mesh.subMeshCount; s++)
            Graphics.DrawMeshInstanced(mesh, s, materials[s], draw.World, draw.World.Length, draw.Properties,
              settings.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, settings.ReceiveShadows, gameObject.layer,
              null, LightProbeUsage.Off);
        }
      }
    }

    private static Bounds AnimatedBounds(Vector3[] vertices, Granny2ActorMotion motion, Vector4[] indices, Vector4[] weights, float unitScale)
    {
      var bounds = new Bounds(); bool initialized = false;
      for (int frame = 0; frame < motion.Frames; frame++)
        for (int v = 0; v < vertices.Length; v++)
        {
          Vector3 p = new Vector3(vertices[v].x, vertices[v].z, vertices[v].y) / unitScale, posed = Vector3.zero;
          for (int w = 0; w < 4; w++)
          {
            float weight = weights[v][w]; if (weight == 0) continue;
            int b = (frame * motion.Bones + (int)indices[v][w]) * 16;
            float[] a = motion.Matrices;
            posed += weight * new Vector3(p.x * a[b] + p.y * a[b + 4] + p.z * a[b + 8] + a[b + 12],
              p.x * a[b + 1] + p.y * a[b + 5] + p.z * a[b + 9] + a[b + 13],
              p.x * a[b + 2] + p.y * a[b + 6] + p.z * a[b + 10] + a[b + 14]);
          }
          posed = new Vector3(posed.x, posed.z, posed.y) * unitScale;
          if (!initialized) { bounds = new Bounds(posed, Vector3.zero); initialized = true; } else bounds.Encapsulate(posed);
        }
      bounds.Expand(.05f); return bounds;
    }

    private static readonly string[] AttachmentPoints = {
      "Bip01 HeadNub", "Bip01 Head", "Bip01 Neck", "Bip01 Spine", "Bip01 Spine1", "Bip01 Pelvis",
      "Bip01 L Clavicle", "Bip01 L UpperArm", "Bip01 L ForeArm", "Bip01 L Hand", "Bip01 L Finger0",
      "Bip01 L Thigh", "Bip01 L Calf", "Bip01 L Foot", "Bip01 L Toe0", "Bip01 R Clavicle",
      "Bip01 R UpperArm", "Bip01 R ForeArm", "Bip01 R Hand", "Bip01 R Finger0", "Bip01 R Thigh",
      "Bip01 R Calf", "Bip01 R Foot", "Bip01 R Toe0", "Bip01 Prop1", "Bip01 Prop2", "Bip01 Prop3", "Bip01 Ponytail1"
    };
    private static int AttachmentBone(IReadOnlyList<string> names, int point)
    {
      if (point >= 0 && point < AttachmentPoints.Length)
        for (int i = 0; i < names.Count; i++) if (string.Equals(names[i], AttachmentPoints[point], StringComparison.OrdinalIgnoreCase)) return i;
      return 0;
    }

    private sealed class DrawBatch
    {
      public Matrix4x4[] Local, World;
      public Matrix4x4 PartTransform;
      public Vector4[] Playback;
      public bool PlaybackDirty;
      public Mesh[] Meshes;
      public Material[][] Materials;
      public MaterialPropertyBlock Properties;
    }
  }
}
