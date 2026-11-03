using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Simulation.AI;
public static class AicBlueprintValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Malformed AIC accepted");}
    static void Set(object instance,string property,object value)=>instance.GetType().GetProperty(property).SetValue(instance,value);
    static void Word(byte[] b,int at,uint value){for(int i=0;i<4;i++)b[at+i]=(byte)(value>>(8*i));}
    public static int Main()
    {
        try{Run();Console.WriteLine("PASS: AIC reader/native files, malformed/truncated inputs, immutable raw fields, kingdom ownership, campaign resolution, overrides, shared cache, selection and unload.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    public static void Run()
    {
        var raw=new byte[4+16+AicCastleReader.TailByteLength+3];Word(raw,0,2);Word(raw,4,0xfa0201ff);Word(raw,8,0xffccabcd);Word(raw,12,0x00080716);
        int tail=20;Word(raw,tail,0xffffffff);Word(raw,tail+50*4,123);Word(raw,tail+51*4+4,1004);Word(raw,tail+(51+240+199)*4,9901);Word(raw,tail+(51+240+200)*4,0x80000000);raw[raw.Length-1]=91;
        var blueprint=AicCastleReader.Read(raw);raw[4]=0;raw[raw.Length-1]=0;
        Check(blueprint.Items[0].Kind==255 && blueprint.Items[0].RotationCode==250 && blueprint.Items[0].Group==0xabcd && blueprint.Items[0].Parameter==0xcc && blueprint.Items[0].ReservedByte==255,"Raw packed fields lost");
        Check(blueprint.Values[0]==-1 && blueprint.UnknownValue==123 && blueprint.GetListPoint(0,1).X==4 && blueprint.GetListPoint(0,1).Z==10 && !blueprint.ListPoints[0].HasPosition,"Point/value decoding failed");
        Check(blueprint.GetGroupPoint(9,19).Encoded==9901 && blueprint.ListPointFlags[0]==int.MinValue && blueprint.TrailingBytes.Count==3 && blueprint.TrailingBytes[2]==91,"Tail/extension preservation failed");
        Check(blueprint.PrimaryKeep?.KeepKind==AicKeepKind.Medium && blueprint.Items.Count==2 && blueprint.Keeps.Count==1,"Keep extraction failed");
        try{((IList<int>)blueprint.Values)[0]=7;throw new Exception("Mutable values exposed");}catch(NotSupportedException){}
        Reject(()=>AicCastleReader.Read(Array.Empty<byte>()));Reject(()=>AicCastleReader.Read(new byte[3]));
        var overflow=new byte[4];Word(overflow,0,uint.MaxValue);Reject(()=>AicCastleReader.Read(overflow));
        Reject(()=>AicCastleReader.Read(new byte[4+AicCastleReader.TailByteLength-1]));
        Check(AicCastleReader.Read(new byte[4+AicCastleReader.TailByteLength]).Items.Count==0,"Valid empty blueprint rejected");
        var olaf=AicCastleReader.ReadFile(Path.Combine(Install,"castles","AIVChap6_Olaf.aic"));
        Check(olaf.Items.Count==90 && olaf.Items[0].Kind==20 && olaf.Items[0].LocalX==48 && olaf.Items[0].LocalZ==40 && olaf.PrimaryKeep?.KeepKind==AicKeepKind.Wooden,"Native Olaf design incorrect");
        int valid=0,invalid=0;
        foreach(string file in Directory.EnumerateFiles(Path.Combine(Install,"castles"),"*.aic"))
        {try{var b=AicCastleReader.ReadFile(file);Check(b.ByteLength==4+8*b.Items.Count+AicCastleReader.TailByteLength+b.TrailingBytes.Count,"Byte accounting failed");valid++;}catch(InvalidDataException){invalid++;}}
        Console.WriteLine($"Native AIC files: {valid} valid; {invalid} truncated test files rejected.");
        var map=new S2MFile();Set(map,"SourcePath",Path.Combine(Install,"maps","war_chapter6.s2m"));
        var world=new AiWorld();
        world.Add(new AiTerritory(1,AiTerritoryKind.Kingdom,AiController.Human,new AiFaction("scenario-lord",1)));
        world.Add(new AiTerritory(2,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("scenario-lord",2)));
        world.Add(new AiTerritory(3,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("scenario-lord",7)));
        world.Add(new AiTerritory(4,AiTerritoryKind.Estate,AiController.Autonomous,new AiFaction("scenario-lord",2)));
        using(var library=new S2MCastleBlueprints(map,world,Install))
        {
            Check(library.Loaded.Count==valid && library.Files.Count==valid+invalid && library.Issues.Count==invalid,"Catalog error isolation incorrect");
            Check(library.Kingdoms.Count==2 && library.WarChapter==6 && library.Kingdoms[2].Selected.PrimaryKeep?.KeepKind==AicKeepKind.Wooden && library.Kingdoms[3].Selected.PrimaryKeep?.KeepKind==AicKeepKind.Small,"Campaign assignments failed");
            Check(world.Territories[2].BlueprintKey=="AIVChap6_Olaf.aic" && library.Kingdoms[2].RotationQuarterTurns==0,"AI fact key/rotation not published");
            Check(ReferenceEquals(library.Loaded["aivchap6_olaf.AIC"],library.Kingdoms[2].Selected),"Duplicate parsed object per kingdom");
            Check(!library.TrySelect(1,olaf.Key) && !library.TrySelect(4,olaf.Key),"Human/village selected a castle blueprint");
            world.SetControl(2,new AiFaction("scenario-lord",2),AiController.Human);Check(!library.Kingdoms.ContainsKey(2) && world.Territories[2].BlueprintKey==null,"Human capture retained assignment");
            world.SetControl(2,new AiFaction("scenario-lord",3),AiController.Autonomous);
            Check(library.Kingdoms[2].Selected==null && library.Kingdoms[2].Candidates.Count==3,"Wrong lord candidate set or guessed selection");
            world.Add(new AiTerritory(5,AiTerritoryKind.Kingdom,AiController.Autonomous,new AiFaction("scenario-lord",3)));
            Check(ReferenceEquals(library.Kingdoms[5].Candidates[0],library.Kingdoms[2].Candidates[0]),"Shared lord blueprints duplicated");
            Check(library.TrySelect(5,"barclay01.aic",2) && world.Territories[5].BlueprintKey=="Barclay01.aic","Runtime selection failed");
            world.Remove(5);Check(!library.Kingdoms.ContainsKey(5),"Removed kingdom retained");
            world.Add(new AiTerritory(6,AiTerritoryKind.Kingdom,AiController.Autonomous));Check(library.Kingdoms[6].Origin==CastleBlueprintOrigin.Unresolved && library.Kingdoms[6].Issue!=null,"Unknown lord silently guessed");
            library.Dispose();world.SetControl(2,new AiFaction("scenario-lord",2),AiController.Autonomous);Check(library.Loaded.Count==0 && library.Kingdoms.Count==0,"Unload retained cache or subscriptions");
        }
        world.SetControl(2,new AiFaction("scenario-lord",2),AiController.Autonomous);
        var settings=new CastleBlueprintSettings();settings.KingdomOverrides.Add(new KingdomCastleDesignOverride{EstateId=2,MapFileName="war_chapter6.s2m",File="Keep_only_Stone.aic",RotationQuarterTurns=3});
        using(var custom=new S2MCastleBlueprints(map,world,Install,settings))Check(custom.Kingdoms[2].Origin==CastleBlueprintOrigin.Override && custom.Kingdoms[2].Selected.Key=="Keep_only_Stone.aic" && custom.Kingdoms[2].RotationQuarterTurns==3,"Override lost priority");
        Set(map,"SourcePath",Path.Combine(Path.GetTempPath(),"war_chapter6.s2m"));
        using(var customMap=new S2MCastleBlueprints(map,world,Install))Check(customMap.WarChapter==null && customMap.Kingdoms[2].Selected==null,"Custom file mistaken for official campaign");
        settings.KingdomOverrides[0].File="missing.aic";
        using(var missing=new S2MCastleBlueprints(map,world,Install,settings))Check(missing.Kingdoms[2].Selected==null && missing.Kingdoms[2].Issue!=null,"Missing explicit assignment silently replaced");
    }
}
