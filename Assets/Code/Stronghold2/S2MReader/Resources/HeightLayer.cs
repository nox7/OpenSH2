namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class HeightLayer : S2Object
  {
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>The first, confirmed float32 height plane in row-major file order.</summary>
    public float[] Heights { get; set; }
    public int AdditionalSamplesPerCell { get; set; }
    /// <summary>
    /// Additional float32 height-like samples in cell-major order. For a cell
    /// index i and sample s, use i * AdditionalSamplesPerCell + s.
    /// </summary>
    public float[] AdditionalHeightSamples { get; set; }
    /// <summary>Bytes after the additional sample block, excluding the object terminator.</summary>
    public byte[] RemainingPayload { get; set; }
  }
}
