using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Assets.Code.UI
{
  public class FullscreenVideoPlayer : MonoBehaviour
  {
    private Action onFinished;
    private Canvas canvas;
    private RawImage videoImage;
    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;

    public static FullscreenVideoPlayer Play(string videoPath, Action onFinished = null)
    {
      if (!File.Exists(videoPath))
      {
        Debug.LogError($"Video file not found: {videoPath}");
        return null;
      }

      GameObject root = new("FullscreenVideoPlayer");
      FullscreenVideoPlayer player = root.AddComponent<FullscreenVideoPlayer>();
      player.Initialize(videoPath, onFinished);
      return player;
    }

    private void Initialize(string videoPath, Action finishedCallback)
    {
      onFinished = finishedCallback;

      canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1000;

      gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();

      GameObject imageObject = new("VideoImage");
      imageObject.transform.SetParent(transform, false);
      RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
      rectTransform.anchorMin = Vector2.zero;
      rectTransform.anchorMax = Vector2.one;
      rectTransform.offsetMin = Vector2.zero;
      rectTransform.offsetMax = Vector2.zero;

      videoImage = imageObject.AddComponent<RawImage>();
      videoImage.color = Color.white;

      videoPlayer = gameObject.AddComponent<VideoPlayer>();
      videoPlayer.playOnAwake = false;
      videoPlayer.isLooping = false;
      videoPlayer.waitForFirstFrame = true;
      videoPlayer.skipOnDrop = false;
      videoPlayer.playbackSpeed = 1f;
      videoPlayer.timeReference = VideoTimeReference.Freerun;
      videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
      videoPlayer.source = VideoSource.Url;
      videoPlayer.url = new Uri(videoPath).AbsoluteUri;
      videoPlayer.renderMode = VideoRenderMode.APIOnly;
      videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
      videoPlayer.controlledAudioTrackCount = 1;
      videoPlayer.EnableAudioTrack(0, true);
      videoPlayer.SetDirectAudioMute(0, false);
      videoPlayer.SetDirectAudioVolume(0, 1f);

      videoImage.texture = null;

      videoPlayer.errorReceived += OnErrorReceived;
      videoPlayer.loopPointReached += OnPlaybackFinished;
      videoPlayer.prepareCompleted += OnPrepared;
      videoPlayer.started += OnStarted;
      videoPlayer.frameReady += OnFrameReady;
      videoPlayer.sendFrameReadyEvents = true;
      videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer source)
    {
      Debug.Log($"Video prepared. Resolution: {source.width}x{source.height}, frameCount: {source.frameCount}");
      source.Play();
    }

    private void OnStarted(VideoPlayer source)
    {
      Debug.Log("Video playback started.");
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
      if (videoImage.texture == null && source.texture != null)
      {
        videoImage.texture = source.texture;
      }
    }

    private void OnPlaybackFinished(VideoPlayer source)
    {
      Cleanup();
      onFinished?.Invoke();
      Destroy(gameObject);
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
      Debug.LogError($"Video playback failed: {message}");
      Cleanup();
      Destroy(gameObject);
    }

    private void Cleanup()
    {
      if (videoPlayer != null)
      {
        videoPlayer.errorReceived -= OnErrorReceived;
        videoPlayer.loopPointReached -= OnPlaybackFinished;
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.started -= OnStarted;
        videoPlayer.frameReady -= OnFrameReady;
      }
    }
  }
}
