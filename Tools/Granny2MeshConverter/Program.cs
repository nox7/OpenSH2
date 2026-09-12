using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSH2.Granny2MeshConverter
{
  internal static class Program
  {
    private static int Main(string[] args)
    {
      try
      {
        Dictionary<string, string> options = ParseArguments(args);
        string gamePath = GetRequired(options, "--game-path");
        string inputPath = GetRequired(options, "--input");
        string outputPath = GetRequired(options, "--output");

        Stronghold2Installation installation = Stronghold2Installation.Validate(gamePath);
        inputPath = installation.ValidateAssetPath(inputPath, ".gr2");

        using (var granny = new GrannyNative(installation.GrannyDllPath))
        {
          GrannyModel model = granny.ReadModel(inputPath);
          GrannyMeshCacheWriter.Write(outputPath, inputPath, granny.Version, model);
          Console.WriteLine(
            $"Converted {model.Meshes.Count} mesh(es) and found {model.ModelCount} Granny model(s) " +
            $"in {Path.GetFileName(inputPath)} using Granny {granny.Version}.");
        }

        return 0;
      }
      catch (Exception exception)
      {
        Console.Error.WriteLine(exception.Message);
        return 1;
      }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
      var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < args.Length; i += 2)
      {
        if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
          throw new ArgumentException("Usage: --game-path <Stronghold 2> --input <file.gr2> --output <file.osh2mesh>");
        values[args[i]] = args[i + 1];
      }
      return values;
    }

    private static string GetRequired(Dictionary<string, string> values, string name)
    {
      if (!values.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
        throw new ArgumentException($"Missing required argument {name}.");
      return value;
    }
  }
}
