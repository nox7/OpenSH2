using System.Threading.Tasks;
using Assets.Code.UI;
using UnityEngine;

public class Main : MonoBehaviour
{
  private Task conversionTask;
  private string cachedIntroVideoPath;
  private bool introVideoStarted;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    // Convert the game intro video into an MP4 if it doesn't exist in the cache.
    string pathToVideoCache = Application.dataPath + "/Cache/Video";
    string pathToIntroVideoBIK = Constants.GamePath + "/ui/frontend/Stronghold2 FMV.bik";

    string basename = System.IO.Path.GetFileNameWithoutExtension(pathToIntroVideoBIK);
    cachedIntroVideoPath = pathToVideoCache + "/" + basename + ".mp4";

    // Check if it's already cached
    if (System.IO.File.Exists(cachedIntroVideoPath))
    {
      Debug.Log("Intro video already cached, skipping conversion.");
      StartIntroVideoPlayback();
      return;
    }

    conversionTask = Task.Run(() => Converter.ConvertToMP4(pathToIntroVideoBIK, cachedIntroVideoPath));
    Debug.Log("Started intro video conversion in background.");
  }

  // Update is called once per frame
  void Update()
  {
    if (conversionTask == null)
    {
      return;
    }

    if (!conversionTask.IsCompleted)
    {
      return;
    }

    if (conversionTask.IsFaulted)
    {
      Debug.LogError($"Intro video conversion task failed: {conversionTask.Exception}");
    }
    else
    {
      Debug.Log("Intro video conversion task finished.");
      StartIntroVideoPlayback();
    }

    conversionTask = null;
  }

  private void StartIntroVideoPlayback()
  {
    if (introVideoStarted)
    {
      return;
    }

    if (!System.IO.File.Exists(cachedIntroVideoPath))
    {
      Debug.LogError($"Cached intro video not found: {cachedIntroVideoPath}");
      return;
    }

    introVideoStarted = true;
    FullscreenVideoPlayer.Play(cachedIntroVideoPath, () =>
    {
      Debug.Log("Intro video playback finished.");
    });
  }
}
