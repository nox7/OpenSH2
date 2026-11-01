using System;
using System.Diagnostics;
using UnityEngine;

namespace Assets.Code.Video.EmbeddedFFmpeg
{
  /// <summary>Main-thread, silent Bink texture playback driven by a caller-supplied delta time.</summary>
  internal sealed class BinkTexturePlayer : IDisposable
  {
    private readonly BinkFrameDecoder decoder;
    private readonly string path;
    private readonly Stopwatch preparation = Stopwatch.StartNew();
    private BinkFrame pending;
    private bool started, disposed;
    private double seconds;
    public Texture2D Texture { get; private set; }
    public double PresentedSeconds { get; private set; }
    public int CompletedLoops { get; private set; }
    public long PresentedFrames { get; private set; }
    public string Error { get; private set; }
    public float PlaybackSpeed { get; }

    public BinkTexturePlayer(string path, float playbackSpeed)
    {
      if (float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed) || playbackSpeed < 0)
        throw new ArgumentOutOfRangeException(nameof(playbackSpeed));
      this.path = path;
      PlaybackSpeed = playbackSpeed;
      decoder = new BinkFrameDecoder(path, true);
    }

    public void Tick(float deltaTime)
    {
      if (disposed || Error != null) return;
      if (decoder.Failure != null) { Fail(decoder.Failure.Message); return; }
      if (!started && preparation.Elapsed.TotalSeconds > 30) { Fail("Preparation timed out."); return; }
      var info = decoder.Info;
      if (info == null) return;
      if (started && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime))
        seconds += Math.Max(0, deltaTime) * PlaybackSpeed;
      if (pending == null && !decoder.TryTake(out pending)) return;
      if (!started)
      {
        // A zero-speed player still shows frame zero. Loading time never advances its clock.
        Texture = new Texture2D(info.Width, info.Height, TextureFormat.RGBA32, false, false)
        { name = "Bink terrain: " + System.IO.Path.GetFileName(path), wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        started = true;
      }
      BinkFrame selected = null;
      while (pending != null && pending.Seconds <= seconds)
      {
        if (selected != null) decoder.Recycle(selected);
        selected = pending;
        pending = decoder.TryTake(out var next) ? next : null;
      }
      if (selected == null) return;
      Texture.LoadRawTextureData(selected.Pixels);
      Texture.Apply(false, false);
      PresentedSeconds = selected.Seconds;
      CompletedLoops = selected.Loop;
      PresentedFrames++;
      decoder.Recycle(selected);
    }

    private void Fail(string message)
    {
      Error = message;
      decoder.Dispose();
      UnityEngine.Debug.LogWarning($"Embedded Bink terrain playback failed ({path}): {message}");
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      decoder.Dispose();
      pending = null;
      if (Texture != null)
      {
        if (Application.isPlaying) UnityEngine.Object.Destroy(Texture);
        else UnityEngine.Object.DestroyImmediate(Texture);
      }
    }
  }
}
