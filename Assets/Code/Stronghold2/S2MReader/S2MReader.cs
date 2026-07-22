using Assets.Code.Stronghold2.S2MReader.ObjectReaders;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Utilities;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.S2MReader
{
  internal class S2MReader
  {
    /// <summary>
    /// Stores the trailer marker which is AF1EFFFF in hex
    /// </summary>
    private const int TrailerMarker = -57681; // 0xAF1EFFFF as a signed int
    private string FilePath { get; set; }
    private S2MFile MapFile { get; set; }
    /// <summary>
    /// Map the object Id to its S2Object
    /// </summary>
    private readonly Dictionary<int, S2Object> Objects = new();
    /// <summary>
    /// Map the object type index (not Id) to the type name.
    /// </summary>
    private readonly Dictionary<int, string> Types = new();

    public S2MReader(string filePath)
    {
      FilePath = filePath;
      MapFile = new S2MFile();
    }

    public S2MFile ReadS2MFile()
    {
      using var stream = File.OpenRead(FilePath);
      using var reader = new BinaryReader(stream);

      // Read the map header, not compressed
      ReadHeader(reader);

      // Next, decompress the rest of the file (which has one-to-many zlib compressed segments)
      MapFile.DecompressedSegments = new ZLibDecompressor().DecompressAll(reader);

      Debug.Log("Number of decompressed segments: " + MapFile.DecompressedSegments.Count);

      for (int i = 0; i < MapFile.DecompressedSegments.Count; i++)
      {
        using var chunkStream = new MemoryStream(MapFile.DecompressedSegments[i].Bytes, writable: false);
        using var chunkReader = new BinaryReader(chunkStream);
        if (i == 0)
        {
          // This is the map header chunk

          // Skip unknown first ints
          chunkReader.ReadInt32();
          chunkReader.ReadInt32();

          while (true)
          {
            var obj = ReadObjectHeader(chunkReader);
            if (obj == null)
            {
              Debug.Log("Hit end of segment.");
              break;
            }

            Debug.Log("Reading " + obj.Type + " with Id " + obj.Id);

            ReadObject(chunkReader, obj);
          }
        }
      }

      return MapFile;
    }

    private void ReadHeader(BinaryReader reader)
    {
      // Unknown header marker. war_chapter1 has 2 here.
      int authorPresenceFlag = reader.ReadInt32();

      // Author data is only stored if the authorPresenceFlag is 2. If it is 1, then this is skipped
      // (or anything but 2, I guess)
      if (authorPresenceFlag == 2)
      {
        S2MReaderUtils.ReadFieldName(reader, "author");
        MapFile.Author = S2MReaderUtils.ReadUtf16String(reader);
      }

      S2MReaderUtils.ReadFieldName(reader, "type");
      string mapTypeString = S2MReaderUtils.ReadUtf16String(reader);

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

      S2MReaderUtils.ReadFieldName(reader, "balanced");
      MapFile.Balanced = reader.ReadInt32() == 1;

      S2MReaderUtils.ReadFieldName(reader, "lastsave");
      MapFile.LastSave = reader.ReadInt32().ToString();

      S2MReaderUtils.ReadFieldName(reader, "mapsize");
      MapFile.MapSize = reader.ReadInt32();

      S2MReaderUtils.ReadFieldName(reader, "maxplayers");
      MapFile.MaxPlayers = reader.ReadInt32();

      S2MReaderUtils.ReadFieldName(reader, "version");
      MapFile.Version = reader.ReadInt32();
    }

    /// <summary>
    /// Reads the header of an S2Object and returns that object.
    /// Other functions should read the rest of the object until the object-end trailer marker.
    /// 
    /// Returns null if the object header is the end of segment marker AD DE FF FF
    /// </summary>
    /// <returns></returns>
    private S2Object ReadObjectHeader(BinaryReader reader)
    {
      S2Object obj = new();

      // Read the object Id
      obj.Id = reader.ReadInt32();

      if (obj.Id == -8531)
      {
        return null;
      }

      // Read the type index
      int typeIndex = reader.ReadInt32();
      obj.TypeIndex = typeIndex;

      // Now, check if that type index has been mapped in a dictionary yet
      Types.TryGetValue(typeIndex, out string typeName);
      if (typeName == null)
      {
        // If it's null, then the next bytes we will read will be the length of the type name
        // followed by the type name itself
        typeName = S2MReaderUtils.ReadASCIIString(reader);

        // Register the typeName
        Types.Add(typeIndex, typeName);
      }

      // Read the type index again (it will max typeIndex) as it pads the type name
      reader.ReadInt32();

      // Read the parent type index
      int parentTypeIndex = reader.ReadInt32();

      // If this is not 0, then we handle it similarly to the typeIndex
      if (parentTypeIndex != 0)
      {
        Types.TryGetValue(parentTypeIndex, out string parentTypeName);
        if (parentTypeName == null)
        {
          parentTypeName = S2MReaderUtils.ReadASCIIString(reader);
          Types.Add(parentTypeIndex, parentTypeName);

          // To our best knowledge, there will never be a another "grandparent" type index and it will always be 0
          int grandparentTypeIndex = reader.ReadInt32();
          if (grandparentTypeIndex != 0)
          {
            throw new System.Exception($"Unexpected grandparent type index {grandparentTypeIndex} for object type '{typeName}' with parent type '{parentTypeName}'.");
          }
        }
      }

      obj.Type = typeName;

      return obj;
    }

    /// <summary>
    /// Takes an object-header-parsed S2Object and, using its Type, determines which reader to use to read the object.
    /// Once read, adds the object to the Objects dictionary by its Id.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="obj"></param>
    private S2Object ReadObject(BinaryReader reader, S2Object obj)
    {
      ObjectReader objReader;
      if (obj.Type == "MapHeader")
      {
        objReader = new MapHeaderReader(obj);
        var parsedObject = objReader.Read(reader);
        Objects.Add(obj.Id, parsedObject);
        return parsedObject;
      }
      else if (obj.Type == "EstateMarkers")
      {
        objReader = new EstateMarkersReader(obj);
        var parsedObject = objReader.Read(reader);
        Objects.Add(obj.Id, parsedObject);
        return parsedObject;
      }
      else if (obj.Type == "Scenario")
      {
        objReader = new ScenarioReader(obj);
        var parsedObject = objReader.Read(reader);
        Objects.Add(obj.Id, parsedObject);
        return parsedObject;
      }
      else if (obj.Type == "Mission")
      {
        objReader = new MissionReader(obj);
        var parsedObject = objReader.Read(reader);
        Objects.Add(obj.Id, parsedObject);
        return parsedObject;
      }

      return null;
    }
  }
}
