using UnityEngine;

namespace Assets.Code.Lighting
{
  [CreateAssetMenu(menuName = "OpenSH2/Atmosphere Profile")]
  public sealed class AtmosphereProfile : ScriptableObject
  {
    public AtmosphereSettings Settings = new();
  }
}
