using System;
using System.Collections.Generic;
using System.Text;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal static class WalkingLayerReader
  {
    public static WalkingLayer Read(IReadOnlyList<S2Object> objects)
    {
      foreach(var obj in objects)
      {
        if(obj.Type!="ContextLayer" || !(obj is RawS2Object raw))continue;
        byte[] b=raw.Payload; int p=0;
        while(p<=b.Length-8)
        {
          int field=BitConverter.ToInt32(b,p);p+=4;
          if(field==S2MReaderUtils.DataPayloadMarker)
          {
            if(p>b.Length-4 || BitConverter.ToInt32(b,p)!=obj.TypeIndex)break;p+=4;
            if(p>b.Length-4)break;
            int n=BitConverter.ToInt32(b,p);
            if(n==obj.Type.Length && p+4+n<=b.Length && Encoding.ASCII.GetString(b,p+4,n)==obj.Type)p+=4+n;
            if(p>b.Length-8)break;field=BitConverter.ToInt32(b,p);p+=4;
          }
          int length=BitConverter.ToInt32(b,p);p+=4;
          if(length<0 || length>b.Length-p)break;
          if(field>=0 && field<=3 && length>=12 && (length-12)%6==0)
          {
            int count=(length-12)/6,side=(int)Math.Sqrt(count),stride=4+count*2;
            if(count>0 && side*side==count && BitConverter.ToInt32(b,p)==count
              && BitConverter.ToInt32(b,p+stride)==count && BitConverter.ToInt32(b,p+stride*2)==count)
            {
              var layers=new ushort[3][];
              for(int layer=0;layer<3;layer++)
              {
                layers[layer]=new ushort[count];
                for(int i=0;i<count;i++)layers[layer][i]=BitConverter.ToUInt16(b,p+stride*layer+4+i*2);
              }
              return new WalkingLayer { Width=side,Height=side,Layers=layers };
            }
          }
          p+=length;
        }
      }
      return null;
    }
  }
}
