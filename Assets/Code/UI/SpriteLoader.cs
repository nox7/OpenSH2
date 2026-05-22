using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.UI
{
  class SpriteLoader
  {
    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static Sprite LoadSprite(string texturePath)
    {
      if (SpriteCache.TryGetValue(texturePath, out Sprite cachedSprite))
      {
        return cachedSprite;
      }

      if (!File.Exists(texturePath))
      {
        Debug.LogError($"UI texture not found: {texturePath}");
        return null;
      }

      Texture2D texture;

      // If the texturePath does not end in .tga, we can try loading it directly as a PNG/JPG/etc. using Unity's built-in loading
      if (texturePath.EndsWith(".tga"))
      {
        texture = TGALoader.LoadTGA(texturePath);
      }
      else
      {
        byte[] textureBytes = File.ReadAllBytes(texturePath);
        texture = new(2, 2, TextureFormat.ARGB32, false);
        if (!ImageConversion.LoadImage(texture, textureBytes))
        {
          Debug.LogError($"Failed to load UI texture: {texturePath}");
          return null;
        }
      }

      texture.name = Path.GetFileName(texturePath);
      Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
      SpriteCache[texturePath] = sprite;
      return sprite;
    }
  }
}
