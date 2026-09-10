namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>An object whose type-specific payload has not been decoded yet.</summary>
  public class RawS2Object : S2Object
  {
    /// <summary>Payload bytes excluding the AF 1E FF FF object terminator.</summary>
    public byte[] Payload { get; set; }
  }
}
