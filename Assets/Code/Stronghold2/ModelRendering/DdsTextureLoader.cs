using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Loads the DXT1, DXT3, and DXT5 DDS textures used by Stronghold 2.</summary>
  internal static class DdsTextureLoader
  {
    /// <param name="keepReadable">Keep CPU pixels when a caller needs to copy this texture into another runtime texture.</param>
    public static Texture2D Load(string path, bool keepReadable = false)
    {
      byte[] bytes = File.ReadAllBytes(path);
      if (bytes.Length < 128 || Encoding.ASCII.GetString(bytes, 0, 4) != "DDS ")
        throw new InvalidDataException($"Invalid DDS texture: {path}");
      int height = BitConverter.ToInt32(bytes, 12);
      int width = BitConverter.ToInt32(bytes, 16);
      string fourCc = Encoding.ASCII.GetString(bytes, 84, 4);
      if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        throw new InvalidDataException($"Invalid DDS dimensions {width}x{height}.");

      Color32[] pixels = fourCc switch
      {
        "DXT1" => DecodeDxt(bytes, 128, width, height, DxtKind.Dxt1),
        "DXT3" => DecodeDxt(bytes, 128, width, height, DxtKind.Dxt3),
        "DXT5" => DecodeDxt(bytes, 128, width, height, DxtKind.Dxt5),
        _ => throw new NotSupportedException($"DDS compression {fourCc} is not supported yet.")
      };

      var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
      {
        name = Path.GetFileNameWithoutExtension(path),
        wrapMode = TextureWrapMode.Repeat,
        filterMode = FilterMode.Bilinear
      };
      texture.SetPixels32(pixels);
      texture.Apply(updateMipmaps: false, makeNoLongerReadable: !keepReadable);
      return texture;
    }

    private static Color32[] DecodeDxt(byte[] data, int offset, int width, int height, DxtKind kind)
    {
      var pixels = new Color32[checked(width * height)];
      int blockBytes = kind == DxtKind.Dxt1 ? 8 : 16;
      for (int blockY = 0; blockY < (height + 3) / 4; blockY++)
      {
        for (int blockX = 0; blockX < (width + 3) / 4; blockX++)
        {
          if (offset + blockBytes > data.Length) throw new EndOfStreamException("DDS block data is truncated.");
          byte[] alpha = kind == DxtKind.Dxt1 ? null : DecodeAlpha(data, offset, kind);
          int colorOffset = kind == DxtKind.Dxt1 ? offset : offset + 8;
          ushort c0 = BitConverter.ToUInt16(data, colorOffset);
          ushort c1 = BitConverter.ToUInt16(data, colorOffset + 2);
          Color32[] colors = CreateColorPalette(c0, c1, kind != DxtKind.Dxt1 || c0 > c1);
          uint selectors = BitConverter.ToUInt32(data, colorOffset + 4);

          for (int py = 0; py < 4; py++)
          {
            for (int px = 0; px < 4; px++)
            {
              int x = blockX * 4 + px;
              int yFromTop = blockY * 4 + py;
              if (x >= width || yFromTop >= height) continue;
              int sample = py * 4 + px;
              Color32 color = colors[(int)((selectors >> (sample * 2)) & 3)];
              if (alpha != null) color.a = alpha[sample];
              // Keeping DDS row zero at Unity pixel row zero compensates for the
              // Direct3D top-origin UV convention used by these assets.
              pixels[yFromTop * width + x] = color;
            }
          }
          offset += blockBytes;
        }
      }
      return pixels;
    }

    private static byte[] DecodeAlpha(byte[] data, int offset, DxtKind kind)
    {
      var alpha = new byte[16];
      if (kind == DxtKind.Dxt3)
      {
        ulong bits = BitConverter.ToUInt64(data, offset);
        for (int i = 0; i < alpha.Length; i++) alpha[i] = (byte)(((bits >> (i * 4)) & 0xF) * 17);
        return alpha;
      }

      byte a0 = data[offset];
      byte a1 = data[offset + 1];
      var palette = new byte[8];
      palette[0] = a0;
      palette[1] = a1;
      if (a0 > a1)
      {
        for (int i = 1; i <= 6; i++) palette[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
      }
      else
      {
        for (int i = 1; i <= 4; i++) palette[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
        palette[6] = 0;
        palette[7] = 255;
      }
      ulong selectors = 0;
      for (int i = 0; i < 6; i++) selectors |= (ulong)data[offset + 2 + i] << (i * 8);
      for (int i = 0; i < alpha.Length; i++) alpha[i] = palette[(int)((selectors >> (i * 3)) & 7)];
      return alpha;
    }

    private static Color32[] CreateColorPalette(ushort c0, ushort c1, bool fourColors)
    {
      Color32 a = Decode565(c0);
      Color32 b = Decode565(c1);
      var colors = new Color32[4];
      colors[0] = a;
      colors[1] = b;
      if (fourColors)
      {
        colors[2] = Mix(a, b, 2, 1, 3);
        colors[3] = Mix(a, b, 1, 2, 3);
      }
      else
      {
        colors[2] = Mix(a, b, 1, 1, 2);
        colors[3] = new Color32(0, 0, 0, 0);
      }
      return colors;
    }

    private static Color32 Decode565(ushort value)
    {
      byte r = (byte)(((value >> 11) & 31) * 255 / 31);
      byte g = (byte)(((value >> 5) & 63) * 255 / 63);
      byte b = (byte)((value & 31) * 255 / 31);
      return new Color32(r, g, b, 255);
    }

    private static Color32 Mix(Color32 a, Color32 b, int aw, int bw, int divisor) => new Color32(
      (byte)((a.r * aw + b.r * bw) / divisor),
      (byte)((a.g * aw + b.g * bw) / divisor),
      (byte)((a.b * aw + b.b * bw) / divisor),
      255);

    private enum DxtKind { Dxt1, Dxt3, Dxt5 }
  }
}
