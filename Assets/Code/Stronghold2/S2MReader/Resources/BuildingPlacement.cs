namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Common Building metadata, independent of a building's decoded mesh/gameplay state.</summary>
  public class BuildingPlacement : S2Object
  {
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    /// <summary>Map-local saved reference, distinct from the outer serialized object's Id.</summary>
    public uint SavedBuildingReference { get; internal set; }
    /// <summary>Raw saved reference to a related building; zero means no saved link.</summary>
    public uint LinkedBuildingReference { get; internal set; }
    /// <summary>Resolved reciprocal kitchen/pantry or courthouse/dungeon partner, when present.</summary>
    public int? LinkedBuildingId { get; internal set; }
    /// <summary>
    /// Original map-recorded placement footprint in cells, already rotated by
    /// RotationDegrees. Retain the full reserved area, including clearance beyond
    /// the visible mesh, for future building-placement checks and debugging.
    /// This is separate from mesh collision and does not classify unit walkability.
    /// </summary>
    public int FootprintWidth { get; internal set; }
    /// <summary>Other dimension of the original reserved placement footprint; never trimmed to mesh bounds.</summary>
    public int FootprintHeight { get; internal set; }
    public float MapX => RawX / 1024f;
    public float MapZ => RawZ / 1024f;
    internal int BuildingTypeIndex { get; set; }
    internal bool RegistersBuildingType { get; set; }
  }
}
