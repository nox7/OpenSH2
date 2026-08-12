using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class TriggerReader : ObjectReader
  {
    public TriggerReader(S2Object obj) : base(obj)
    {

    }

    /// <summary>
    /// Reads the four 4-byte segments and then the null padding before the data payload marker.
    /// </summary>
    /// <param name="reader"></param>
    public void ReadTriggerHeader(BinaryReader reader)
    {
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadByte();
    }
  }
}
