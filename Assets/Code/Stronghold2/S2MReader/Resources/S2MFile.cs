using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Utilities;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class S2MFile
  {
    public string SourcePath { get; internal set; }
    public string Author { get; set; }
    public MapType MapType { get; set; }
    public bool Balanced { get; set; }
    public string LastSave { get; set; }
    public int MapSize { get; set; }
    public int MaxPlayers { get; set; }
    public int Version { get; set; }
    public IReadOnlyDictionary<string, string> StringOptions { get; internal set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> IntegerOptions { get; internal set; } = new Dictionary<string, int>();
    public IReadOnlyList<S2Object> MapHeaderObjects { get; internal set; } = Array.Empty<S2Object>();
    public IReadOnlyList<S2Object> RadarMapObjects { get; internal set; } = Array.Empty<S2Object>();
    public IReadOnlyList<S2Object> S2GameObjects { get; internal set; } = Array.Empty<S2Object>();
    public RadarMap RadarMap { get; internal set; }
    public EstateLayer RadarEstateLayer { get; internal set; }
    public Landscape Landscape { get; internal set; }
    public WaterLayer WaterLayer => Landscape?.Water;
    public HeightLayer HeightLayer { get; internal set; }
    public Forest Forest { get; internal set; }
    /// <summary>Complete zlib payloads in their original S2M file order.</summary>
    public IReadOnlyList<ZLibDecompressedSegment> DecompressedSegments { get; internal set; } = Array.Empty<ZLibDecompressedSegment>();
  }
}
