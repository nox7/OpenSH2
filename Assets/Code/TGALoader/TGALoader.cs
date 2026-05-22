// This was made by aaro4130 on the Unity forums.  Thanks boss!
// Updated by nox7 to provide support for compressed TGA file formats
// with the help of GPT Codex.
using System;
using System.IO;
using UnityEngine;

public static class TGALoader
{

  public static Texture2D LoadTGA(string fileName)
  {
    using var imageFile = File.OpenRead(fileName);
    return LoadTGA(imageFile);
  }

  public static Texture2D LoadTGA(Stream TGAStream)
  {
    using BinaryReader r = new(TGAStream);

    byte idLength = r.ReadByte();
    byte colorMapType = r.ReadByte();
    byte imageType = r.ReadByte();

    if (colorMapType != 0)
    {
      throw new Exception("TGA texture used an unsupported color map.");
    }

    r.BaseStream.Seek(5, SeekOrigin.Current);
    r.BaseStream.Seek(4, SeekOrigin.Current);

    short width = r.ReadInt16();
    short height = r.ReadInt16();
    int bitDepth = r.ReadByte();
    byte imageDescriptor = r.ReadByte();

    if (bitDepth != 24 && bitDepth != 32)
    {
      throw new Exception("TGA texture had non 32/24 bit depth.");
    }

    if (imageType != 2 && imageType != 10)
    {
      throw new Exception($"Unsupported TGA image type: {imageType}");
    }

    if (idLength > 0)
    {
      r.BaseStream.Seek(idLength, SeekOrigin.Current);
    }

    Texture2D tex = new(width, height, TextureFormat.ARGB32, false);
    Color32[] pulledColors = new Color32[width * height];
    int bytesPerPixel = bitDepth / 8;
    bool isTopOrigin = (imageDescriptor & 0x20) != 0;

    if (imageType == 2)
    {
      for (int i = 0; i < width * height; i++)
      {
        pulledColors[GetPixelIndex(i, width, height, isTopOrigin)] = ReadColor(r, bytesPerPixel);
      }
    }
    else
    {
      int pixelIndex = 0;
      while (pixelIndex < width * height)
      {
        byte packetHeader = r.ReadByte();
        int pixelCount = (packetHeader & 0x7F) + 1;

        if ((packetHeader & 0x80) != 0)
        {
          Color32 color = ReadColor(r, bytesPerPixel);
          for (int i = 0; i < pixelCount; i++)
          {
            pulledColors[GetPixelIndex(pixelIndex++, width, height, isTopOrigin)] = color;
          }
        }
        else
        {
          for (int i = 0; i < pixelCount; i++)
          {
            pulledColors[GetPixelIndex(pixelIndex++, width, height, isTopOrigin)] = ReadColor(r, bytesPerPixel);
          }
        }
      }
    }

    tex.SetPixels32(pulledColors);
    tex.Apply();
    return tex;
  }

  private static int GetPixelIndex(int pixelIndex, int width, int height, bool isTopOrigin)
  {
    if (isTopOrigin)
    {
      int x = pixelIndex % width;
      int y = pixelIndex / width;
      int flippedY = height - 1 - y;
      return (flippedY * width) + x;
    }

    return pixelIndex;
  }

  private static Color32 ReadColor(BinaryReader reader, int bytesPerPixel)
  {
    byte blue = reader.ReadByte();
    byte green = reader.ReadByte();
    byte red = reader.ReadByte();
    byte alpha = bytesPerPixel == 4 ? reader.ReadByte() : (byte)255;
    return new Color32(red, green, blue, alpha);
  }
}