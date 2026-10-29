using System.Collections.Generic;using UnityEngine;using Assets.Code.Simulation.Economy;
namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MOxHaulingAssets
  {
    public const string HandlerWalk="ox-handler-walk",HandlerIdle="ox-handler-idle",OxWalk="working-ox-walk",OxIdle="working-ox-idle";
    public const string FramePath="meshes/units/animals/oxen_stone_prop.GR2";
    private static readonly string[,] variants=CreateVariants();
    private static readonly string[,] ironVariants=CreateVariants(true);
    private static string[,] CreateVariants(bool iron=false)
    {var result=new string[2,9];for(int moving=0;moving<2;moving++)for(int n=0;n<=8;n++)result[moving,n]=(moving==1?OxWalk:OxIdle)+(iron?"-iron":"")+(n==0?"":"-"+n);return result;}
    public static string OxVariant(bool moving,int blocks,Goods goods=Goods.Stone)=>(goods==Goods.Iron && blocks>0?ironVariants:variants)[moving?1:0,blocks];
    public static IEnumerable<S2MActorOverride> Overrides()
    {
      foreach(var entry in new[]{(HandlerWalk,"WALK"),(HandlerIdle,"IDLE SCRATCH")})
        yield return new S2MActorOverride{ObjectType=entry.Item1,Variant=entry.Item1,DefinitionPath="meshes/units/ox_handler.uni",AnimationAction=entry.Item2,LocalEulerAngles=new Vector3(0,180,0)};
      foreach(bool iron in new[]{false,true})for(int moving=0;moving<2;moving++)for(int blocks=iron?1:0;blocks<=8;blocks++)
      {
        // The animal definition's attachment point 1 is skeleton bone 1 (pelvis).
        // Both the sledge and its load are authored in that bone's coordinates.
        var attachments=new List<S2MActorAttachment>{new(){ModelPath=FramePath,BoneName="Bip01 Pelvis"}};
        if(blocks>0)attachments.Add(new S2MActorAttachment{ModelPath=$"meshes/units/animals/oxen_{(iron?"iron":"stone")}_{blocks:00}.GR2",BoneName="Bip01 Pelvis"});
        string variant=OxVariant(moving==1,blocks,iron?Goods.Iron:Goods.Stone);
        yield return new S2MActorOverride{ObjectType=variant,Variant=variant,ModelPath="meshes/units/animals/oxen.GR2",
          AnimationPath="meshes/units/animals/"+(moving==1?"oxen_walk.GR2":"oxen_grazing.GR2"),Attachments=attachments.ToArray(),LocalEulerAngles=new Vector3(0,180,0),TeamGroup=-1};
      }
    }
  }
}
