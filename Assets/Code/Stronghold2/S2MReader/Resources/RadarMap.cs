namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class RadarMap : S2Object
  {
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Four bytes per pixel. The exact channel meaning is not confirmed yet.</summary>
    public byte[] PixelBytes { get; set; }
    public int UnknownAfterPixels1 { get; set; }
    public int UnknownAfterPixels2 { get; set; }
    public int EstateLayerObjectId { get; set; }
  }
}
