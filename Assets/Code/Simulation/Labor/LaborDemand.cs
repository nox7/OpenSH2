namespace Assets.Code.Simulation.Labor
{
    public readonly struct LaborDemand
    {
        public readonly int BuildingsNeedingLabor,RequiredWorkers,AssignedWorkers,ArrivedWorkers,UnknownRequirements;
        public int MissingLaborers=>RequiredWorkers-AssignedWorkers;
        public LaborDemand(int buildings,int required,int assigned,int arrived,int unknown=0)
        {BuildingsNeedingLabor=buildings;RequiredWorkers=required;AssignedWorkers=assigned;ArrivedWorkers=arrived;UnknownRequirements=unknown;}
    }
    public readonly struct LaborSupply
    {
        public readonly int Alive,Available,Pending,Removed;
        public readonly bool HasPopulation;
        public LaborSupply(int alive,int available,int pending,int removed,bool hasPopulation)
        {Alive=alive;Available=available;Pending=pending;Removed=removed;HasPopulation=hasPopulation;}
    }
}
