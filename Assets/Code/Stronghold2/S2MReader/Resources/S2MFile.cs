using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Utilities;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class S2MFile
  {
    public string Author { get; set; }
    public MapType MapType { get; set; }
    public bool Balanced { get; set; }
    public string LastSave { get; set; }
    public int MapSize { get; set; }
    public int MaxPlayers { get; set; }
    public int Version { get; set; }
    /// <summary>Complete zlib payloads in their original S2M file order.</summary>
    public IReadOnlyList<ZLibDecompressedSegment> DecompressedSegments { get; internal set; } = Array.Empty<ZLibDecompressedSegment>();
  }
}
