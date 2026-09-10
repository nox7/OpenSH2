using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Owns the loaded in-memory map that scene renderers can share.</summary>
  public sealed class S2MMapLoader : MonoBehaviour
  {
    [Tooltip("Absolute path, or a path relative to Application.streamingAssetsPath.")]
    [SerializeField]
    private string s2mFilePath;

    [SerializeField]
    private bool loadOnStart = true;

    public S2MFile LoadedMap { get; private set; }
    public bool IsLoaded => LoadedMap != null;

    public event Action<S2MFile> MapLoaded;

    private void Start()
    {
      if (loadOnStart) LoadMap();
    }

    [ContextMenu("Load S2M Map")]
    public void LoadMap()
    {
      LoadedMap = S2MFileLoader.Load(ResolvePath(s2mFilePath));
      MapLoaded?.Invoke(LoadedMap);
    }

    public void LoadMap(string path)
    {
      LoadedMap = S2MFileLoader.Load(ResolvePath(path));
      MapLoaded?.Invoke(LoadedMap);
    }

    private static string ResolvePath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        throw new InvalidOperationException("Set an S2M file path before loading the map.");

      return Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, path));
    }
  }
}
