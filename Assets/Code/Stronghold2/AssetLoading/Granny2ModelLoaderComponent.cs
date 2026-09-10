using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class Granny2ModelLoaderComponent : MonoBehaviour
  {
    [SerializeField] private string gameInstallPath = "C:/Steam/steamapps/common/Stronghold 2";
    [SerializeField] private string assetRelativePath = "meshes/landscape/tree_1.gr2";
    [SerializeField] private string converterExecutablePath;
    [SerializeField] private bool loadOnStart = true;

    public Granny2ModelData LoadedModel { get; private set; }
    public bool IsLoaded => LoadedModel != null;
    public event Action<Granny2ModelData> ModelLoaded;

    private void Start()
    {
      if (loadOnStart) Load();
    }

    [ContextMenu("Load Stronghold 2 GR2 Model")]
    public void Load()
    {
      LoadedModel = Granny2AssetConverter.LoadOrConvert(
        gameInstallPath,
        assetRelativePath,
        string.IsNullOrWhiteSpace(converterExecutablePath) ? null : converterExecutablePath);
      ModelLoaded?.Invoke(LoadedModel);
    }
  }
}
