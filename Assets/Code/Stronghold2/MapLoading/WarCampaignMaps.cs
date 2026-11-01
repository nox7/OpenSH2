using System;
using System.IO;

namespace Assets.Code.Stronghold2.MapLoading
{
  public static class WarCampaignMaps
  {
    public const int ChapterCount = 12;
    public static string GetPath(int chapter)
    {
      if (chapter < 1 || chapter > ChapterCount) throw new ArgumentOutOfRangeException(nameof(chapter));
      return Path.Combine(Constants.GamePath, "maps", $"war_chapter{chapter}.s2m");
    }
  }
}
