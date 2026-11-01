using System;
using System.Collections.Generic;
using Assets.Code.Video.EmbeddedFFmpeg;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  internal readonly struct S2MWaterVideoBinding
  {
    public S2MWaterVideoBinding(Material material, string videoPath, float playbackSpeed)
    {
      Material = material ?? throw new ArgumentNullException(nameof(material));
      VideoPath = videoPath ?? throw new ArgumentNullException(nameof(videoPath));
      PlaybackSpeed = playbackSpeed;
    }
    public Material Material { get; }
    public string VideoPath { get; }
    public float PlaybackSpeed { get; }
  }

  /// <summary>Loops original Bink files on water materials, independently of UI presentation.</summary>
  internal sealed class S2MWaterVideoPlayer : MonoBehaviour
  {
    private readonly List<BinkTexturePlayer> players = new();
    private S2MWaterVideoBinding[] bindings;

    public void Initialize(IReadOnlyList<S2MWaterVideoBinding> sourceBindings)
    {
      if (sourceBindings == null) throw new ArgumentNullException(nameof(sourceBindings));
      ReleasePlayers();
      bindings = new S2MWaterVideoBinding[sourceBindings.Count];
      for (int i = 0; i < bindings.Length; i++) bindings[i] = sourceBindings[i];
      if (isActiveAndEnabled) StartPlayers();
    }

    private void StartPlayers()
    {
      if (bindings == null || players.Count != 0) return;
      try
      {
        foreach (var binding in bindings) players.Add(new BinkTexturePlayer(binding.VideoPath, binding.PlaybackSpeed));
      }
      catch { ReleasePlayers(); throw; }
    }

    private void Update()
    {
      for (int i = 0; i < players.Count; i++)
      {
        var player = players[i];
        // Preserve the old game-time water animation semantics, including pause and per-water speed.
        player.Tick(Time.deltaTime);
        if (bindings[i].Material != null && player.Texture != null)
          bindings[i].Material.SetTexture("_SurfaceTexture", player.Texture);
      }
    }

    private void ReleasePlayers()
    {
      for (int i = 0; i < players.Count; i++)
      {
        var player = players[i];
        if (bindings[i].Material != null && bindings[i].Material.GetTexture("_SurfaceTexture") == player.Texture)
          bindings[i].Material.SetTexture("_SurfaceTexture", Texture2D.blackTexture);
        player.Dispose();
      }
      players.Clear();
    }
    private void OnEnable() => StartPlayers();
    private void OnDisable() => ReleasePlayers();
    private void OnDestroy() => ReleasePlayers();
  }
}
