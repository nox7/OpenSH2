using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        Stronghold2Installation installation = Stronghold2Installation.Validate(gamePath);
        using (var granny = new GrannyNative(installation.GrannyDllPath))
        {
          if (options.TryGetValue("--batch", out string manifestPath))
          {
            // Pairs of UTF-8 lines: absolute input and output paths. One result
            // line per pair: empty for success, base64 UTF-8 error otherwise.
            string[] manifest = File.ReadAllLines(manifestPath, Encoding.UTF8);
            if (manifest.Length % 2 != 0) throw new InvalidDataException("Invalid conversion manifest.");
            var results = new string[manifest.Length / 2];
            int converted = 0;
            for (int i = 0; i < results.Length; i++)
            {
              try
              {
                string source = installation.ValidateAssetPath(manifest[i * 2], ".gr2");
                GrannyModel batchModel = granny.ReadModel(source);
                GrannyMeshCacheWriter.Write(manifest[i * 2 + 1], source, granny.Version, batchModel);
                results[i] = string.Empty;
                converted++;
              }
              catch (Exception exception)
              {
                results[i] = Convert.ToBase64String(Encoding.UTF8.GetBytes(exception.Message));
              }
            }
            File.WriteAllLines(GetRequired(options, "--results"), results, Encoding.UTF8);
            Console.WriteLine($"Converted {converted}/{results.Length} GR2 assets in one batch using Granny {granny.Version}.");
            return 0;
          }
          string inputPath = installation.ValidateAssetPath(GetRequired(options, "--input"), ".gr2");
          string outputPath = GetRequired(options, "--output");
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
