using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Shares imported diffuse textures across building/model batches with explicit ownership.</summary>
  internal static class Granny2TextureCache
  {
    private sealed class Entry
    {
      public Texture2D Texture;
      public int References;
    }

    private static readonly Dictionary<string, Entry> Textures = new(StringComparer.OrdinalIgnoreCase);

    public static Texture2D Acquire(string path)
    {
      path = Path.GetFullPath(path);
      if (!Textures.TryGetValue(path, out Entry entry) || entry.Texture == null)
      {
        entry = new Entry { Texture = DdsTextureLoader.Load(path) };
        Textures[path] = entry;
      }
      entry.References++;
      return entry.Texture;
    }

    public static void Release(string path)
    {
      path = Path.GetFullPath(path);
      if (!Textures.TryGetValue(path, out Entry entry)) return;
      if (--entry.References > 0) return;
      Textures.Remove(path);
      if (entry.Texture == null) return;
      if (Application.isPlaying) UnityEngine.Object.Destroy(entry.Texture);
      else UnityEngine.Object.DestroyImmediate(entry.Texture);
    }
  }
}
