using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Movement
{
  /// <summary>Reusable map-space locomotion; callers choose walk/run speed and own activity transitions.</summary>
  public sealed class MovementComponent
  {
    public MapPoint Position { get; internal set; }
    public float Heading { get; internal set; }
    public bool IsFollowingPath => path != null && waypoint < path.Count;
    private IReadOnlyList<MapPoint> path;
    private int waypoint;
    public double DistanceTravelled { get; private set; }
    public bool CanContinue(NavigationGrid navigation)
    {
      var from=Position;
      for(int i=waypoint;path!=null && i<path.Count;i++)
      { if(!navigation.CanTravel(from,path[i]))return false;from=path[i]; }
      return true;
    }

    public MovementComponent(MapPoint position,float heading) { Position=position;Heading=heading; }
    public void Follow(IReadOnlyList<MapPoint> route)
    {
      if(route==null || route.Count==0)throw new ArgumentException("A movement route must contain a start point.",nameof(route));
      path=route;waypoint=1;
    }
    public void Place(MapPoint position,float heading)
    { Position=position;Heading=heading;path=null;waypoint=0; }
    public void Advance(ref float remaining,float speed)
    {
      Validate(speed);
      while(IsFollowingPath)
      {
        if(!MoveTo(path[waypoint],ref remaining,speed))return;
        waypoint++;
        if(remaining<=0 && IsFollowingPath)return;
      }
    }
    public bool MoveTo(MapPoint goal,ref float remaining,float speed)
    {
      Validate(speed);
      float distance=MapPoint.Distance(Position,goal);
      if(distance<.0001f) { Position=goal;return true; }
      if(remaining<=0)return false;
      float step=Math.Min(distance,speed*remaining),t=step/distance;
      Heading=(float)(Math.Atan2(goal.X-Position.X,goal.Z-Position.Z)*180/Math.PI);
      Position=new MapPoint(Position.X+(goal.X-Position.X)*t,Position.Z+(goal.Z-Position.Z)*t);
      DistanceTravelled+=step;
      remaining=Math.Max(0,remaining-step/speed);
      if(step>=distance) { Position=goal;return true; }
      return false;
    }
    private static void Validate(float speed)
    { if(!(speed>0) || float.IsInfinity(speed))throw new ArgumentOutOfRangeException(nameof(speed)); }
  }
}
