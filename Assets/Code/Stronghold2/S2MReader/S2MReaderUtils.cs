using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader
{
  internal class S2MReaderUtils
  {
    public const int TrailerMarker = -57681; // AF 1E FF FF
    public const int DataPayloadMarker = -15963; // A5 C1 FF FF

    /// <summary>
    /// Reads a binary field name, which consists of a length-prefixed UTF-16 string. The length is the number of characters, not bytes.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="expectedName"></param>
    /// <exception cref="InvalidDataException"></exception>
    public static void ReadFieldName(BinaryReader reader, string expectedName)
    {
      int nameLength = reader.ReadInt32();
      string name = Encoding.ASCII.GetString(ReadExactBytes(reader, nameLength));

      if (name != expectedName)
      {
        throw new InvalidDataException($"Expected S2M header field '{expectedName}' but found '{name}' at offset {reader.BaseStream.Position}.");
      }
    }

    /// <summary>
    /// Reads the length of the upcoming ASCII string and then the string itself
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static string ReadASCIIString(BinaryReader reader)
    {
      int lengthOfString = reader.ReadInt32();
      byte[] bytes = ReadExactBytes(reader, lengthOfString);
      return Encoding.ASCII.GetString(bytes);
    }

    /// <summary>
    /// Reads a UTF-16 string which is prefixed with its length in characters (not bytes).
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static string ReadUtf16String(BinaryReader reader)
    {
      int characterCount = reader.ReadInt32();
      byte[] bytes = ReadExactBytes(reader, characterCount * 2);

      return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// Reads a size value and then a byte array of that size where each value to read is an int32.
    /// </summary>
    /// <param name="includesTrailerIdMarker">Some specs have the lengthOfBytesInList include the 4 byte object trailer marker. It's stupid.</param>
    /// <returns></returns>
    /// <exception cref="EndOfStreamException"></exception>
    public static List<int> ReadListOfInts(
      BinaryReader reader,
      bool includesTrailerIdMarker
      )
    {
      List<int> values = new();
      int lengthOfBytesInList = reader.ReadInt32();
      int numInList = reader.ReadInt32();
      for (int i = 0; i < numInList; i++)
      {
        values.Add(reader.ReadInt32());
      }

      // If we read fewer bytes than the length of the list, then we need to skip the remaining bytes
      // Some lists (like in the Scenario object) have an extra 4 bytes at the end that we don't care about, so we just skip them
      int bytesRead = numInList * 4;

      if (lengthOfBytesInList - bytesRead == 4 && includesTrailerIdMarker)
      {
        // Do nothing. 4 bytes remain to be read, but the spec for this read says they're just the object
        // trailer marker, so we don't need to read them.
        // The reader that is calling this function will read them.
      }
      else
      {
        if (bytesRead < lengthOfBytesInList)
        {
          int bytesToSkip = lengthOfBytesInList - bytesRead;
          reader.BaseStream.Seek(bytesToSkip, SeekOrigin.Current);
        }
      }

      return values;
    }

    public static byte[] ReadExactBytes(BinaryReader reader, int count)
    {
      byte[] bytes = reader.ReadBytes(count);

      if (bytes.Length != count)
      {
        throw new EndOfStreamException($"Expected {count} bytes but only read {bytes.Length}.");
      }

      return bytes;
    }
  }
}
