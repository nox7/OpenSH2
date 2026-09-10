using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class RawObjectReader : ObjectReader
  {
    public RawObjectReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      return new RawS2Object { Payload = ReadPayloadToTrailer(reader) };
    }
  }
}
