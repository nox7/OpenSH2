using System;
using Assets.Code.Video.EmbeddedFFmpeg;
using UnityEngine;

namespace Assets.Code.UI.Video.EmbeddedFFmpeg
{
  /// <summary>Owns Unity audio objects and schedules PCM against the DSP clock shared by video.</summary>
  internal sealed class BinkAudioPlayback : IDisposable
  {
    private readonly BinkAudioDecoder decoder;
    private readonly Transform parent;
    private GameObject host;
    private AudioSource source;
    private AudioClip clip;
    private double startTime;
    private bool started, disposed;
    public bool IsReady => decoder.IsReady;
    public bool HasAudio => decoder.HasAudio;
    public Exception Failure => decoder.Failure;
    public double PlaybackSeconds => started ? Math.Max(0, AudioSettings.dspTime - startTime) : 0;
    public bool IsFinished => !HasAudio || (decoder.Ended && PlaybackSeconds >= (double)decoder.DecodedFrames / BinkAudioDecoder.SampleRate);

    public BinkAudioPlayback(Transform parent, string path)
    {
      this.parent = parent;
      decoder = new BinkAudioDecoder(path);
    }

    public void Start()
    {
      if (started || !HasAudio) return;
      host = new GameObject("Bink streaming audio");
      host.transform.SetParent(parent, false);
      source = host.AddComponent<AudioSource>();
      source.playOnAwake = false;
      source.spatialBlend = 0;
      source.dopplerLevel = 0;
      source.ignoreListenerPause = true;
      // A short streaming clip loops over a forward-only PCM stream, not over the movie's audio.
      clip = AudioClip.Create("OpenSH2 Bink PCM", BinkAudioDecoder.SampleRate, BinkAudioDecoder.Channels,
        BinkAudioDecoder.SampleRate, true, decoder.Read);
      source.clip = clip;
      source.loop = true;
      startTime = AudioSettings.dspTime + 0.1;
      started = true;
      source.PlayScheduled(startTime);
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      if (source != null)
      {
        source.Stop();
        source.clip = null;
      }
      decoder.Dispose();
      if (clip != null) UnityEngine.Object.Destroy(clip);
      if (host != null) UnityEngine.Object.Destroy(host);
    }
  }
}
