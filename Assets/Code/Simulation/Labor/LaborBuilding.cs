using System;using System.Collections.Generic;
namespace Assets.Code.Simulation.Labor
{
  /// <summary>Building-level staffing; reservations include workers still traveling, arrival is tracked separately.</summary>
  public sealed class LaborBuilding
  {
    public int BuildingId { get; }public int EstateId { get; }public bool StaffingConfigured { get; }
    public IReadOnlyList<LaborJob> Jobs { get; }
    public int MaximumWorkers => Jobs.Count;
    public int TotalWorkers {get{int n=0;for(int i=0;i<Jobs.Count;i++)if(Jobs[i].WorkerId.HasValue)n++;return n;}}
    public int ArrivedWorkers {get{int n=0;for(int i=0;i<Jobs.Count;i++)if(Jobs[i].IsOccupied)n++;return n;}}
    public bool IsFullyStaffed => StaffingConfigured && TotalWorkers==MaximumWorkers;
    public bool IsCrewReady => StaffingConfigured && ArrivedWorkers==MaximumWorkers;
    public LaborBuilding(int id,int estate,IReadOnlyList<LaborJob> jobs,bool configured=true)
    {
      BuildingId=id;EstateId=estate;StaffingConfigured=configured;var slots=new List<LaborJob>(jobs);
      foreach(var job in slots){if(job.BuildingId!=id || job.EstateId!=estate)throw new ArgumentException("Worker slot belongs to another building.");job.Building=this;}
      Jobs=slots.AsReadOnly();
    }
  }
}
