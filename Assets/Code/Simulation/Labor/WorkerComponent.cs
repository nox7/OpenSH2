using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Movement;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public enum WorkerState { Unassigned, Traveling, Working }

  /// <summary>Employment component, independent of recruitment, building type, and rendering.</summary>
  public sealed class WorkerComponent
  {
    public int ActorId { get; }
    public MovementComponent Movement { get; }
    public LaborJob Job { get; private set; }
    public WorkerState State { get; private set; }
    public float StateTime { get; private set; }
    public WorkerComponent(int actorId,MovementComponent movement)
    { ActorId=actorId;Movement=movement??throw new ArgumentNullException(nameof(movement)); }

    public bool TryAssign(LaborJob job,IReadOnlyList<MapPoint> path)
    {
      if(job==null)throw new ArgumentNullException(nameof(job));
      if(Job!=null || path==null || path.Count==0 || MapPoint.Distance(path[path.Count-1],job.Approach)>.001f)return false;
      if(!job.TryReserve(ActorId))return false;
      Job=job;State=WorkerState.Traveling;StateTime=0;Movement.Follow(path);
      if(!Movement.IsFollowingPath)Arrive();
      return true;
    }
    public void Advance(float seconds,float walkSpeed)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      float remaining=seconds;
      if(State==WorkerState.Traveling)
      {
        Movement.Advance(ref remaining,walkSpeed*Job.TravelSpeedMultiplier);
        StateTime+=seconds-remaining;
        if(!Movement.IsFollowingPath)Arrive();
      }
      if(State==WorkerState.Working && remaining>0)
      { StateTime+=remaining;Job.AdvanceWork(this,remaining); }
    }
    private void Arrive()
    {
      State=WorkerState.Working;StateTime=0;
      Job.Arrive(this);
    }
  }
}
