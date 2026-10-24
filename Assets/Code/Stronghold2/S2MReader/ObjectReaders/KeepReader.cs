using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class KeepReader : ObjectReader
  {
    public KeepReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      if (BuildingPlacementReader.TryRead(payload, out BuildingPlacement placement))
      {
        return new Keep
        {
          Size = Object.Type switch
          {
            "Keep1" => KeepSize.Wooden,
            "Keep2" => KeepSize.Small,
            "Keep3" => KeepSize.Medium,
            "Keep4" => KeepSize.Large,
            _ => throw new InvalidDataException($"Unsupported keep type {Object.Type}.")
          },
          RawX = placement.RawX, RawY = placement.RawY, RawZ = placement.RawZ,
          RotationDegrees = placement.RotationDegrees, FootprintWidth = placement.FootprintWidth,
          FootprintHeight = placement.FootprintHeight, SavedBuildingReference = placement.SavedBuildingReference,
          RawPayload = payload
        };
      }
      throw new InvalidDataException("Keep has no supported counted Building transform block.");
    }
  }
}
