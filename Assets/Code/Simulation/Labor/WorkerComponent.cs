using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Movement;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public enum WorkerState { Unassigned, Traveling, Working, Removed }

  /// <summary>Employment component, independent of recruitment, building type, and rendering.</summary>
  public sealed class WorkerComponent
  {
    public int ActorId { get; }
    public MovementComponent Movement { get; }
    public LaborJob Job { get; private set; }
    public WorkerState State { get; private set; }
    public float StateTime { get; private set; }
    public float WalkSpeed { get; private set; }
    public bool WaitingForPath { get; private set; }
    private int navigationRevision=-1;
    private float pathRetry;
    public event Action<WorkerComponent,LaborJob> Removed;
    public void Remove()
    {
      if(State==WorkerState.Removed)return;
      var former=Job;former?.RemoveWorker(this);Job=null;State=WorkerState.Removed;
      Movement.Place(Movement.Position,Movement.Heading);Removed?.Invoke(this,former);
    }
    public WorkerComponent(int actorId,MovementComponent movement)
    { ActorId=actorId;Movement=movement??throw new ArgumentNullException(nameof(movement)); }

    public bool TryAssign(LaborJob job,IReadOnlyList<MapPoint> path)
    {
      if(job==null)throw new ArgumentNullException(nameof(job));
      if(State==WorkerState.Removed || Job!=null || path==null || path.Count==0 || MapPoint.Distance(path[path.Count-1],job.Approach)>.001f)return false;
      if(!job.TryReserve(ActorId))return false;
      Job=job;State=WorkerState.Traveling;StateTime=0;navigationRevision=-1;WaitingForPath=false;Movement.Follow(path);
      if(!Movement.IsFollowingPath)Arrive();
      return true;
    }
    public void Advance(float seconds,float walkSpeed,NavigationGrid navigation=null)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      if(seconds==0)return;
      WalkSpeed=walkSpeed;
      float remaining=seconds;
      if(State==WorkerState.Traveling)
      {
        if(navigation!=null)
        {
          if(navigationRevision!=navigation.Revision)
          {
            if(Movement.RouteMayHaveChanged(navigation,navigationRevision) && !Movement.CanContinue(navigation))WaitingForPath=true;
            navigationRevision=navigation.Revision;pathRetry=0;
          }
          if(WaitingForPath)
          {
            pathRetry-=seconds;
            if(pathRetry>0)return;
            if(!navigation.TryFindPath(Movement.Position,Job.Approach,out var replacement)){pathRetry=1;return;}
            Movement.Follow(replacement);WaitingForPath=false;
          }
        }
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
