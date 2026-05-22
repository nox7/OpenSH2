using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Assets.Code.UI
{
  public class VideoPlayerUI : MonoBehaviour
  {
    private Action onFinished;
    private Canvas canvas;
    private RawImage videoImage;
    private VideoPlayer videoPlayer;
    private RectTransform rootRectTransform;
    private int fadeOutMs;
    private bool isFinishing;
    private bool isLooping;
    private bool isCompleted;

    public static VideoPlayerUI Play(
      string videoPath, Action onFinished = null, int fadeOutMs = 0, int sortingOrder = 1000, bool isLooping = false, float width = 0f, float height = 0f, float anchorX = 0.5f, float anchorY = 0.5f, float pivotX = 0.5f, float pivotY = 0.5f, float offsetX = 0f, float offsetY = 0f, RectTransform parent = null, string gameObjectName = "VideoPlayerUI", float aspectRatio = 0f)
    {
      if (!File.Exists(videoPath))
      {
        Debug.LogError($"Video file not found: {videoPath}");
        return null;
      }

      GameObject root = new(gameObjectName);
      VideoPlayerUI player = root.AddComponent<VideoPlayerUI>();

      if (aspectRatio > 0f)
      {
        AspectRatioFitter arFitter = root.AddComponent<AspectRatioFitter>();
        arFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        arFitter.aspectRatio = aspectRatio;
      }

      player.Initialize(videoPath, onFinished, fadeOutMs, sortingOrder, isLooping, width, height, anchorX, anchorY, pivotX, pivotY, offsetX, offsetY, parent);
      return player;
    }

    private void Initialize(string videoPath, Action finishedCallback, int fadeOutMilliseconds, int sortingOrder, bool shouldLoop, float width, float height, float anchorX, float anchorY, float pivotX, float pivotY, float offsetX, float offsetY, RectTransform parent)
    {
      onFinished = finishedCallback;
      fadeOutMs = Mathf.Max(0, fadeOutMilliseconds);
      isLooping = shouldLoop;

      if (parent == null)
      {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();
      }
      else
      {
        transform.SetParent(parent, false);
      }

      rootRectTransform = gameObject.GetComponent<RectTransform>();
      if (rootRectTransform == null)
      {
        rootRectTransform = gameObject.AddComponent<RectTransform>();
      }
      ApplyLayout(rootRectTransform, width, height, anchorX, anchorY, pivotX, pivotY, offsetX, offsetY);

      GameObject imageObject = new("VideoImage");
      imageObject.transform.SetParent(transform, false);
      RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
      rectTransform.anchorMin = Vector2.zero;
      rectTransform.anchorMax = Vector2.one;
      rectTransform.offsetMin = Vector2.zero;
      rectTransform.offsetMax = Vector2.zero;

      videoImage = imageObject.AddComponent<RawImage>();
      videoImage.color = Color.black;

      videoPlayer = gameObject.AddComponent<VideoPlayer>();
      videoPlayer.playOnAwake = false;
      videoPlayer.isLooping = isLooping;
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
      if (!isLooping)
      {
        videoPlayer.loopPointReached += OnPlaybackFinished;
      }
      videoPlayer.prepareCompleted += OnPrepared;
      videoPlayer.started += OnStarted;
      videoPlayer.frameReady += OnFrameReady;
      videoPlayer.sendFrameReadyEvents = true;
      videoPlayer.Prepare();
    }

    private void ApplyLayout(RectTransform rectTransform, float width, float height, float anchorX, float anchorY, float pivotX, float pivotY, float offsetX, float offsetY)
    {
      bool hasWidth = width > 0f;
      bool hasHeight = height > 0f;

      float clampedAnchorX = Mathf.Clamp01(anchorX);
      float clampedAnchorY = Mathf.Clamp01(anchorY);
      float clampedPivotX = Mathf.Clamp01(pivotX);
      float clampedPivotY = Mathf.Clamp01(pivotY);

      if (!hasWidth && !hasHeight)
      {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return;
      }

      if (hasWidth && !hasHeight)
      {
        rectTransform.anchorMin = new Vector2(clampedAnchorX, 0f);
        rectTransform.anchorMax = new Vector2(clampedAnchorX, 1f);
        rectTransform.pivot = new Vector2(clampedPivotX, clampedPivotY);
        rectTransform.anchoredPosition = new Vector2(offsetX, offsetY);
        rectTransform.sizeDelta = new Vector2(width, 0f);
        return;
      }

      if (!hasWidth && hasHeight)
      {
        rectTransform.anchorMin = new Vector2(0f, clampedAnchorY);
        rectTransform.anchorMax = new Vector2(1f, clampedAnchorY);
        rectTransform.pivot = new Vector2(clampedPivotX, clampedPivotY);
        rectTransform.anchoredPosition = new Vector2(offsetX, offsetY);
        rectTransform.sizeDelta = new Vector2(0f, height);
        return;
      }

      rectTransform.anchorMin = new Vector2(clampedAnchorX, clampedAnchorY);
      rectTransform.anchorMax = new Vector2(clampedAnchorX, clampedAnchorY);
      rectTransform.pivot = new Vector2(clampedPivotX, clampedPivotY);
      rectTransform.anchoredPosition = new Vector2(offsetX, offsetY);
      rectTransform.sizeDelta = new Vector2(width, height);
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
        videoImage.color = Color.white;
      }
    }

    private void OnPlaybackFinished(VideoPlayer source)
    {
      if (isFinishing)
      {
        return;
      }

      isFinishing = true;

      if (fadeOutMs > 0)
      {
        StartCoroutine(FadeOutAndComplete());
      }
      else
      {
        CompletePlayback();
      }
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
      Debug.LogError($"Video playback failed: {message}");
      CompletePlayback();
    }

    public void Stop(bool invokeFinishedCallback = false)
    {
      CompletePlayback(invokeFinishedCallback);
    }

    public void UpdateLayout(float width = 0f, float height = 0f, float anchorX = 0.5f, float anchorY = 0.5f, float pivotX = 0.5f, float pivotY = 0.5f, float offsetX = 0f, float offsetY = 0f)
    {
      if (rootRectTransform == null)
      {
        return;
      }

      ApplyLayout(rootRectTransform, width, height, anchorX, anchorY, pivotX, pivotY, offsetX, offsetY);
    }

    private IEnumerator FadeOutAndComplete()
    {
      float durationSeconds = fadeOutMs / 1000f;
      float elapsedSeconds = 0f;

      while (elapsedSeconds < durationSeconds)
      {
        elapsedSeconds += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        videoImage.color = Color.Lerp(Color.white, Color.black, t);
        yield return null;
      }

      videoImage.color = Color.black;
      CompletePlayback();
    }

    private void CompletePlayback(bool invokeFinishedCallback = true)
    {
      if (isCompleted)
      {
        return;
      }

      isCompleted = true;
      Cleanup();

      if (invokeFinishedCallback)
      {
        onFinished?.Invoke();
      }

      Destroy(gameObject);
    }

    private void Cleanup()
    {
      if (videoPlayer != null)
      {
        videoPlayer.errorReceived -= OnErrorReceived;
        if (!isLooping)
        {
          videoPlayer.loopPointReached -= OnPlaybackFinished;
        }
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.started -= OnStarted;
        videoPlayer.frameReady -= OnFrameReady;
      }
    }
  }
}
