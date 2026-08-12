using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class InvasionActionReader : ActionReader
  {
    public InvasionActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      InvasionAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.InvasionPointFlagColor = (FlagColor)reader.ReadInt32();
      obj.InvasionPointFlagNumber = reader.ReadInt32();

      obj.Lord = reader.ReadInt32() switch
      {
        21 => S2MLords.Player,
        22 => S2MLords.Olaf,
        23 => S2MLords.LordBarclay,
        24 => S2MLords.TheHawk,
        25 => S2MLords.TheBull,
        26 => S2MLords.LadySeren,
        27 => S2MLords.Edwin,
        28 => S2MLords.TheKing,
        29 => S2MLords.SirWilliam,
        30 => S2MLords.SirGrey,
        _ => S2MLords.Olaf
      };

      reader.ReadInt32();

      // BEGIN Unit amounts
      obj.Troops.ArmedPeasant = reader.ReadInt32();
      obj.Troops.Spearman = reader.ReadInt32();
      obj.Troops.Archer = reader.ReadInt32();
      obj.Troops.Pikeman = reader.ReadInt32();
      obj.Troops.Maceman = reader.ReadInt32();
      obj.Troops.Crossbowman = reader.ReadInt32();
      obj.Troops.Swordsman = reader.ReadInt32();
      obj.Troops.Knight = reader.ReadInt32();
      obj.Troops.Monk = reader.ReadInt32();
      obj.Troops.WarriorMonk = reader.ReadInt32();
      reader.ReadInt32();
      obj.Troops.Ladderman = reader.ReadInt32();
      obj.Troops.Engineer = reader.ReadInt32();
      obj.Troops.Assassin = reader.ReadInt32();
      obj.Troops.Outlaw = reader.ReadInt32();
      obj.Troops.HorseArcher = reader.ReadInt32();
      obj.Troops.Berserker = reader.ReadInt32();
      obj.Troops.PictishBoatWarrior = reader.ReadInt32();
      obj.Troops.LightCalvary = reader.ReadInt32();
      obj.Troops.AxeThrower = reader.ReadInt32();
      reader.ReadInt32();
      obj.Troops.SmallSiegeTower = reader.ReadInt32();
      obj.Troops.LargeSiegeTower = reader.ReadInt32();
      obj.Troops.BatteringRam = reader.ReadInt32();
      obj.Troops.CAT = reader.ReadInt32();
      obj.Troops.Trebuchet = reader.ReadInt32();
      obj.Troops.Ballista = reader.ReadInt32();
      obj.Troops.Catapult = reader.ReadInt32();
      obj.Troops.Mantlet = reader.ReadInt32();
      obj.Troops.BurningCart = reader.ReadInt32();
      // END Unit amounts

      reader.ReadInt32();
      reader.ReadInt32();

      obj.TargetPointFlagColor = (FlagColor)reader.ReadInt32();
      obj.TargetPointFlagNumber = reader.ReadInt32();
      obj.TargetLord = reader.ReadInt32() switch
      {
        0 => null,
        21 => S2MLords.Player,
        22 => S2MLords.Olaf,
        23 => S2MLords.LordBarclay,
        24 => S2MLords.TheHawk,
        25 => S2MLords.TheBull,
        26 => S2MLords.LadySeren,
        27 => S2MLords.Edwin,
        28 => S2MLords.TheKing,
        29 => S2MLords.SirWilliam,
        30 => S2MLords.SirGrey,
        _ => S2MLords.Olaf
      };

      obj.ArmyType = reader.ReadByte();
      obj.DoesLeaveMap = reader.ReadBoolean();
      obj.DoesReceiveFullWarnings = reader.ReadBoolean();
      obj.IsReinforcingTargetLord = reader.ReadBoolean();

      reader.ReadBoolean();

      obj.IncludeLordInInvasion = reader.ReadBoolean();
      obj.AreInvasionWarningsEnabled = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
