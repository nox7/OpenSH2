using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.AssetLoading;

namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  /// <summary>Register another role presentation here without changing recruitment, scheduling or the renderer loop.</summary>
  public sealed class LaborPresentationCatalog
  {
    private readonly Dictionary<LaborRole,LaborPresentation> definitions=new();
    public IEnumerable<LaborPresentation> Definitions => definitions.Values;
    public LaborPresentationCatalog(HorseCartAssemblySettings assembly=null)
    { Register(new WoodcutterPresentation(assembly??new HorseCartAssemblySettings())); }
    public void Register(LaborPresentation definition)
    {
      if(definition==null)throw new ArgumentNullException(nameof(definition));
      definitions.Add(definition.Role,definition);
    }
    public LaborPresentation Get(LaborRole role) => definitions.TryGetValue(role,out var value) ? value
      : throw new NotSupportedException("No labor presentation registered for "+role);
  }
}
