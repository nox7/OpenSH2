using System;
using System.Diagnostics;
using Assets.Code.Video.EmbeddedFFmpeg;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Video.EmbeddedFFmpeg
{
  /// <summary>Adapts bounded, worker-decoded Bink frames to a UI Toolkit image on Unity's main thread.</summary>
  internal sealed class BinkVideoSurface : IVideoSurface
  {
    private readonly Image image;
    private readonly BinkFrameDecoder decoder;
    private readonly Stopwatch clock = new();
    private readonly Stopwatch preparation = Stopwatch.StartNew();
    private Texture2D texture;
    private BinkFrame pending;
    private bool playRequested;
    private bool disposed;
    private double lastSeconds;
    private bool firstFrame;
    public bool IsFinished { get; private set; }
    public int CompletedLoops { get; private set; }
    public long PresentedFrames { get; private set; }
    public string Error { get; private set; }
    public event Action FirstFrame;

    public BinkVideoSurface(Image image, string path, bool loop, bool autoplay = true)
    {
      this.image = image;
      image.image = null;
      image.scaleMode = ScaleMode.ScaleToFit;
      playRequested = autoplay;
      decoder = new BinkFrameDecoder(path, loop);
    }

    public void Play()
    {
      if (!playRequested) preparation.Restart();
      playRequested = true;
    }

    public void Tick()
    {
      if (disposed || IsFinished) return;
      if (decoder.Failure != null) { Fail(decoder.Failure.Message); return; }
      if (!firstFrame && preparation.Elapsed.TotalSeconds > 30 && playRequested)
      { Fail("Timed out waiting for a decoded Bink frame."); return; }
      var info = decoder.Info;
      if (info == null || !playRequested) return;
      if (pending == null && !decoder.TryTake(out pending))
      {
        if (firstFrame && decoder.HasEnded && clock.Elapsed.TotalSeconds >= lastSeconds + info.FrameSeconds)
          IsFinished = true;
        return;
      }
      if (texture == null)
      {
        texture = new Texture2D(info.Width, info.Height, TextureFormat.RGBA32, false, false);
        texture.name = "OpenSH2 direct Bink RGBA";
        texture.wrapMode = TextureWrapMode.Clamp;
        // This texture changes in place; exclude it from UI Toolkit's static image atlas.
        texture.filterMode = FilterMode.Point;
        clock.Start();
      }
      BinkFrame selected = null;
      while (pending != null && pending.Seconds <= clock.Elapsed.TotalSeconds)
      {
        if (selected != null) decoder.Recycle(selected);
        selected = pending;
        pending = decoder.TryTake(out var next) ? next : null;
      }
      if (selected != null)
      {
        texture.LoadRawTextureData(selected.Pixels);
        texture.Apply(false, false);
        lastSeconds = selected.Seconds;
        CompletedLoops = selected.Loop;
        PresentedFrames++;
        decoder.Recycle(selected);
        if (!firstFrame)
        {
          firstFrame = true;
          image.image = texture;
          FirstFrame?.Invoke();
        }
      }
      if (pending == null && decoder.HasEnded && clock.Elapsed.TotalSeconds >= lastSeconds + info.FrameSeconds)
        IsFinished = true;
    }

    private void Fail(string message)
    {
      Error = message;
      IsFinished = true;
      UnityEngine.Debug.LogWarning("Embedded Bink playback failed: " + message);
      decoder.Dispose();
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      decoder.Dispose();
      pending = null;
      if (image.image == texture) image.image = null;
      if (texture != null) UnityEngine.Object.Destroy(texture);
      clock.Stop();
    }
  }
}
