namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Render-placement data for saved actors and synthetic adapters; live gameplay state belongs to Simulation.</summary>
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
    public int PlacedType { get; internal set; }
    public byte NavigationLayer { get; internal set; }
    public short TileX { get; internal set; }
    public short TileZ { get; internal set; }
    /// <summary>Low byte of the serialized ContextLayer entry; 255 means unavailable.</summary>
    public byte SavedWalkingClass { get; internal set; } = byte.MaxValue;
    public int PendingPlayer { get; internal set; } = -1;
    public int EstateId { get; internal set; } = -1;
    /// <summary>Low word: clothing color choice; high word: crest choice. -1 is unassigned.</summary>
    public int SavedLivery { get; internal set; } = -1;
    public bool HasKnightState { get; internal set; }
    public bool SavedMounted { get; internal set; }
    public uint SavedHorseReference { get; internal set; }
    public float HorseAnimationTime { get; internal set; }
    /// <summary>Initial campaign placement policy, kept separate from serialized mount state.</summary>
    public bool MountAtMapStart { get; internal set; }
    public bool IsMounted => SavedMounted || MountAtMapStart;
  }
}
