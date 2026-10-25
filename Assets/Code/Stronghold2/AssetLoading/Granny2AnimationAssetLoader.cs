using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public readonly struct Granny2AnimationRequest
  {
    public readonly string ModelPath, AnimationPath;
    public string Key => ModelPath + "|" + AnimationPath;
    public Granny2AnimationRequest(string modelPath, string animationPath)
    { ModelPath = modelPath?.Replace('\\', '/'); AnimationPath = animationPath?.Replace('\\', '/'); }
  }

  public sealed class Granny2AnimationLoadResult
  {
    public Granny2ActorMotion Motion { get; internal set; }
    public Exception Error { get; internal set; }
  }

  /// <summary>Shared native sampling/cache path for animated units and scenery parts.</summary>
  public static class Granny2AnimationAssetLoader
  {
    internal static readonly object ConversionGate = new();
    public static IReadOnlyDictionary<string, Granny2AnimationLoadResult> LoadOrConvertMany(string installation,
      IEnumerable<Granny2AnimationRequest> requests, int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null)
    {
      if (samplesPerSecond < 1 || samplesPerSecond > 60) throw new ArgumentOutOfRangeException(nameof(samplesPerSecond));
      lock (ConversionGate)
      {
        var results = new Dictionary<string, Granny2AnimationLoadResult>(StringComparer.OrdinalIgnoreCase);
        var unique = new Dictionary<string, Granny2AnimationRequest>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests) unique[request.Key] = request;
        if (unique.Count == 0) return results;
        string root = Path.GetFullPath(installation).TrimEnd('\\', '/');
        converterPath = Path.GetFullPath(converterPath ?? Granny2AssetConverter.DefaultConverterPath);
        cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Animations");
        Directory.CreateDirectory(cacheDirectory);
        var manifest = new List<string>(); var pending = new List<string>();
        var caches = new Dictionary<string, string>();
        foreach (var pair in unique)
        {
          var result = new Granny2AnimationLoadResult(); results[pair.Key] = result;
          try
          {
            string source = Source(pair.Value.ModelPath), clip = Source(pair.Value.AnimationPath);
            if (!File.Exists(converterPath)) throw new FileNotFoundException("Build Tools/Granny2MeshConverter in Release before loading animations.", converterPath);
            string identity = source + "|" + File.GetLastWriteTimeUtc(source).Ticks + "|" + clip + "|" + File.GetLastWriteTimeUtc(clip).Ticks
              + "|" + File.GetLastWriteTimeUtc(converterPath).Ticks + "|" + File.GetLastWriteTimeUtc(Path.Combine(root, "granny2.dll")).Ticks + "|v2|" + samplesPerSecond;
            using var sha = SHA256.Create();
            string cache = Path.Combine(cacheDirectory, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "") + ".osh2actor");
            caches[pair.Key] = cache;
            if (!File.Exists(cache)) { manifest.Add(source); manifest.Add(clip); manifest.Add(cache); manifest.Add(samplesPerSecond.ToString()); pending.Add(pair.Key); }
          }
          catch (Exception e) when (IsAssetError(e)) { result.Error = e; }
        }
        if (pending.Count > 0)
        {
          string file = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + ".batch"), errors = file + ".results";
          try
          {
            File.WriteAllLines(file, manifest, Encoding.UTF8);
            Granny2AssetConverter.ConvertBatch(converterPath, root, file, errors, "--actor-batch");
            string[] lines = File.ReadAllLines(errors, Encoding.UTF8);
            if (lines.Length != pending.Count) throw new InvalidDataException("Incomplete animation conversion results.");
            for (int i = 0; i < lines.Length; i++) if (lines[i].Length > 0)
              results[pending[i]].Error = new InvalidDataException(Encoding.UTF8.GetString(Convert.FromBase64String(lines[i])));
          }
          catch (Exception e) when (IsAssetError(e) || e is TimeoutException)
          { foreach (string key in pending) if (!File.Exists(caches[key])) results[key].Error = e; }
          finally { if (File.Exists(file)) File.Delete(file); if (File.Exists(errors)) File.Delete(errors); }
        }
        foreach (var pair in caches)
        {
          if (results[pair.Key].Error != null) continue;
          try { results[pair.Key].Motion = Granny2ActorMotion.Read(pair.Value); }
          catch (Exception e) when (IsAssetError(e)) { results[pair.Key].Error = e; }
        }
        return results;

        string Source(string relative)
        {
          if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new ArgumentException("Use a relative animation/model path.");
          string path = Path.GetFullPath(Path.Combine(root, relative));
          if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Path.GetExtension(path).Equals(".gr2", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Animation/model must be a GR2 inside the installation.");
          if (!File.Exists(path)) throw new FileNotFoundException("Animation asset not found.", path);
          return path;
        }
      }
    }
    private static bool IsAssetError(Exception e) => e is IOException || e is InvalidDataException || e is ArgumentException;
  }
}
