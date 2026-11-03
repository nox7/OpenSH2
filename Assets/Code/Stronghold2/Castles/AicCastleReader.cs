using System;
using System.IO;
namespace Assets.Code.Stronghold2.Castles
{
    public static class AicCastleReader
    {
        public const int TailByteLength=4*(50+1+240+200+240);
        public const int MaximumItemCount=262144,MaximumFileBytes=16*1024*1024;
        public static AicCastleBlueprint ReadFile(string path)
        {
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(file.Length>MaximumFileBytes)throw new InvalidDataException("AIC exceeds the supported file size: "+path);
            var bytes=new byte[(int)file.Length];int offset=0;
            while(offset<bytes.Length){int n=file.Read(bytes,offset,bytes.Length-offset);if(n==0)throw new EndOfStreamException("AIC changed or ended during reading: "+path);offset+=n;}
            return Read(bytes,Path.GetFileName(path));
        }
        public static AicCastleBlueprint Read(ReadOnlySpan<byte> bytes,string key="memory.aic")
        {
            if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("A blueprint key is required.",nameof(key));
            if(bytes.Length<4 || bytes.Length>MaximumFileBytes)throw new InvalidDataException("Invalid AIC length: "+bytes.Length);
            uint count=Word(bytes,0);
            long required=4L+8L*count+TailByteLength;
            if(count>MaximumItemCount || required>bytes.Length)throw new InvalidDataException($"Truncated or excessive AIC item count {count}; needs {required} bytes, has {bytes.Length}.");
            var items=new AicBuildItem[(int)count];int at=4;
            for(int i=0;i<items.Length;i++,at+=8)items[i]=new AicBuildItem(Word(bytes,at),Word(bytes,at+4));
            var values=Numbers(bytes,ref at,50);int unknown=unchecked((int)Word(bytes,at));at+=4;
            var lists=Points(bytes,ref at,240);var groups=Points(bytes,ref at,200);var flags=Numbers(bytes,ref at,240);
            // Longer native files exist. Preserve extension bytes without assigning invented semantics.
            var trailing=bytes.Slice(at).ToArray();
            return new AicCastleBlueprint(key,bytes.Length,items,values,unknown,lists,groups,flags,trailing);
        }
        private static uint Word(ReadOnlySpan<byte> bytes,int at)=>(uint)bytes[at]|(uint)bytes[at+1]<<8|(uint)bytes[at+2]<<16|(uint)bytes[at+3]<<24;
        private static int[] Numbers(ReadOnlySpan<byte> bytes,ref int at,int count)
        {var result=new int[count];for(int i=0;i<count;i++,at+=4)result[i]=unchecked((int)Word(bytes,at));return result;}
        private static AicPoint[] Points(ReadOnlySpan<byte> bytes,ref int at,int count)
        {var result=new AicPoint[count];for(int i=0;i<count;i++,at+=4)result[i]=new AicPoint(unchecked((int)Word(bytes,at)));return result;}
    }
}
