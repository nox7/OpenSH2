using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Movement;

namespace Assets.Code.Simulation.Population
{
  public enum PeasantState { Unspawned, HovelSeated, StandingUp, LeavingHovel, Running, WaitingForPath, Seated, WalkingToJob, Working, Removed }

  public sealed class Peasant
  {
    public int Id { get; }
    public int SourceBuildingId { get; }
    public int EstateId { get; }
    public MovementComponent Movement { get; }
    public WorkerComponent Worker { get; }
    public LaborJob Job => Worker.Job;
    public bool FromHovel { get; }
    public MapPoint Spawn { get; }
    public MapPoint Exit { get; }
    public MapPoint Seat { get; }
    public float SeatHeading { get; }
    public float Heading { get => Movement.Heading; internal set => Movement.Heading=value; }
    public MapPoint Position { get => Movement.Position; internal set => Movement.Position=value; }
    private PeasantState recruitmentState;
    private float recruitmentTime;
    public PeasantState State
    {
      get => Worker.State switch { WorkerState.Traveling=>PeasantState.WalkingToJob,WorkerState.Working=>PeasantState.Working,WorkerState.Removed=>PeasantState.Removed,_=>recruitmentState };
      internal set => recruitmentState=value;
    }
    public float StateTime { get => Job==null ? recruitmentTime : Worker.StateTime; internal set => recruitmentTime=value; }
    public Peasant(int id,int sourceBuildingId,bool fromHovel,MapPoint spawn,MapPoint exit,MapPoint seat,float seatHeading,float heading,int estateId=-1)
    {
      Id=id;SourceBuildingId=sourceBuildingId;FromHovel=fromHovel;Spawn=spawn;Exit=exit;Seat=seat;SeatHeading=seatHeading;EstateId=estateId;
      Movement=new MovementComponent(spawn,heading);Worker=new WorkerComponent(id,Movement);
    }
  }

  /// <summary>Timed estate recruitment and movement. Rendering consumes this state, never owns it.</summary>
  public sealed class PeasantRecruitment
  {
    public IReadOnlyList<Peasant> Peasants { get; }
    public int SpawnedCount { get; private set; }
    private readonly NavigationGrid navigation;
    private readonly PopulationSettings settings;
    private readonly float standDuration;
    private double timeUntilSpawn;
    public PeasantRecruitment(IReadOnlyList<Peasant> peasants,NavigationGrid navigation,PopulationSettings settings,float standDuration)
    {
      settings.Validate(); if(!(standDuration>0) || float.IsInfinity(standDuration))throw new ArgumentOutOfRangeException(nameof(standDuration));
      Peasants=peasants; this.navigation=navigation; this.settings=settings; this.standDuration=standDuration; timeUntilSpawn=settings.SpawnIntervalSeconds;
    }
    public void Advance(float seconds)
    {
      if (!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      // Process spawn boundaries in order: a large frame must not age a just-born peasant by the whole frame.
      float remaining=seconds;
      while(remaining>0)
      {
        float step=SpawnedCount<Peasants.Count ? Math.Min(remaining,(float)timeUntilSpawn) : remaining;
        for(int i=0;i<SpawnedCount;i++) Tick(Peasants[i],step);
        remaining-=step;
        if(SpawnedCount>=Peasants.Count)break;
        timeUntilSpawn-=step;
        if(timeUntilSpawn<=.000001)
        {
          var p=Peasants[SpawnedCount++]; Change(p,p.FromHovel ? PeasantState.HovelSeated : PeasantState.WaitingForPath);
          if(!p.FromHovel) Plan(p);
          timeUntilSpawn=settings.SpawnIntervalSeconds;
        }
      }
    }
    private void Tick(Peasant p,float seconds)
    {
      if(p.Worker.State==WorkerState.Removed)return;
      if(p.Job!=null) { p.Worker.Advance(seconds,settings.WalkSpeedCellsPerSecond);return; }
      float remaining=seconds;
      while(remaining>0)
      {
        if(p.State==PeasantState.HovelSeated || p.State==PeasantState.StandingUp || p.State==PeasantState.WaitingForPath)
        {
          float duration=p.State==PeasantState.HovelSeated ? settings.HovelSeatedSeconds : p.State==PeasantState.StandingUp ? standDuration : 5;
          float step=Math.Min(remaining,Math.Max(0,duration-p.StateTime)); p.StateTime+=step; remaining-=step;
          if(p.StateTime<duration)break;
          if(p.State==PeasantState.HovelSeated)Change(p,PeasantState.StandingUp);
          else if(p.State==PeasantState.StandingUp)Change(p,PeasantState.LeavingHovel);
          else { p.StateTime=0; Plan(p); }
        }
        else if(p.State==PeasantState.LeavingHovel)
        {
          Move(p,p.Exit,ref remaining);
          if(MapPoint.Distance(p.Position,p.Exit)<.0001f)Plan(p); else break;
        }
        else if(p.State==PeasantState.Running)
        {
          float before=remaining;p.Movement.Advance(ref remaining,settings.RunSpeedCellsPerSecond);p.StateTime+=before-remaining;
          if(!p.Movement.IsFollowingPath) { p.Movement.Place(p.Seat,p.SeatHeading);Change(p,PeasantState.Seated); }
          else break;
        }
        else { p.StateTime+=remaining; break; }
      }
    }
    private void Plan(Peasant p)
    {
      if(navigation.TryFindPath(p.Position,p.Seat,out var path)) { p.Movement.Follow(path);Change(p,PeasantState.Running); }
      else Change(p,PeasantState.WaitingForPath);
    }
    private void Move(Peasant p,MapPoint goal,ref float remaining)
    {
      float before=remaining;p.Movement.MoveTo(goal,ref remaining,settings.RunSpeedCellsPerSecond);p.StateTime+=before-remaining;
    }
    private static void Change(Peasant p,PeasantState state) { p.State=state; p.StateTime=0; }
  }
}
