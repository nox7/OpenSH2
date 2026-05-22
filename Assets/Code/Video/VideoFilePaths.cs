using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Video
{
  /// <summary>
  /// Paths to the original game's Bink video files
  /// </summary>
  class VideoFilePaths
  {
    public static string FireflyLogo = Constants.GamePath + "/ui/frontend/firefly_logo.bik";
    public static string Intro = Constants.GamePath + "/ui/frontend/Stronghold2 FMV.bik";
    public static string MainMenuCurtainOpen = Constants.GamePath + "/ui/frontend/curtain_animation.bik";
    public static string MainMenuCurtainIdle = Constants.GamePath + "/ui/frontend/curtain_idle.bik";
    public static string MainMenuBackground = Constants.GamePath + "/ui/frontend/background.bik";

    public static readonly Dictionary<string, string> CachedVideoOutputFormats = new()
    {
      { FireflyLogo, Converter.OutputFormatMp4 },
      { Intro, Converter.OutputFormatMp4 },
      { MainMenuCurtainOpen, Converter.OutputFormatWebM },
      { MainMenuCurtainIdle, Converter.OutputFormatWebM },
      { MainMenuBackground, Converter.OutputFormatMp4 },
    };

    public static string GetCachedVideoOutputFormat(string originalBikPath)
    {
      if (CachedVideoOutputFormats.TryGetValue(originalBikPath, out string outputFormat))
      {
        return outputFormat;
      }

      return Converter.OutputFormatMp4;
    }

    public static string GetCachedVideoPath(string originalBikPath)
    {
      string basename = System.IO.Path.GetFileNameWithoutExtension(originalBikPath);
      string outputFormat = GetCachedVideoOutputFormat(originalBikPath);
      return Constants.VideoCachePath + "/" + basename + "." + outputFormat;
    }
  }
}
