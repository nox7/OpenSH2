using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ObjectReader
  {
    protected readonly S2Object Object;

    public ObjectReader(S2Object obj)
    {
      Object = obj;
    }

    public virtual S2Object Read(BinaryReader reader)
    {
      throw new NotImplementedException();
    }
  }
}
