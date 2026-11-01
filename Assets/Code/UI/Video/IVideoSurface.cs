using System;

namespace Assets.Code.UI.Video
{
  internal interface IVideoSurface : IDisposable
  {
    bool IsFinished { get; }
    event Action FirstFrame;
    void Play();
    void Tick();
  }
}
