using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.UI.Assets
{
  /// <summary>Loads installed game artwork without importing or distributing it with the port.</summary>
  internal sealed class RuntimeUiAssets : IDisposable
  {
    private readonly Dictionary<string, Texture2D> textures = new();

    public Texture2D Load(string path)
    {
      if (textures.TryGetValue(path, out var cached)) return cached;
      Texture2D texture = null;
      try
      {
        if (Path.GetExtension(path).Equals(".tga", StringComparison.OrdinalIgnoreCase))
          texture = TGALoader.LoadTGA(path);
        else if (Path.GetExtension(path).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
          texture = BmpTextureLoader.Load(path);
        else
        {
          texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
          if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            throw new InvalidDataException("Unsupported image data.");
        }
        if (texture != null) texture.name = Path.GetFileName(path);
      }
      catch (Exception error)
      {
        if (texture != null) UnityEngine.Object.Destroy(texture);
        texture = null;
        Debug.LogWarning($"Unable to load UI artwork {path}: {error.Message}");
      }
      textures[path] = texture;
      return texture;
    }

    public void Dispose()
    {
      foreach (var texture in textures.Values)
        if (texture != null) UnityEngine.Object.Destroy(texture);
      textures.Clear();
    }
  }
}
