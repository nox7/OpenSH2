using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader
{
  internal class S2MReaderUtils
  {
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
