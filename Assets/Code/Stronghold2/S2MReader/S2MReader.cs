using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader
{
  internal class S2MReader
  {
    private string FilePath { get; set; }
    private S2MFile MapFile { get; set; }

    public S2MReader(string filePath)
    {
      FilePath = filePath;
      MapFile = new S2MFile();
    }

    public S2MFile ReadS2MFile()
    {
      using var stream = File.OpenRead(FilePath);
      using var reader = new BinaryReader(stream);

      ReadHeader(reader);
      return MapFile;
    }

    private void ReadHeader(BinaryReader reader)
    {
      // Unknown header marker. war_chapter1 has 2 here.
      reader.ReadInt32();

      ReadFieldName(reader, "author");
      MapFile.Author = ReadUtf16String(reader);

      ReadFieldName(reader, "type");
      string mapTypeString = ReadUtf16String(reader);

      if (mapTypeString == "warcampaign")
      {
        MapFile.MapType = Enums.MapType.WarCampaign;
      }
      else if (mapTypeString == "kingmaker")
      {
        MapFile.MapType = Enums.MapType.Kingmaker;
      }
      else if (mapTypeString == "peacecampaign")
      {
        MapFile.MapType = Enums.MapType.PeaceCampaign;
      }
      else if (mapTypeString == "freebuild")
      {
        MapFile.MapType = Enums.MapType.FreeBuild;
      }
      else
      {
        throw new InvalidDataException($"Unknown S2M map type '{mapTypeString}'.");
      }

      // Read random "04 00 00 00"
      reader.ReadInt32();

      ReadFieldName(reader, "balanced");
      MapFile.Balanced = reader.ReadInt32() == 1;

      ReadFieldName(reader, "lastsave");
      MapFile.LastSave = reader.ReadInt32().ToString();

      ReadFieldName(reader, "maxplayers");
      MapFile.MaxPlayers = reader.ReadInt32();

      ReadFieldName(reader, "version");
      MapFile.Version = reader.ReadInt32();
    }

    private static void ReadFieldName(BinaryReader reader, string expectedName)
    {
      int nameLength = reader.ReadInt32();
      string name = Encoding.ASCII.GetString(ReadExactBytes(reader, nameLength));

      if (name != expectedName)
      {
        throw new InvalidDataException($"Expected S2M header field '{expectedName}' but found '{name}' at offset {reader.BaseStream.Position}.");
      }
    }

    private static string ReadUtf16String(BinaryReader reader)
    {
      int characterCount = reader.ReadInt32();
      byte[] bytes = ReadExactBytes(reader, characterCount * 2);

      return Encoding.Unicode.GetString(bytes);
    }

    private static byte[] ReadExactBytes(BinaryReader reader, int count)
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
