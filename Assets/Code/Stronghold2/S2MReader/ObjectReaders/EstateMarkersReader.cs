using System;
using System.IO;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;
namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
    internal sealed class EstateMarkersReader : ObjectReader
    {
        public EstateMarkersReader(S2Object obj) : base(obj) { }
        public override S2Object Read(BinaryReader reader)
        {
            var payload = ReadPayloadToTrailer(reader); var result = new EstateMarkers();
            using var r = new BinaryReader(new MemoryStream(payload, false));
            int bytes = r.ReadInt32(), count = r.ReadInt32();
            if (count < 0 || count > 32 || bytes != 4 + count * 12 || bytes > payload.Length - 4) throw new InvalidDataException("Invalid counted estate marker table.");
            for (int estate = 0; estate < count; estate++)
            {
                int packed = r.ReadInt32(), x = r.ReadInt16(), z = r.ReadInt16(), kind = r.ReadInt32();
                if (kind < 0 || kind > 2) throw new InvalidDataException("Unknown estate marker kind.");
                result.Markers.Add(new EstateMarkerFlag { EstateId = estate, PackedLocation = packed, X = x, Y = z, Type = (EstateType)kind });
            }
            return result;
        }
    }
}
