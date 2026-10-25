namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Saved actors only. No population or gameplay spawning is performed.</summary>
  public sealed class ActorPlacement
  {
    public int Id { get; internal set; }
    public string Type { get; internal set; }
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float HeadingDegrees { get; internal set; }
    public bool OnMap { get; internal set; }
    public int AnimationId { get; internal set; }
    public float AnimationTime { get; internal set; }
    public float AnimationRate { get; internal set; } = 1;
    public bool AnimationLoops { get; internal set; } = true;
  }
}
