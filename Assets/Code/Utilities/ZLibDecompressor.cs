using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Assets.Code.Utilities
{
  /// <summary>
  /// Inflates the consecutive, zlib-wrapped DEFLATE streams at the current reader position.
  /// System.IO.Compression is part of Unity's .NET profile; no NuGet package is required.
  /// </summary>
  internal class ZLibDecompressor
  {
    private const uint Adler32Modulus = 65521;

    public IReadOnlyList<ZLibDecompressedSegment> DecompressAll(BinaryReader reader)
    {
      if (reader == null) throw new ArgumentNullException(nameof(reader));

      Stream input = reader.BaseStream;
      long firstOffset = input.Position;
      long remaining = input.Length - firstOffset;
      if (remaining > int.MaxValue) throw new InvalidDataException("S2M file is too large to decompress in memory.");

      byte[] source = reader.ReadBytes((int)remaining);
      var segments = new List<ZLibDecompressedSegment>();
      int offset = 0;

      // S2M zlib members are consecutive. Scanning inside a compressed member for
      // header-like bytes produces false positives, so only inspect the exact end
      // of the preceding, fully validated member.
      while (HasZlibHeader(source, offset))
      {
        if (!TryDecompress(source, offset, out byte[] bytes, out int endOffset))
        {
          throw new InvalidDataException($"Invalid zlib stream at S2M offset {firstOffset + offset}.");
        }

        segments.Add(new ZLibDecompressedSegment(firstOffset + offset, bytes));
        offset = endOffset;
      }

      return segments;
    }

    private static bool HasZlibHeader(byte[] source, int offset)
    {
      if (offset < 0 || offset + 1 >= source.Length) return false;

      byte cmf = source[offset];
      byte flg = source[offset + 1];
      bool usesDeflate = (cmf & 0x0F) == 8;
      bool validWindowSize = (cmf >> 4) <= 7;
      bool validHeaderChecksum = (((cmf << 8) | flg) % 31) == 0;
      bool requiresPresetDictionary = (flg & 0x20) != 0;

      return usesDeflate && validWindowSize && validHeaderChecksum && !requiresPresetDictionary;
    }

    private static bool TryDecompress(byte[] source, int zlibOffset, out byte[] bytes, out int endOffset)
    {
      bytes = null;
      endOffset = zlibOffset;

      try
      {
        if (!HasZlibHeader(source, zlibOffset)) return false;

        // DeflateStream expects the raw DEFLATE body and may buffer far beyond its
        // endpoint. Locate the trailer by its checksum, then verify the candidate
        // again with a stream bounded to the exact DEFLATE body.
        using var compressed = new MemoryStream(source, writable: false);
        compressed.Position = zlibOffset + 2;
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var output = new MemoryStream();
        inflater.CopyTo(output);

        bytes = output.ToArray();
        uint adler32 = ComputeAdler32(bytes);
        int trailerOffset = FindVerifiedTrailerOffset(source, zlibOffset + 2, bytes, adler32);
        if (trailerOffset < 0)
        {
          bytes = null;
          return false;
        }

        endOffset = trailerOffset + 4;
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

    private static int FindVerifiedTrailerOffset(byte[] source, int bodyOffset, byte[] expectedBytes, uint adler32)
    {
      for (int offset = bodyOffset; offset <= source.Length - 4; offset++)
      {
        if (ReadUInt32BigEndian(source, offset) != adler32) continue;
        if (InflatesTo(source, bodyOffset, offset - bodyOffset, expectedBytes)) return offset;
      }

      return -1;
    }

    private static bool InflatesTo(byte[] source, int offset, int count, byte[] expectedBytes)
    {
      try
      {
        var compressedBytes = new byte[count];
        Buffer.BlockCopy(source, offset, compressedBytes, 0, count);

        using var compressed = new MemoryStream(compressedBytes, writable: false);
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
        using var output = new MemoryStream();
        inflater.CopyTo(output);

        if (output.Length != expectedBytes.Length) return false;
        byte[] actualBytes = output.ToArray();
        for (int i = 0; i < actualBytes.Length; i++)
        {
          if (actualBytes[i] != expectedBytes[i]) return false;
        }

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

    private static uint ReadUInt32BigEndian(byte[] source, int offset)
    {
      return ((uint)source[offset] << 24)
        | ((uint)source[offset + 1] << 16)
        | ((uint)source[offset + 2] << 8)
        | source[offset + 3];
    }

    private static uint ComputeAdler32(byte[] bytes)
    {
      uint a = 1;
      uint b = 0;

      foreach (byte value in bytes)
      {
        a += value;
        if (a >= Adler32Modulus) a -= Adler32Modulus;

        b += a;
        if (b >= Adler32Modulus) b -= Adler32Modulus;
      }

      return (b << 16) | a;
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
