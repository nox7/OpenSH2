using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class VineRotActionReader : ActionReader
  {
    public VineRotActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      VineRotAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, true);
      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
