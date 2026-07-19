using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Assets.Code.Utilities
{
  /// <summary>
  /// Locates and inflates complete zlib-wrapped DEFLATE streams in an S2M file.
  /// System.IO.Compression is part of Unity's .NET profile; no NuGet package is required.
  /// </summary>
  internal class ZLibDecompressor
  {
    public IReadOnlyList<ZLibDecompressedSegment> DecompressAll(BinaryReader reader)
    {
      if (reader == null) throw new ArgumentNullException(nameof(reader));

      Stream input = reader.BaseStream;
      long firstOffset = input.Position;
      long remaining = input.Length - firstOffset;
      if (remaining > int.MaxValue) throw new InvalidDataException("S2M file is too large to decompress in memory.");

      byte[] source = reader.ReadBytes((int)remaining);
      var segments = new List<ZLibDecompressedSegment>();

      for (int offset = 0; offset < source.Length - 1; offset++)
      {
        if (!HasZlibHeader(source, offset)) continue;

        if (TryDecompress(source, offset, out byte[] bytes))
        {
          segments.Add(new ZLibDecompressedSegment(firstOffset + offset, bytes));
        }
      }

      return segments;
    }

    private static bool HasZlibHeader(byte[] source, int offset)
    {
      byte cmf = source[offset];
      byte flg = source[offset + 1];
      return (cmf & 0x0F) == 8 && (cmf >> 4) <= 7 && (((cmf << 8) | flg) % 31) == 0;
    }

    private static bool TryDecompress(byte[] source, int zlibOffset, out byte[] bytes)
    {
      bytes = null;
      try
      {
        // DeflateStream expects the DEFLATE body, whereas zlib adds a two-byte header.
        using var compressed = new MemoryStream(source, writable: false);
        compressed.Position = zlibOffset + 2;
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        bytes = output.ToArray();
        return true;
      }
      catch (InvalidDataException)
      {
        return false;
      }
      catch (IOException)
      {
        return false;
      }
    }
  }

  /// <summary>One decompressed zlib payload, retained in its original file order.</summary>
  internal sealed class ZLibDecompressedSegment
  {
    public long CompressedStartOffset { get; }
    public byte[] Bytes { get; }

    public ZLibDecompressedSegment(long compressedStartOffset, byte[] bytes)
    {
      CompressedStartOffset = compressedStartOffset;
      Bytes = bytes;
    }
  }
}
