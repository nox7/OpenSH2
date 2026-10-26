using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>ContextLayer's three packed walking-class grids, retaining their backing stride.</summary>
  public sealed class WalkingLayer
  {
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public ushort[][] Layers { get; internal set; }
    public ushort GetValue(int x,int z,int layer=0)
    {
      if(x<0 || z<0 || x>=Width || z>=Height || layer<0 || layer>=Layers.Length)throw new ArgumentOutOfRangeException();
      return Layers[layer][z*Width+x];
    }
    public byte GetClass(int x,int z,int layer=0) => (byte)GetValue(x,z,layer);
    public bool IsGroundSurface(int x,int z)
    {
      byte value=GetClass(x,z);
      // Open ground, authored building/bridge floors, and gate passages.
      return value==0 || value==4 || value==10;
    }
  }
}
