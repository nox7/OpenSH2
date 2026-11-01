using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Assets.Code.Video.EmbeddedFFmpeg
{
  internal sealed class BinkVideoInfo
  {
    public readonly int Width, Height;
    public readonly double FrameSeconds;
    public BinkVideoInfo(int width, int height, double frameSeconds)
    { Width = width; Height = height; FrameSeconds = frameSeconds; }
  }

  internal sealed class BinkFrame
  {
    public byte[] Pixels;
    public double Seconds;
    public int Loop;
  }

  /// <summary>Bink 1 video decoding on one worker; at most four RGBA buffers exist. Audio has its own worker.</summary>
  internal sealed class BinkFrameDecoder : IDisposable
  {
    private readonly BlockingCollection<BinkFrame> ready = new(3);
    private readonly BlockingCollection<BinkFrame> free = new(4);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private volatile BinkVideoInfo info;
    private volatile Exception failure;
    private volatile bool ended;
    private bool disposed;
    public BinkVideoInfo Info => info;
    public Exception Failure => failure;
    public bool HasEnded => ended && ready.Count == 0;

    public BinkFrameDecoder(string path, bool loop)
    {
      worker = new Thread(() => Decode(path, loop)) { IsBackground = true, Name = "OpenSH2 Bink decode" };
      worker.Start();
    }

    private void Decode(string path, bool loop)
    {
      IntPtr handle = IntPtr.Zero;
      var error = new StringBuilder(1024);
      try
      {
        handle = BinkNative.osh2_bink_open(path, out int width, out int height, out double step, error, error.Capacity);
        if (handle == IntPtr.Zero) throw new InvalidOperationException($"Cannot open Bink '{path}': {error}");
        for (int i = 0; i < 4; i++) free.Add(new BinkFrame { Pixels = new byte[checked(width * height * 4)] });
        info = new BinkVideoInfo(width, height, step);
        double offset = 0;
        int loopIndex = 0, frameCount = 0;
        while (true)
        {
          cancellation.Token.ThrowIfCancellationRequested();
          BinkFrame frame = free.Take(cancellation.Token);
          int result = BinkNative.osh2_bink_read(handle, frame.Pixels, frame.Pixels.Length,
            out double seconds, error, error.Capacity);
          if (result < 0) throw new InvalidOperationException($"Bink decode failed: {error}");
          if (result == 0)
          {
            free.Add(frame, cancellation.Token);
            if (!loop) { ended = true; break; }
            if (frameCount == 0) throw new InvalidOperationException("Bink loop contains no decoded frames.");
            if (BinkNative.osh2_bink_rewind(handle, error, error.Capacity) < 0)
              throw new InvalidOperationException($"Bink rewind failed: {error}");
            offset += frameCount * step;
            frameCount = 0;
            loopIndex++;
            continue;
          }
          frame.Seconds = offset + seconds;
          frame.Loop = loopIndex;
          frameCount++;
          ready.Add(frame, cancellation.Token);
        }
      }
      catch (OperationCanceledException) { }
      catch (Exception exception) { failure = exception; }
      finally
      {
        if (handle != IntPtr.Zero) BinkNative.osh2_bink_close(handle);
      }
    }

    public bool TryTake(out BinkFrame frame) => ready.TryTake(out frame);
    public void Recycle(BinkFrame frame) => free.Add(frame);

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      cancellation.Cancel();
      // Local Bink packets are small; no network I/O or Unity calls occur on this worker.
      // Join before releasing queues so a native decoder can never outlive its owner.
      worker.Join();
      ready.Dispose();
      free.Dispose();
      cancellation.Dispose();
    }
  }
}
