using Assets.Code.Stronghold2.S2MReader.Resources;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Terrain textures loaded from a legally installed Stronghold 2 copy.</summary>
  public sealed class S2MTerrainTextureData
  {
    public S2MFile Map { get; internal set; }
    public IReadOnlyDictionary<byte, Texture2D> Textures { get; internal set; }
    public IReadOnlyCollection<byte> UnsupportedMaterialIds { get; internal set; }
  }
}
