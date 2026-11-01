using System;
using System.IO;
using UnityEngine;

namespace Assets.Code.UI.Assets
{
  /// <summary>Installed UI artwork uses uncompressed Windows RGB BMPs (Unity LoadImage accepts PNG/JPEG).</summary>
  internal static class BmpTextureLoader
  {
    public static Texture2D Load(string path)
    {
      using var stream = File.OpenRead(path);
      using var reader = new BinaryReader(stream);
      if (reader.ReadUInt16() != 0x4D42) throw new InvalidDataException("Invalid BMP signature.");
      stream.Position = 10;
      var offset = reader.ReadUInt32();
      var headerSize = reader.ReadUInt32();
      var width = reader.ReadInt32();
      var height = reader.ReadInt32();
      var planes = reader.ReadUInt16();
      var bits = reader.ReadUInt16();
      var compression = reader.ReadUInt32();
      if (headerSize < 40 || width <= 0 || height == 0 || height == int.MinValue ||
          planes != 1 || (bits != 24 && bits != 32) || compression != 0)
        throw new InvalidDataException("Unsupported BMP layout.");
      int rows = Math.Abs(height), stride = checked(((width * bits + 31) / 32) * 4);
      if (offset < 14 + headerSize || offset + (long)stride * rows > stream.Length)
        throw new InvalidDataException("Truncated BMP pixels.");
      var pixels = new Color32[checked(width * rows)];
      stream.Position = offset;
      for (int row = 0; row < rows; row++)
      {
        var bytes = reader.ReadBytes(stride);
        int y = height > 0 ? row : rows - row - 1;
        for (int x = 0; x < width; x++)
        {
          int i = x * (bits / 8);
          // BI_RGB's fourth byte is reserved, not alpha.
          pixels[y * width + x] = new Color32(bytes[i + 2], bytes[i + 1], bytes[i], 255);
        }
      }
      var texture = new Texture2D(width, rows, TextureFormat.RGBA32, false);
      texture.SetPixels32(pixels);
      texture.Apply(false, true);
      return texture;
    }
  }
}
