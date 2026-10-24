using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class Granny2AssetLoadResult
  {
    public Granny2ModelData Model { get; internal set; }
    public Exception Error { get; internal set; }
  }

  public static class Granny2AssetConverter
  {
    public static string DefaultConverterPath => Path.GetFullPath(Path.Combine(
      Application.dataPath, "..", "Tools", "Granny2MeshConverter", "bin", "Release", "net48",
      "OpenSH2.Granny2MeshConverter.exe"));

    public static Granny2ModelData LoadOrConvert(
      string gameInstallPath,
      string assetRelativePath,
      string converterPath = null,
      string cacheDirectory = null)
    {
      Granny2AssetLoadResult result = LoadOrConvertMany(gameInstallPath,
        new[] { assetRelativePath }, converterPath, cacheDirectory)[assetRelativePath];
      if (result.Error != null) throw result.Error;
      return result.Model;
    }

    /// <summary>Converts stale assets in one native process, then loads each distinct cache once.
    /// Individual asset failures do not prevent unrelated assets from loading.</summary>
    public static IReadOnlyDictionary<string, Granny2AssetLoadResult> LoadOrConvertMany(
      string gameInstallPath, IEnumerable<string> assetRelativePaths,
      string converterPath = null, string cacheDirectory = null)
    {
      var results = new Dictionary<string, Granny2AssetLoadResult>(StringComparer.OrdinalIgnoreCase);
      foreach (string path in assetRelativePaths)
        if (!results.ContainsKey(path)) results.Add(path, new Granny2AssetLoadResult());
      if (results.Count == 0) return results;
      string gameRoot = ValidateInstallation(gameInstallPath);
      converterPath = Path.GetFullPath(string.IsNullOrWhiteSpace(converterPath) ? DefaultConverterPath : converterPath);
      if (!File.Exists(converterPath))
        throw new FileNotFoundException("Build Tools/Granny2MeshConverter in Release configuration before loading GR2 assets.", converterPath);

      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Meshes");
      Directory.CreateDirectory(cacheDirectory);
      string grannyDll = Path.Combine(gameRoot, "granny2.dll");
      var pending = new List<string>();
      var manifest = new List<string>();
      var cachePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var pair in results)
      {
        try
        {
          string sourcePath = ValidateAssetPath(gameRoot, pair.Key);
          string cachePath = Path.Combine(cacheDirectory, CreateCacheName(pair.Key));
          cachePaths.Add(pair.Key, cachePath);
          if (!IsCurrent(cachePath, sourcePath, grannyDll, converterPath))
          {
            pending.Add(pair.Key);
            manifest.Add(sourcePath);
            manifest.Add(cachePath);
          }
        }
        catch (Exception exception) when (IsAssetError(exception)) { pair.Value.Error = exception; }
      }
      if (pending.Count > 0)
      {
        string manifestPath = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + ".batch");
        string resultsPath = manifestPath + ".results";
        try
        {
          File.WriteAllLines(manifestPath, manifest, Encoding.UTF8);
          ConvertBatch(converterPath, gameRoot, manifestPath, resultsPath);
          string[] errors = File.ReadAllLines(resultsPath, Encoding.UTF8);
          if (errors.Length != pending.Count) throw new InvalidDataException("Incomplete Granny batch conversion results.");
          for (int i = 0; i < errors.Length; i++)
            if (errors[i].Length > 0)
              results[pending[i]].Error = new InvalidDataException(Encoding.UTF8.GetString(System.Convert.FromBase64String(errors[i])));
        }
        finally
        {
          if (File.Exists(manifestPath)) File.Delete(manifestPath);
          if (File.Exists(resultsPath)) File.Delete(resultsPath);
        }
      }
      foreach (var pair in cachePaths)
      {
        if (results[pair.Key].Error != null) continue;
        try { results[pair.Key].Model = Granny2MeshCacheReader.Load(pair.Value); }
        catch (Exception exception) when (IsAssetError(exception)) { results[pair.Key].Error = exception; }
      }
      return results;
    }

    private static bool IsAssetError(Exception exception) => exception is IOException
      || exception is InvalidDataException || exception is ArgumentException || exception is InvalidOperationException;

    private static void ConvertBatch(string converterPath, string gameRoot, string manifestPath, string resultsPath)
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = converterPath,
          Arguments = $"--game-path {Quote(gameRoot)} --batch {Quote(manifestPath)} --results {Quote(resultsPath)}",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        }
      };
      process.Start();
      Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
      Task<string> errorTask = process.StandardError.ReadToEndAsync();
      if (!process.WaitForExit(120000))
      {
        process.Kill();
        process.WaitForExit();
        throw new TimeoutException("The Granny mesh converter timed out.");
      }
      string output = outputTask.GetAwaiter().GetResult();
      string error = errorTask.GetAwaiter().GetResult();
      if (process.ExitCode != 0)
        throw new InvalidDataException($"The Granny mesh converter failed: {error.Trim()}");
      if (!string.IsNullOrWhiteSpace(output)) UnityEngine.Debug.Log(output.Trim());
    }

    private static string ValidateInstallation(string gameInstallPath)
    {
      string root = Path.GetFullPath(gameInstallPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      if (!File.Exists(Path.Combine(root, "Stronghold2.exe")) || !File.Exists(Path.Combine(root, "granny2.dll")))
        throw new DirectoryNotFoundException("A valid Stronghold 2 installation containing Stronghold2.exe and granny2.dll is required.");
      return root;
    }

    private static string ValidateAssetPath(string gameRoot, string assetRelativePath)
    {
      if (Path.IsPathRooted(assetRelativePath))
        throw new ArgumentException("Use a GR2 path relative to the Stronghold 2 installation.", nameof(assetRelativePath));
      string fullPath = Path.GetFullPath(Path.Combine(gameRoot, assetRelativePath));
      if (!fullPath.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The GR2 asset must remain inside the Stronghold 2 installation.");
      if (!string.Equals(Path.GetExtension(fullPath), ".gr2", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        throw new FileNotFoundException("The selected Stronghold 2 GR2 asset does not exist.", fullPath);
      return fullPath;
    }

    private static bool IsCurrent(string cachePath, params string[] inputs)
    {
      if (!File.Exists(cachePath)) return false;
      DateTime cacheTime = File.GetLastWriteTimeUtc(cachePath);
      foreach (string input in inputs) if (File.GetLastWriteTimeUtc(input) > cacheTime) return false;
      return true;
    }

    private static string CreateCacheName(string relativePath)
    {
      using var sha = SHA256.Create();
      byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/').ToLowerInvariant()));
      string shortHash = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
      return $"{Path.GetFileNameWithoutExtension(relativePath)}-{shortHash}.osh2mesh";
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
  }
}
