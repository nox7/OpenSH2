using System;
using System.IO;

namespace OpenSH2.Granny2MeshConverter
{
  internal sealed class Stronghold2Installation
  {
    public string RootPath { get; private set; }
    public string GrannyDllPath { get; private set; }

    public static Stronghold2Installation Validate(string gamePath)
    {
      string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      string executable = Path.Combine(root, "Stronghold2.exe");
      string grannyDll = Path.Combine(root, "granny2.dll");
      if (!File.Exists(executable) || !File.Exists(grannyDll))
        throw new FileNotFoundException("The selected directory is not a Stronghold 2 installation (Stronghold2.exe and granny2.dll are required).", root);

      return new Stronghold2Installation { RootPath = root, GrannyDllPath = grannyDll };
    }

    public string ValidateAssetPath(string assetPath, string requiredExtension)
    {
      string fullPath = Path.GetFullPath(assetPath);
      string rootPrefix = RootPath + Path.DirectorySeparatorChar;
      if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The input asset must be inside the validated Stronghold 2 installation.");
      if (!string.Equals(Path.GetExtension(fullPath), requiredExtension, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Expected a {requiredExtension} input asset.");
      if (!File.Exists(fullPath)) throw new FileNotFoundException("The Granny asset does not exist.", fullPath);
      return fullPath;
    }
  }
}
