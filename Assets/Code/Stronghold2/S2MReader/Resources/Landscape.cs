namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public sealed class Landscape : S2Object
  {
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int LogicalMapSize { get; internal set; }
    /// <summary>The first Landscape float grid, transposed to row-major order.</summary>
    public float[] Heights { get; internal set; }
    public WaterLayer Water { get; internal set; }
    /// <summary>Complete type-specific payload excluding the object trailer.</summary>
    public byte[] RawPayload { get; internal set; }
  }
}
