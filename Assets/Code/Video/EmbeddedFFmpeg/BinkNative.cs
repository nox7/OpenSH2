using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Assets.Code.Video.EmbeddedFFmpeg
{
  // Only this class knows the native ABI. FFmpeg structs never cross into managed code.
  internal static class BinkNative
  {
    private const string Library = "OpenSH2.Bink";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr osh2_bink_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path,
      out int width, out int height, out double frameSeconds, StringBuilder error, int errorCapacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int osh2_bink_read(IntPtr decoder, [Out] byte[] rgba, int capacity,
      out double seconds, StringBuilder error, int errorCapacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int osh2_bink_rewind(IntPtr decoder, StringBuilder error, int errorCapacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void osh2_bink_close(IntPtr decoder);
  }
}
