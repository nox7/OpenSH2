using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Loads and parses an S2M file without creating any Unity scene objects.</summary>
  public static class S2MFileLoader
  {
    public static S2MFile Load(string s2mFilePath)
    {
      if (string.IsNullOrWhiteSpace(s2mFilePath))
        throw new ArgumentException("An S2M file path is required.", nameof(s2mFilePath));

      string fullPath = Path.GetFullPath(s2mFilePath);
      return new Assets.Code.Stronghold2.S2MReader.S2MReader(fullPath).ReadS2MFile();
    }
  }
}
