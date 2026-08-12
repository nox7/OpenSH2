using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class QuestActionReader : ActionReader
  {
    public QuestActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      QuestAction obj = new();

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
