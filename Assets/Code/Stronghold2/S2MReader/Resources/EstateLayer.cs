namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class EstateLayer : S2Object
  {
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>One estate identifier byte per map cell.</summary>
    public byte[] EstateIds { get; set; }
  }
}
