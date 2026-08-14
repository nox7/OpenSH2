using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class SetAvailableTroopTypesActionReader : ActionReader
  {
    public SetAvailableTroopTypesActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SetAvailableTroopTypesAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      reader.ReadBytes(19);
      obj.Troops.Archer = reader.ReadBoolean();
      reader.ReadBytes(7);
      obj.Troops.Swordsman = reader.ReadBoolean();
      obj.Troops.Spearman = reader.ReadBoolean();
      obj.Troops.Ladderman = reader.ReadBoolean();
      reader.ReadBytes(18);
      obj.Troops.Engineer = reader.ReadBoolean();
      obj.Troops.ArmedPeasant = reader.ReadBoolean();
      obj.Troops.Maceman = reader.ReadBoolean();
      obj.Troops.Pikeman = reader.ReadBoolean();
      obj.Troops.Crossbowman = reader.ReadBoolean();
      reader.ReadBytes(13);
      obj.Troops.Trebuchet = reader.ReadBoolean();
      obj.Troops.Ballista = reader.ReadBoolean();
      obj.Troops.Catapult = reader.ReadBoolean();
      reader.ReadBytes(2);
      obj.Troops.Knight = reader.ReadBoolean();
      obj.Troops.Assassin = reader.ReadBoolean();
      obj.Troops.Outlaw = reader.ReadBoolean();
      obj.Troops.HorseArcher = reader.ReadBoolean();
      obj.Troops.Berserker = reader.ReadBoolean();
      obj.Troops.PictishBoatWarrior = reader.ReadBoolean();
      obj.Troops.LightCalvary = reader.ReadBoolean();
      obj.Troops.AxeThrower = reader.ReadBoolean();
      obj.Troops.Thief = reader.ReadBoolean();
      reader.ReadBytes(8);
      obj.Troops.SmallSiegeTower = reader.ReadBoolean();
      obj.Troops.LargeSiegeTower = reader.ReadBoolean();
      obj.Troops.BatteringRam = reader.ReadBoolean();
      obj.Troops.CAT = reader.ReadBoolean();
      reader.ReadByte();
      obj.Troops.Monk = reader.ReadBoolean();
      obj.Troops.WarriorMonk = reader.ReadBoolean();
      reader.ReadBytes(7);
      obj.Troops.Mantlet = reader.ReadBoolean();
      reader.ReadBytes(10);

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
