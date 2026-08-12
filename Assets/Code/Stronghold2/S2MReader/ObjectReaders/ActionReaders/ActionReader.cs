using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class ActionReader : ObjectReader
  {
    public ActionReader(S2Object obj) : base(obj)
    {

    }

    /// <summary>
    /// Reads the two 4-byte segments and then the null padding before the data payload marker.
    /// </summary>
    /// <param name="reader"></param>
    public void ReadActionHeader(BinaryReader reader)
    {
      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadByte();
    }
  }
}
