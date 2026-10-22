using Assets.Code.Stronghold2.S2MReader.Resources;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class BridgesReader : ObjectReader
  {
    public BridgesReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      if (payload.Length == 0) return new Bridges();
      if (payload.Length < 8) throw new InvalidDataException("Bridges array header is incomplete.");
      using var stream = new MemoryStream(payload, writable: false);
      using var data = new BinaryReader(stream);
      int length = data.ReadInt32();
      int count = data.ReadInt32();
      if (length != payload.Length - 4 || count < 0 || (long)count * 26 != payload.Length - 8)
        throw new InvalidDataException("Bridges length/count does not match its 26-byte records.");
      var spans = new List<BridgeSpan>(count);
      for (int i = 0; i < count; i++)
      {
        spans.Add(new BridgeSpan
        {
          State = data.ReadByte(), Axis = data.ReadByte(),
          StartX = data.ReadInt32(), StartZ = data.ReadInt32(),
          EndX = data.ReadInt32(), EndZ = data.ReadInt32(),
          Unknown1 = data.ReadInt32(), Unknown2 = data.ReadInt32()
        });
      }
      return new Bridges { Spans = spans, RawPayload = payload };
    }
  }
}
