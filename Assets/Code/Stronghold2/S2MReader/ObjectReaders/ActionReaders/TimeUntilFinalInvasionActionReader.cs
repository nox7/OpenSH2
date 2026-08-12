using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class TimeUntilFinalInvasionActionReader : ActionReader
  {
    public TimeUntilFinalInvasionActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      TimeUntilFinalInvasionAction obj = new();

      ReadActionHeader(reader);
      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
