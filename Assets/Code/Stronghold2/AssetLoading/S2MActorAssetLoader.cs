using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class S2MActorData
  {
    public S2MFile Map { get; internal set; }
    public List<S2MActorBatchData> Batches { get; } = new();
    public List<string> Issues { get; } = new();
    public int PlacedCount { get; internal set; }
    public int InactiveCount { get; internal set; }
  }

  public sealed class S2MActorBatchData
  {
    public S2MActorAsset Asset { get; internal set; }
    public Granny2ModelData Model { get; internal set; }
    public Granny2ActorMotion Motion { get; internal set; }
    public List<ActorPlacement> Placements { get; } = new();
    public List<float> Scales { get; } = new();
    public List<(Granny2ModelData model, int point, string boneName)> Attachments { get; } = new();
  }

  public sealed class Granny2ActorMotion
  {
    public float Duration { get; private set; }
    public int Bones { get; private set; }
    public int Frames { get; private set; }
    public List<Vector4[]> BoneIndices { get; } = new();
    public List<Vector4[]> BoneWeights { get; } = new();
    public float[] Matrices { get; private set; }
    public List<string> BoneNames { get; } = new();

    public Granny2ActorMotion CreateBindPose()
    {
      var result=new Granny2ActorMotion { Duration=1,Bones=Bones,Frames=2,Matrices=new float[Bones*2*16] };
      result.BoneNames.AddRange(BoneNames);result.BoneIndices.AddRange(BoneIndices);result.BoneWeights.AddRange(BoneWeights);
      for(int frame=0;frame<2;frame++)for(int bone=0;bone<Bones;bone++)for(int axis=0;axis<4;axis++)
        result.Matrices[(frame*Bones+bone)*16+axis*5]=1;
      return result;
    }

    public static Granny2ActorMotion Read(string path)
    {
      using var r = new BinaryReader(File.OpenRead(path));
      if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "OSH2ACT\0" || r.ReadInt32() != 2)
        throw new InvalidDataException("Unsupported actor cache.");
      var motion = new Granny2ActorMotion { Duration = r.ReadSingle(), Bones = Count(r, 2048), Frames = Count(r, 36001) };
      if (!(motion.Duration > 0) || motion.Duration > 600 || motion.Bones == 0 || motion.Frames < 2)
        throw new InvalidDataException("Invalid actor clip dimensions.");
      int names = Count(r, 1024);
      if (names * 2 != motion.Bones) throw new InvalidDataException("Invalid attachment palette.");
      for (int i = 0; i < names; i++) motion.BoneNames.Add(r.ReadString());
      int meshes = Count(r, 10000);
      for (int m = 0; m < meshes; m++)
      {
        int vertices = Count(r, 1000000);
        if ((long)vertices * 32 > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException();
        var indices = new Vector4[vertices]; var weights = new Vector4[vertices];
        for (int v = 0; v < vertices; v++)
        {
          float sum = 0;
          for (int w = 0; w < 4; w++)
          {
            int bone = r.ReadInt32(); float weight = r.ReadSingle();
            if (bone < 0 || bone >= motion.Bones || !(weight >= 0 && weight <= 1)) throw new InvalidDataException("Invalid actor weight.");
            indices[v][w] = bone; weights[v][w] = weight; sum += weight;
          }
          if (Math.Abs(sum - 1) > .001f) throw new InvalidDataException("Unnormalized actor weights.");
        }
        motion.BoneIndices.Add(indices); motion.BoneWeights.Add(weights);
      }
      int count = checked(motion.Bones * motion.Frames * 16);
      if (count > 64000000 || (long)count * 4 != r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("Truncated actor matrix cache.");
      motion.Matrices = new float[count];
      for (int i = 0; i < count; i++)
      {
        float value = r.ReadSingle();
        if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite actor matrix.");
        motion.Matrices[i] = value;
      }
      return motion;
    }
    private static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("Invalid actor cache count."); return n; }
  }

  public sealed class S2MActorAssetLoader
  {
    // Capture Unity paths on the caller thread; all file I/O/conversion can run on a worker.
    public Task<S2MActorData> LoadAsync(S2MFile map, string installation, IEnumerable<S2MActorOverride> overrides = null,
      int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null,
      IReadOnlyList<ActorPlacement> placements = null,HorseCartAssemblySettings assembly=null)
    {
      converterPath ??= Granny2AssetConverter.DefaultConverterPath;
      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Actors");
      string converter = converterPath, cache = cacheDirectory;
      return Task.Run(() => Load(map, installation, overrides, samplesPerSecond, converter, cache, placements,assembly));
    }

    public S2MActorData Load(S2MFile map, string installation, IEnumerable<S2MActorOverride> overrides = null,
      int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null,
      IReadOnlyList<ActorPlacement> placements = null,HorseCartAssemblySettings assembly=null)
    {
      // Prevent concurrent loaders from writing the same native cache temporary file.
      lock (Granny2AnimationAssetLoader.ConversionGate) return LoadCore(map, installation, overrides, samplesPerSecond, converterPath, cacheDirectory, placements,assembly);
    }

    private S2MActorData LoadCore(S2MFile map, string installation, IEnumerable<S2MActorOverride> overrides,
      int samplesPerSecond, string converterPath, string cacheDirectory, IReadOnlyList<ActorPlacement> placements,HorseCartAssemblySettings assembly)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (samplesPerSecond < 1 || samplesPerSecond > 60) throw new ArgumentOutOfRangeException(nameof(samplesPerSecond));
      converterPath ??= Granny2AssetConverter.DefaultConverterPath;
      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Actors");
      Directory.CreateDirectory(cacheDirectory);
      var result = new S2MActorData { Map = map };
      var catalog = new S2MActorCatalog(installation, overrides,assembly);
      var batches = new Dictionary<string, S2MActorBatchData>(StringComparer.OrdinalIgnoreCase);
      var issues = new HashSet<string>();
      foreach (var actor in placements ?? map.Actors)
      {
        if (!actor.OnMap) { result.InactiveCount++; continue; }
        try
        {
          foreach (var asset in catalog.ResolveParts(actor))
          {
            if (!batches.TryGetValue(asset.Key, out var batch)) batches[asset.Key] = batch = new S2MActorBatchData { Asset = asset };
            batch.Placements.Add(actor); batch.Scales.Add(asset.Scale);
          }
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException) { issues.Add(actor.Type + ": " + e.Message); }
      }
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var batch in batches.Values)
      {
        paths.Add(batch.Asset.ModelPath);
        foreach (var attachment in batch.Asset.Attachments) paths.Add(attachment.ModelPath);
      }
      string installationKey;
      using (var sha = SHA256.Create()) installationKey = BitConverter.ToString(sha.ComputeHash(
        Encoding.UTF8.GetBytes(Path.GetFullPath(installation).ToUpperInvariant())), 0, 8).Replace("-", "");
      var models = Granny2AssetConverter.LoadOrConvertMany(installation, paths, converterPath, Path.Combine(cacheDirectory, "Meshes", installationKey));
      var requests = new List<Granny2AnimationRequest>();
      foreach (var batch in batches.Values)
        if (models[batch.Asset.ModelPath].Error == null)
          requests.Add(new Granny2AnimationRequest(batch.Asset.ModelPath, batch.Asset.AnimationPath));
      var motions = Granny2AnimationAssetLoader.LoadOrConvertMany(installation, requests, samplesPerSecond, converterPath, cacheDirectory);
      var loadedIds = new HashSet<int>();
      foreach (var pair in batches)
      {
        try
        {
          var batch = pair.Value;
          var modelResult = models[batch.Asset.ModelPath];
          if (modelResult.Error != null) throw new InvalidDataException(modelResult.Error.Message, modelResult.Error);
          var animation = motions[new Granny2AnimationRequest(batch.Asset.ModelPath, batch.Asset.AnimationPath).Key];
          if (animation.Error != null) throw new InvalidDataException(animation.Error.Message, animation.Error);
          batch.Model = modelResult.Model; batch.Motion = batch.Asset.UseBindPose ? animation.Motion.CreateBindPose() : animation.Motion;
          foreach (var attachment in batch.Asset.Attachments)
          {
            var model = models[attachment.ModelPath];
            if (model.Error != null) issues.Add(attachment.ModelPath + ": " + model.Error.Message);
            else batch.Attachments.Add((model.Model, attachment.Point, attachment.BoneName));
          }
          result.Batches.Add(batch); foreach (var placement in batch.Placements) loadedIds.Add(placement.Id);
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException) { issues.Add(pair.Key + ": " + e.Message); }
      }
      result.PlacedCount = loadedIds.Count;
      result.Issues.AddRange(issues);
      return result;
    }
  }
}
