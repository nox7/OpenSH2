using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class QuestFailedActionReader : ActionReader
  {
    public QuestFailedActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      QuestFailedAction obj = new();

      ReadActionHeader(reader);

      reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();

      obj.Quest = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
