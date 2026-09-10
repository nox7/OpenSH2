using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
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
      string gameRoot = ValidateInstallation(gameInstallPath);
      string sourcePath = ValidateAssetPath(gameRoot, assetRelativePath);
      converterPath = Path.GetFullPath(string.IsNullOrWhiteSpace(converterPath) ? DefaultConverterPath : converterPath);
      if (!File.Exists(converterPath))
        throw new FileNotFoundException("Build Tools/Granny2MeshConverter in Release configuration before loading GR2 assets.", converterPath);

      cacheDirectory ??= Path.Combine(Application.persistentDataPath, "OpenSH2", "Cache", "Meshes");
      Directory.CreateDirectory(cacheDirectory);
      string cachePath = Path.Combine(cacheDirectory, CreateCacheName(assetRelativePath));
      string grannyDll = Path.Combine(gameRoot, "granny2.dll");
      if (!IsCurrent(cachePath, sourcePath, grannyDll, converterPath))
        Convert(converterPath, gameRoot, sourcePath, cachePath);
      return Granny2MeshCacheReader.Load(cachePath);
    }

    private static void Convert(string converterPath, string gameRoot, string sourcePath, string cachePath)
    {
      var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = converterPath,
          Arguments = $"--game-path {Quote(gameRoot)} --input {Quote(sourcePath)} --output {Quote(cachePath)}",
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
