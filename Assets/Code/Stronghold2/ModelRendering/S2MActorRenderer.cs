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
    [Tooltip("Standalone-rendering fallback when there is no map livery context. Map ownership uses Main's Livery Settings.")]
    public Texture2D CrestTexture;
    public Color CrestColor = new(.6f, .6f, .65f, 1);
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
    private S2MLiveryContext livery;
    public Assets.Code.Simulation.SimulationClock Clock { get; set; }
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
      renderer.livery = S2MLiveryContext.Find(parent);
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
      var template = Granny2ModelRenderer.Render(batch.Model, transform, new Granny2ModelRenderSettings
      { UnitScale = settings.ModelUnitScale, UseFoliageMaterials = false, OpaqueShader = shader, FallbackMaterial = templateMaterial,
        Livery = livery, TeamGroup = batch.Asset.TeamGroup }, "Shared " + batch.Asset.ModelPath);
      template.SetActive(false);
      var resources = template.GetComponent<Granny2GeneratedModelResources>();
      var texture = Granny2AnimationRendering.CreatePoseTexture(motion, resources, batch.Asset.Action);
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
          new Granny2ModelRenderSettings { UnitScale = settings.ModelUnitScale, UseFoliageMaterials = false, OpaqueShader = shader, FallbackMaterial = templateMaterial, Livery = livery });
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
        Granny2AnimationRendering.ApplySkin(mesh, motion, boneIndices[m], boneWeights[m], settings.ModelUnitScale);
      }
      var materials = template.GetComponentsInChildren<MeshRenderer>(true);
      foreach (var r in materials) foreach (var material in r.sharedMaterials)
      {
        if (livery == null && material.GetTexture("_BaseMap") is Texture2D diffuse
          && string.Equals(diffuse.name, "crest", StringComparison.OrdinalIgnoreCase))
        {
          // crest.dds is exporter placeholder text, not the player's heraldry.
          material.SetTexture("_BaseMap", settings.CrestTexture != null ? settings.CrestTexture : Texture2D.whiteTexture);
          material.SetColor("_BaseColor", settings.CrestColor);
        }
        Granny2AnimationRendering.ConfigureMaterial(material, motion, texture, settings.ModelUnitScale);
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
      Vector3 spinPivot=batch.Asset.SpinAxis.sqrMagnitude>0?Granny2ModelRenderer.GetLocalBounds(batch.Model, settings.ModelUnitScale).center:Vector3.zero;
      foreach (var group in groups.Values)
        // Match shader forcemaxcount; transforms plus both instanced vectors fit within 64 KiB.
        for (int start = 0; start < group.Count; start += 256)
        {
          int count = Math.Min(256, group.Count - start);
          var draw = new DrawBatch { Local = new Matrix4x4[count], World = new Matrix4x4[count], Playback = new Vector4[count], VisiblePlayback = new Vector4[count],
            Livery = new Vector4[count], VisibleLivery = new Vector4[count],
            Visible = new bool[count], AnimationPath = batch.Asset.AnimationPath, Variant = batch.Asset.Variant, HoldPose = batch.Asset.HoldPose,
            PartTransform = Matrix4x4.TRS(batch.Asset.LocalOffset, Quaternion.Euler(batch.Asset.LocalEulerAngles), Vector3.one),
            SpinAxis = batch.Asset.SpinAxis, SpinPivot = spinPivot,
            Meshes = sharedMeshes, Materials = sharedMaterials, Properties = new MaterialPropertyBlock() };
          for (int j = 0; j < count; j++)
          {
            int index = group[start + j]; var p = batch.Placements[index];
            draw.Visible[j] = true;
            draw.Livery[j] = livery != null ? livery.ResolveActor(p).Data : new Vector4(1, 1, 1, 13);
            float x = p.RawX / 1024, z = p.RawZ / 1024, yaw = p.HeadingDegrees;
            if (terrain.FlipX) { x = map.MapSize - x; yaw = 180 - yaw; }
            if (terrain.FlipZ) { z = map.MapSize - z; yaw = -yaw; }
            draw.Local[j] = Matrix4x4.TRS(new Vector3(x * terrain.HorizontalCellSize, p.RawY * terrain.HeightUnitScale, z * terrain.HorizontalCellSize),
              Quaternion.Euler(0, yaw, 0), Vector3.one * settings.ModelScale * modelScaleMultiplier * batch.Scales[index]) * draw.PartTransform;
            float phase = settings.RandomizeLoopPhase && p.AnimationLoops ? (unchecked((uint)p.Id * 2654435761u) & 65535) / 65536f * (batch.Asset.SynchronizedPlayback ? 1f : motion.Duration) : 0;
            draw.Playback[j] = new Vector4(batch.Asset.HoldPose ? 0 : p.AnimationTime + phase, batch.Asset.HoldPose ? 0 : p.AnimationRate, p.AnimationLoops ? 1 : 0, 0);
            if (!actors.TryGetValue(p.Id, out var pieces)) actors[p.Id] = pieces = new List<(DrawBatch batch, int index)>();
            pieces.Add((draw, j));
          }
          draw.Properties.SetVectorArray("_ActorPlayback", draw.Playback);
          draws.Add(draw);
        }
    }

    /// <summary>Changes an existing placed actor without allocating a skeleton or a scene object. Position is local to the map root.</summary>
    public bool SetTransform(int id, Vector3 position, Quaternion rotation, Vector3 scale, float motionDegrees = 0)
    {
      if (!actors.TryGetValue(id, out var actor)) return false;
      foreach (var piece in actor)
      {
        var draw=piece.batch;var part=draw.PartTransform;
        if(draw.SpinAxis.sqrMagnitude>0 && motionDegrees!=0)
          part*=Matrix4x4.TRS(draw.SpinPivot,Quaternion.AngleAxis(motionDegrees,draw.SpinAxis),Vector3.one)*Matrix4x4.Translate(-draw.SpinPivot);
        draw.Local[piece.index] = Matrix4x4.TRS(position, rotation, scale) * part;
      }
      return true;
    }

    /// <summary>Updates every loaded rider, horse, equipment and clip variant without allocating materials.</summary>
    public bool SetLivery(int id, S2MLivery value)
    {
      if (!actors.TryGetValue(id, out var pieces)) return false;
      foreach (var piece in pieces) piece.batch.Livery[piece.index] = value.Data;
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

    public bool HideActor(int id)
    {
      if (!actors.TryGetValue(id, out var pieces)) return false;
      foreach (var piece in pieces) piece.batch.Visible[piece.index] = false;
      return true;
    }

    /// <summary>Selects one preloaded clip variant for an actor without allocating scene objects.</summary>
    public bool SetAnimation(int id, string animationPath, float time = 0, bool loop = true, float rate = 1)
      => SelectAnimation(id,animationPath,false,time,loop,rate);

    public bool SetVariant(int id,string variant,float time=0,bool loop=true,float rate=1)
      => SelectAnimation(id,variant,true,time,loop,rate);

    private bool SelectAnimation(int id,string selection,bool variant,float time,bool loop,float rate)
    {
      if (!actors.TryGetValue(id, out var pieces)) return false;
      if (float.IsNaN(time) || float.IsInfinity(time) || float.IsNaN(rate) || float.IsInfinity(rate)) throw new ArgumentOutOfRangeException();
      bool Matches(DrawBatch batch) => string.Equals(variant ? batch.Variant : batch.AnimationPath,selection,StringComparison.OrdinalIgnoreCase);
      bool found = pieces.Exists(p => Matches(p.batch));
      if (!found) return false;
      float frameClock = clock + (settings.Animate ? Clock?.DeltaTime ?? Time.deltaTime : 0);
      foreach (var piece in pieces)
      {
        bool active = Matches(piece.batch);
        piece.batch.Visible[piece.index] = active;
        if (!active) continue;
        piece.batch.Playback[piece.index] = piece.batch.HoldPose ? Vector4.zero : new Vector4(time - frameClock * rate, rate, loop ? 1 : 0, 0);
        piece.batch.PlaybackDirty = true;
      }
      return true;
    }

    private void LateUpdate()
    {
      if (settings == null) return;
      if (settings.Animate) clock += Clock?.DeltaTime ?? Time.deltaTime;
      Matrix4x4 root = transform.localToWorldMatrix;
      foreach (var draw in draws)
      {
        int visible = 0;
        for (int i = 0; i < draw.Local.Length; i++)
          if (draw.Visible[i]) { draw.World[visible] = root * draw.Local[i]; draw.VisibleLivery[visible] = draw.Livery[i]; draw.VisiblePlayback[visible++] = draw.Playback[i]; }
        if (visible == 0) continue;
        // Visibility compaction changes the playback order as well as the matrix order.
        draw.Properties.SetVectorArray("_ActorPlayback", draw.VisiblePlayback);
        draw.Properties.SetVectorArray("_ActorLivery", draw.VisibleLivery);
        draw.Properties.SetFloat("_ActorClock", clock);
        draw.Properties.SetFloat("_ActorReceiveShadows", settings.ReceiveShadows ? 1 : 0);
        for (int m = 0; m < draw.Meshes.Length; m++)
        {
          Mesh mesh = draw.Meshes[m]; Material[] materials = draw.Materials[m];
          for (int s = 0; s < mesh.subMeshCount; s++)
            Graphics.DrawMeshInstanced(mesh, s, materials[s], draw.World, visible, draw.Properties,
              settings.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, settings.ReceiveShadows, gameObject.layer,
              null, LightProbeUsage.Off);
        }
      }
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
      public Vector3 SpinAxis, SpinPivot;
      public Vector4[] Playback;
      public Vector4[] VisiblePlayback;
      public Vector4[] Livery, VisibleLivery;
      public bool[] Visible;
      public string AnimationPath;
      public string Variant;
      public bool HoldPose;
      public bool PlaybackDirty;
      public Mesh[] Meshes;
      public Material[][] Materials;
      public MaterialPropertyBlock Properties;
    }
  }
}
