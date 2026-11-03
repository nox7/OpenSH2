using System;
using System.Collections.Generic;
namespace Assets.Code.Stronghold2.Castles
{
    [Serializable] public sealed class LordCastleDesigns
    {
        public int Lord;
        public string[] Files=Array.Empty<string>();
        public LordCastleDesigns(){}
        public LordCastleDesigns(int lord,params string[] files){Lord=lord;Files=files;}
    }
    [Serializable] public sealed class KingdomCastleDesignOverride
    {
        public int EstateId;
        public string MapFileName="",File="";
        // -1 leaves placement rotation for the future site selector.
        public int RotationQuarterTurns=-1;
    }
    /// <summary>Snapshotted at map load. This describes candidates/assignments, not permission to build them.</summary>
    [Serializable] public sealed class CastleBlueprintSettings
    {
        public bool UseCampaignAssignments=true;
        public List<KingdomCastleDesignOverride> KingdomOverrides=new();
        public List<LordCastleDesigns> LordDesigns=new()
        {
            new LordCastleDesigns(2,"Tiny_Wooden.aic","Small_Wooden.aic","Med_Wooden.aic","Large_Wooden.aic"),
            new LordCastleDesigns(3,"Barclay01.aic","Barclay02.aic","Barclay03.aic","Barclay04.aic"),
            new LordCastleDesigns(4,"Hawk01.aic","Hawk02.aic","Hawk03.aic","Hawk04.aic"),
            new LordCastleDesigns(5,"Bull01.aic","Bull02.aic","Bull03.aic","Bull04.aic"),
            new LordCastleDesigns(6,"Seren01.aic","Seren02.aic","Seren03.aic","Seren04.aic"),
            new LordCastleDesigns(7,"Edwin01.aic","Edwin02.aic","Edwin03.aic","Edwin04.aic"),
            new LordCastleDesigns(8,"King01.aic","King02.aic","King03.aic","King04.aic"),
            new LordCastleDesigns(9,"William01.aic","William02.aic","William03.aic","William04.aic"),
            new LordCastleDesigns(10,"Grey01.aic","Grey02.aic","Grey03.aic","Grey04.aic")
        };
    }
}
