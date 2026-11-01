using System;
using System.Text;
using System.Threading;

namespace Assets.Code.Video.EmbeddedFFmpeg
{
  /// <summary>Decodes PCM off-thread into a two-second ring. Unity's audio callback only copies floats.</summary>
  internal sealed class BinkAudioDecoder : IDisposable
  {
    public const int SampleRate = 48000;
    public const int Channels = 2;
    private readonly object gate = new();
    private readonly float[] ring = new float[SampleRate * Channels * 2];
    private readonly Thread worker;
    private long readPosition, writePosition, decodedFrames, deliveredFrames, callbackCount, underrunFrames;
    private volatile bool stopped, opened, hasAudio, ended;
    private volatile Exception failure;
    private volatile float peak;
    public bool HasAudio => hasAudio;
    public bool Ended => ended;
    public Exception Failure => failure;
    public long DecodedFrames => Interlocked.Read(ref decodedFrames);
    public long DeliveredFrames => Interlocked.Read(ref deliveredFrames);
    public long CallbackCount => Interlocked.Read(ref callbackCount);
    public long UnderrunFrames => Interlocked.Read(ref underrunFrames);
    public float Peak => peak;
    public bool IsReady
    {
      get { lock (gate) return opened && (!hasAudio || ended || writePosition - readPosition >= SampleRate * Channels); }
    }

    public BinkAudioDecoder(string path)
    {
      worker = new Thread(() => Decode(path)) { IsBackground = true, Name = "OpenSH2 Bink audio" };
      worker.Start();
    }

    private void Decode(string path)
    {
      IntPtr handle = IntPtr.Zero;
      var error = new StringBuilder(1024);
      try
      {
        handle = BinkNative.osh2_bink_audio_open(path, error, error.Capacity);
        if (handle == IntPtr.Zero && error.Length > 0) throw new InvalidOperationException($"Bink audio open failed: {error}");
        hasAudio = handle != IntPtr.Zero;
        opened = true;
        if (!hasAudio) { ended = true; return; }
        var samples = new float[4096 * Channels];
        while (!stopped)
        {
          int frames = BinkNative.osh2_bink_audio_read(handle, samples, samples.Length / Channels, error, error.Capacity);
          if (frames < 0) throw new InvalidOperationException($"Bink audio decode failed: {error}");
          if (frames == 0) { ended = true; break; }
          Interlocked.Add(ref decodedFrames, frames);
          int offset = 0, count = frames * Channels;
          lock (gate)
          {
            while (offset < count && !stopped)
            {
              // If the device underruns, discard late samples to preserve the DSP timeline.
              if (writePosition < readPosition)
              {
                int skip = (int)Math.Min(count - offset, readPosition - writePosition);
                offset += skip; writePosition += skip;
                continue;
              }
              int room = ring.Length - (int)(writePosition - readPosition);
              if (room == 0) { Monitor.Wait(gate); continue; }
              int index = (int)(writePosition % ring.Length);
              int copy = Math.Min(count - offset, Math.Min(room, ring.Length - index));
              Array.Copy(samples, offset, ring, index, copy);
              offset += copy; writePosition += copy;
            }
          }
        }
      }
      catch (Exception exception) { if (!stopped) failure = exception; }
      finally { if (handle != IntPtr.Zero) BinkNative.osh2_bink_audio_close(handle); }
    }

    // PCMReaderCallback: no allocation, decoding, waiting for data, or Unity API calls.
    public void Read(float[] samples)
    {
      Array.Clear(samples, 0, samples.Length);
      lock (gate)
      {
        if (stopped) return;
        int available = (int)Math.Min(samples.Length, Math.Max(0, writePosition - readPosition));
        int index = (int)(readPosition % ring.Length);
        int first = Math.Min(available, ring.Length - index);
        Array.Copy(ring, index, samples, 0, first);
        if (first < available) Array.Copy(ring, 0, samples, first, available - first);
        readPosition += samples.Length;
        Interlocked.Add(ref deliveredFrames, available / Channels);
        Interlocked.Increment(ref callbackCount);
        if (!ended) Interlocked.Add(ref underrunFrames, (samples.Length - available) / Channels);
        for (int i = 0; i < available; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
        Monitor.PulseAll(gate);
      }
    }

    public void Dispose()
    {
      lock (gate)
      {
        if (stopped) return;
        stopped = true;
        Monitor.PulseAll(gate);
      }
      worker.Join();
    }
  }
}
