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
      int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null)
    {
      converterPath ??= Granny2AssetConverter.DefaultConverterPath;
      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Actors");
      string converter = converterPath, cache = cacheDirectory;
      return Task.Run(() => Load(map, installation, overrides, samplesPerSecond, converter, cache));
    }

    private static readonly object ConversionGate = new();
    public S2MActorData Load(S2MFile map, string installation, IEnumerable<S2MActorOverride> overrides = null,
      int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null)
    {
      // Prevent concurrent loaders from writing the same native cache temporary file.
      lock (ConversionGate) return LoadCore(map, installation, overrides, samplesPerSecond, converterPath, cacheDirectory);
    }

    private S2MActorData LoadCore(S2MFile map, string installation, IEnumerable<S2MActorOverride> overrides,
      int samplesPerSecond, string converterPath, string cacheDirectory)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (samplesPerSecond < 1 || samplesPerSecond > 60) throw new ArgumentOutOfRangeException(nameof(samplesPerSecond));
      converterPath ??= Granny2AssetConverter.DefaultConverterPath;
      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Actors");
      Directory.CreateDirectory(cacheDirectory);
      var result = new S2MActorData { Map = map };
      var catalog = new S2MActorCatalog(installation, overrides);
      var batches = new Dictionary<string, S2MActorBatchData>(StringComparer.OrdinalIgnoreCase);
      var issues = new HashSet<string>();
      foreach (var actor in map.Actors)
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
      var manifest = new List<string>(); var pending = new List<string>();
      var caches = new Dictionary<string, string>();
      foreach (var pair in batches)
      {
        var asset = pair.Value.Asset;
        if (models[asset.ModelPath].Error != null) { issues.Add(asset.ModelPath + ": " + models[asset.ModelPath].Error.Message); continue; }
        string source = catalog.SafePath(asset.ModelPath), clip = catalog.SafePath(asset.AnimationPath);
        // Include installation identity, source timestamps, sampler version and sampling rate.
        string identity = source + "|" + File.GetLastWriteTimeUtc(source).Ticks + "|" + clip + "|" + File.GetLastWriteTimeUtc(clip).Ticks
          + "|" + File.GetLastWriteTimeUtc(converterPath).Ticks + "|" + File.GetLastWriteTimeUtc(Path.Combine(installation, "granny2.dll")).Ticks + "|v2|" + samplesPerSecond;
        using var sha = SHA256.Create();
        string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        string cache = Path.Combine(cacheDirectory, key + ".osh2actor");
        caches[pair.Key] = cache;
        if (!File.Exists(cache)) { manifest.Add(source); manifest.Add(clip); manifest.Add(cache); manifest.Add(samplesPerSecond.ToString()); pending.Add(pair.Key); }
      }
      if (pending.Count > 0)
      {
        string file = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + ".batch"), errors = file + ".results";
        try
        {
          File.WriteAllLines(file, manifest, Encoding.UTF8);
          Granny2AssetConverter.ConvertBatch(converterPath, installation, file, errors, "--actor-batch");
          string[] lines = File.ReadAllLines(errors, Encoding.UTF8);
          if (lines.Length != pending.Count) throw new InvalidDataException("Incomplete actor conversion results.");
          for (int i = 0; i < lines.Length; i++) if (lines[i].Length > 0)
          { issues.Add(pending[i] + ": " + Encoding.UTF8.GetString(Convert.FromBase64String(lines[i]))); caches.Remove(pending[i]); }
        }
        catch (Exception e) when (e is InvalidDataException || e is TimeoutException)
        {
          // Completed caches survive a native-process failure. Missing ones become individual issues below.
          issues.Add("Actor conversion: " + e.Message);
        }
        finally { if (File.Exists(file)) File.Delete(file); if (File.Exists(errors)) File.Delete(errors); }
      }
      var loadedIds = new HashSet<int>();
      var loadedMotions = new Dictionary<string, Granny2ActorMotion>(StringComparer.OrdinalIgnoreCase);
      foreach (var pair in caches)
      {
        try
        {
          var batch = batches[pair.Key];
          batch.Model = models[batch.Asset.ModelPath].Model;
          if (!loadedMotions.TryGetValue(pair.Value, out var motion)) loadedMotions[pair.Value] = motion = Granny2ActorMotion.Read(pair.Value);
          batch.Motion = motion;
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
