using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.UserData
{
  [Serializable]
  public sealed class UserProfile
  {
    public int version = 1;
    public string playerName = "";
    public List<int> completedWarChapters = new();
    public int lastAttemptedWarChapter;
    public List<string> completedConquestTrials = new();
  }

  /// <summary>Per-OS-user settings/progress, independent of game saves and installed assets.</summary>
  public sealed class UserProfileStore
  {
    public string FilePath { get; }
    public UserProfile Profile { get; private set; }
    public static string DefaultPath => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSH2", "profile.json");

    public UserProfileStore(string path = null)
    {
      FilePath = path ?? DefaultPath;
      Profile = Read(FilePath) ?? Read(FilePath + ".bak") ?? new UserProfile();
      Profile.completedWarChapters ??= new List<int>();
      Profile.completedConquestTrials ??= new List<string>();
    }

    private static UserProfile Read(string path)
    {
      if (!File.Exists(path)) return null;
      try
      {
        var data = JsonUtility.FromJson<UserProfile>(File.ReadAllText(path));
        if (data == null || data.version != 1) throw new InvalidDataException("Unsupported profile version.");
        return data;
      }
      catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
      {
        Debug.LogWarning($"Unable to read user profile {path}: {error.Message}");
        return null;
      }
    }

    public bool Save()
    {
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)));
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(Profile, true));
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
        else File.Move(temporary, FilePath);
        return true;
      }
      catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
      {
        Debug.LogWarning($"Unable to save user profile {FilePath}: {error.Message}");
        return false;
      }
    }

    public void RecordWarChapterAttempt(int chapter)
    {
      ValidateChapter(chapter);
      Profile.lastAttemptedWarChapter = chapter;
      Save();
    }

    public void CompleteWarChapter(int chapter)
    {
      ValidateChapter(chapter);
      Profile.lastAttemptedWarChapter = chapter;
      if (!Profile.completedWarChapters.Contains(chapter)) Profile.completedWarChapters.Add(chapter);
      Save();
    }

    private static void ValidateChapter(int chapter)
    {
      if (chapter < 1 || chapter > 12) throw new ArgumentOutOfRangeException(nameof(chapter));
    }
  }
}
