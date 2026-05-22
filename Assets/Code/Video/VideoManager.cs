using Assets.Code.UI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Code.Video
{
  class VideoManager
  {
    public Task introVideosTask = null;
    private VideoPlayerUI activeIntroVideoPlayer;

    public VideoManager()
    {
    }

    /// <summary>
    /// Plays a video at full screen. This function will assume the provided video file path
    /// is a .bik video and has already been cached to its configured output format.
    /// </summary>
    /// <param name="videoFilePath"></param>
    /// <returns></returns>
    public Task PlayFullscreenBinkVideo(string videoFilePath, int fadeOutMs = 0)
    {
      // Fetch the video from the videos cache
      string cachedVideoPath = VideoFilePaths.GetCachedVideoPath(videoFilePath);

      var tcs = new TaskCompletionSource<object>(
        TaskCreationOptions.RunContinuationsAsynchronously);

      Debug.Log("Trying?");
      activeIntroVideoPlayer = VideoPlayerUI.Play(cachedVideoPath, delegate ()
      {
        activeIntroVideoPlayer = null;
        tcs.TrySetResult(null);
      }, fadeOutMs);

      if (activeIntroVideoPlayer == null)
      {
        tcs.TrySetResult(null);
      }

      return tcs.Task;
    }

    public void SkipCurrentIntroVideo()
    {
      if (activeIntroVideoPlayer == null)
      {
        return;
      }

      activeIntroVideoPlayer.Stop(invokeFinishedCallback: true);
    }
  }
}
