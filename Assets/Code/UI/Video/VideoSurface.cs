using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Assets.Code.UI.Video
{
  /// <summary>A UI Toolkit image backed by a native video player. Owns its player and render target.</summary>
  internal sealed class VideoSurface : IVideoSurface
  {
    private readonly Image image;
    private readonly GameObject host;
    private readonly VideoPlayer player;
    private RenderTexture texture;
    private bool disposed;
    private bool playRequested;
    private bool firstFrame;
    private float prepareStarted;
    private float playStarted;
    public bool IsFinished { get; private set; }
    public event Action FirstFrame;

    public VideoSurface(Transform parent, Image image, string path, bool loop, bool autoplay = true)
    {
      this.image = image;
      image.image = null;
      image.scaleMode = ScaleMode.ScaleToFit;
      host = new GameObject("Video: " + Path.GetFileNameWithoutExtension(path));
      host.transform.SetParent(parent, false);
      player = host.AddComponent<VideoPlayer>();
      player.playOnAwake = false;
      player.source = VideoSource.Url;
      player.renderMode = VideoRenderMode.RenderTexture;
      player.isLooping = loop;
      player.waitForFirstFrame = true;
      player.skipOnDrop = false;
      player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
      player.audioOutputMode = VideoAudioOutputMode.Direct;
      player.controlledAudioTrackCount = 1;
      player.EnableAudioTrack(0, true);
      player.SetDirectAudioVolume(0, 1f);
      player.prepareCompleted += Prepared;
      player.loopPointReached += ReachedEnd;
      player.errorReceived += Failed;
      player.sendFrameReadyEvents = true;
      player.frameReady += FrameReady;
      playRequested = autoplay;
      prepareStarted = Time.realtimeSinceStartup;
      playStarted = prepareStarted;
      if (!File.Exists(path))
      {
        Debug.LogWarning($"UI video is missing: {path}");
        IsFinished = true;
        return;
      }
      player.url = new Uri(path).AbsoluteUri;
      player.Prepare();
    }

    private void Prepared(VideoPlayer source)
    {
      if (disposed) return;
      texture = new RenderTexture((int)source.width, (int)source.height, 0, RenderTextureFormat.ARGB32);
      texture.name = host.name;
      texture.Create();
      source.targetTexture = texture;
      if (playRequested) source.Play();
    }

    private void FrameReady(VideoPlayer source, long frame)
    {
      if (disposed || firstFrame) return;
      firstFrame = true;
      image.image = texture;
      // Further frames update the same GPU texture; no per-frame texture upload is necessary.
      source.sendFrameReadyEvents = false;
      FirstFrame?.Invoke();
    }

    public void Play()
    {
      if (disposed || IsFinished) return;
      playRequested = true;
      playStarted = Time.realtimeSinceStartup;
      if (player.isPrepared) player.Play();
    }

    public void Tick()
    {
      if (disposed || IsFinished) return;
      if (!player.isPrepared && Time.realtimeSinceStartup - prepareStarted > 30f)
        Failed(player, "Preparation timed out.");
      else if (playRequested && !firstFrame && Time.realtimeSinceStartup - playStarted > 30f)
        Failed(player, "Timed out waiting for the first video frame.");
    }

    private void ReachedEnd(VideoPlayer source)
    {
      if (!source.isLooping) IsFinished = true;
    }

    private void Failed(VideoPlayer source, string message)
    {
      Debug.LogWarning($"UI video failed ({source.url}): {message}");
      source.Stop();
      IsFinished = true;
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      player.prepareCompleted -= Prepared;
      player.loopPointReached -= ReachedEnd;
      player.errorReceived -= Failed;
      player.frameReady -= FrameReady;
      player.Stop();
      player.targetTexture = null;
      if (image.image == texture) image.image = null;
      if (texture != null)
      {
        texture.Release();
        UnityEngine.Object.Destroy(texture);
      }
      UnityEngine.Object.Destroy(host);
    }
  }
}
