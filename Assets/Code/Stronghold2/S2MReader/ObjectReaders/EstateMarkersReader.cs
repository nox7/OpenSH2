using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class EstateMarkersReader : ObjectReader
  {
    public EstateMarkersReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      EstateMarkers obj = new();

      // Read first 4 bytes. 84 01 00 00
      reader.ReadInt32();

      // Read next 4 bytes (20 00 00 00)
      reader.ReadInt32();

      // Read next 4 00 bytes
      reader.ReadInt32();

      // Read next 4 00 bytes
      reader.ReadInt32();

      // Read next 4 00 bytes
      reader.ReadInt32();

      // Read single 00 byte
      reader.ReadByte();

      // Now we will begin reading the flag locations and their marker type
      // There are 16 flags to read
      for (int i = 0; i < 16; i++)
      {
        EstateMarkerFlag flag = new();
        flag.X = reader.ReadByte();
        flag.Y = reader.ReadByte();

        // Read null separator byte
        reader.ReadByte();

        flag.X2 = reader.ReadInt16();
        flag.Y2 = reader.ReadInt16();

        int type = reader.ReadInt32();
        if (type == 1)
        {
          flag.Type = Enums.EstateType.Estate;
        }
        else if (type == 2)
        {
          flag.Type = Enums.EstateType.Castle;
        }

        // Read null terminator byte (00)
        reader.ReadByte();
        obj.Markers.Add(flag);
      }

      // Read 179 unknown bytes
      // We don't know what these do
      reader.ReadBytes(179);

      // Read 4 object-terminator bytes (AF1EFFFF)
      reader.ReadInt32();

      return obj;
    }
  }
}
