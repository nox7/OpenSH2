using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  [Serializable] public sealed class AppleFarmSettings
  {
    public float PlantedSeconds=5,GrowthStageSeconds=12.5f,RestSeconds=12.5f;
    public float HarvestSeasonSeconds=120,SeasonCooldownSeconds=17.5f;
    public float WalkSpeedCellsPerSecond=1,WorkAnimationSpeed=1,RetrySeconds=1,IdleReturnDelaySeconds=.8f;
    public float GranaryDepositSecondsPerApple=.1f;
    public int TreeSearchWorkUnitsPerFrame=64;
    public void Validate()
    {
      foreach(float value in new[]{PlantedSeconds,GrowthStageSeconds,RestSeconds,HarvestSeasonSeconds,SeasonCooldownSeconds,WalkSpeedCellsPerSecond,WorkAnimationSpeed,RetrySeconds,IdleReturnDelaySeconds,GranaryDepositSecondsPerApple})
        if(!(value>0) || float.IsInfinity(value))throw new ArgumentOutOfRangeException(nameof(AppleFarmSettings));
      if(TreeSearchWorkUnitsPerFrame<1)throw new ArgumentOutOfRangeException(nameof(TreeSearchWorkUnitsPerFrame));
    }
    public AppleFarmSettings Copy()=>(AppleFarmSettings)MemberwiseClone();
  }
  public sealed class OrchardTree
  {
    public const int Unplanted=-1,Ripe=4,Picked=5;
    public int Id { get; }public int FarmId { get; }public int EstateId { get; }
    public MapPoint Position { get; }public MapPoint RenderPosition { get; }public MapPoint Approach { get; }
    public float RawHeight { get; }public float Heading { get; }
    public int Stage { get; internal set; }public bool Active { get; internal set; }=true;
    internal int SavedTimerTicks;
    public int? PickerId { get; internal set; }public double RemainingSeconds { get; internal set; }
    internal int Seasons;
    public bool Harvestable=>Active && Stage==Ripe && !PickerId.HasValue;
    public OrchardTree(int id,int farm,int estate,MapPoint position,MapPoint renderPosition,MapPoint approach,float height,int stage=-1,int timerTicks=0,float heading=0)
    {
      if(stage< -1 || stage>5)throw new ArgumentOutOfRangeException(nameof(stage));
      Id=id;FarmId=farm;EstateId=estate;Position=position;RenderPosition=renderPosition;Approach=approach;RawHeight=height;Stage=stage;Heading=heading;
      SavedTimerTicks=timerTicks;RemainingSeconds=double.PositiveInfinity;
    }
  }
  /// <summary>One map-owned orchard clock and shared tree reservation index. No per-tree agents.</summary>
  public sealed class AppleOrchards : ILaborEnvironment
  {
    private sealed class Farm {public readonly List<OrchardTree> Trees=new();public double Season;public int Mode;}
    private readonly Dictionary<int,Farm> farms=new();
    private readonly Dictionary<(int Estate,int X,int Z),List<OrchardTree>> buckets=new();
    private readonly List<OrchardTree> trees=new();
    private readonly HashSet<int> ids=new();private readonly Dictionary<int,int> available=new();
    public IReadOnlyList<OrchardTree> Trees { get; }
    public AppleFarmSettings Settings { get; }public NavigationGrid Navigation { get; }
    public event Action<OrchardTree> Changed;
    public AppleOrchards(NavigationGrid navigation,AppleFarmSettings settings=null)
    {Navigation=navigation;settings??=new AppleFarmSettings();settings.Validate();Settings=settings.Copy();Trees=trees.AsReadOnly();}
    public void Add(OrchardTree tree)
    {
      if(!ids.Add(tree.Id))throw new ArgumentException("Duplicate orchard tree id.");
      if(!farms.TryGetValue(tree.FarmId,out var farm))farms.Add(tree.FarmId,farm=new Farm());
      farm.Trees.Add(tree);trees.Add(tree);var key=(tree.EstateId,(int)Math.Floor(tree.Position.X/16),(int)Math.Floor(tree.Position.Z/16));
      if(!buckets.TryGetValue(key,out var list))buckets.Add(key,list=new());list.Add(tree);
      if(tree.Stage==0)tree.RemainingSeconds=Math.Max(0,Settings.PlantedSeconds-tree.SavedTimerTicks/200.0*Settings.PlantedSeconds);
      else if(tree.Stage>0 && tree.Stage<4)tree.RemainingSeconds=Math.Max(0,Settings.GrowthStageSeconds-tree.SavedTimerTicks/500.0*Settings.GrowthStageSeconds);
      else if(tree.Stage==5 && tree.SavedTimerTicks>0)tree.RemainingSeconds=tree.SavedTimerTicks/500.0*Settings.RestSeconds;
      if(tree.Harvestable)Available(tree.EstateId,1);
    }
    public IReadOnlyList<OrchardTree> FarmTrees(int id)=>farms.TryGetValue(id,out var farm)?farm.Trees:Array.Empty<OrchardTree>();
    private void Available(int estate,int delta){available.TryGetValue(estate,out int count);available[estate]=count+delta;}
    private void Stage(OrchardTree tree,int stage,double seconds)
    {if(tree.Harvestable)Available(tree.EstateId,-1);tree.Stage=stage;tree.RemainingSeconds=seconds;tree.PickerId=null;if(tree.Harvestable)Available(tree.EstateId,1);Changed?.Invoke(tree);}
    public bool TryReserve(OrchardTree tree,int worker,int estate,bool planting=false)
    {if(!tree.Active || tree.EstateId!=estate || tree.PickerId.HasValue || tree.Stage!=(planting?-1:4))return false;if(tree.Harvestable)Available(estate,-1);tree.PickerId=worker;return true;}
    public void Release(OrchardTree tree,int worker){if(tree?.PickerId==worker){tree.PickerId=null;if(tree.Harvestable)Available(tree.EstateId,1);}}
    public bool Plant(OrchardTree tree,int worker)
    {if(tree==null || !tree.Active || tree.PickerId!=worker || tree.Stage!= -1)return false;Stage(tree,0,Settings.PlantedSeconds);return true;}
    public bool Harvest(OrchardTree tree,int worker)
    {if(tree==null || !tree.Active || tree.PickerId!=worker || tree.Stage!=4)return false;Stage(tree,5,double.PositiveInfinity);return true;}
    public void RemoveFarm(int id)
    {if(!farms.TryGetValue(id,out var farm))return;foreach(var tree in farm.Trees){if(tree.Harvestable)Available(tree.EstateId,-1);tree.Active=false;tree.PickerId=null;Changed?.Invoke(tree);}farms.Remove(id);}
    public void Advance(float seconds)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      foreach(var farm in farms.Values)
      {
        double left=seconds;
        while(left>1e-8)
        {
          bool grown=true,picked=true;foreach(var tree in farm.Trees)
          {grown&=tree.Stage==4 || tree.Stage==5 && double.IsPositiveInfinity(tree.RemainingSeconds);picked&=tree.Stage==5;}
          if(farm.Mode==0 && grown){farm.Mode=1;farm.Season=Settings.HarvestSeasonSeconds;}
          if(farm.Mode==1 && picked)farm.Season=Math.Min(farm.Season,1.0/40);
          double step=left;if(farm.Mode!=0)step=Math.Min(step,farm.Season);
          foreach(var tree in farm.Trees)step=Math.Min(step,tree.RemainingSeconds);
          left-=step;if(farm.Mode!=0)farm.Season-=step;
          foreach(var tree in farm.Trees)
          {
            tree.RemainingSeconds-=step;if(tree.RemainingSeconds>1e-8)continue;
            if(tree.Stage>=0 && tree.Stage<4)Stage(tree,tree.Stage+1,tree.Stage==3?double.PositiveInfinity:Settings.GrowthStageSeconds);
            else if(tree.Stage==5)
            {
              // Stable stagger after winter/rest, independent of worker order or frame rate.
              uint seed=unchecked((uint)tree.Id*73856093u+(uint)++tree.Seasons*19349663u);
              Stage(tree,1,Settings.GrowthStageSeconds*(.5+(seed%500)/500.0));
            }
          }
          if(farm.Mode!=0 && farm.Season<=1e-8)
          {
            if(farm.Mode==1){foreach(var tree in farm.Trees)Stage(tree,5,Settings.RestSeconds);farm.Mode=-1;farm.Season=Settings.SeasonCooldownSeconds;}
            else farm.Mode=0;
          }
        }
      }
    }
    public NearestTreeSearch FindNearest(int estate,MapPoint origin)=>new(this,estate,origin);
    /// <summary>Incremental spatial query; each step examines one tree or one bucket.</summary>
    public sealed class NearestTreeSearch
    {
      private readonly AppleOrchards owner;private readonly int estate;private readonly MapPoint origin;
      private readonly IEnumerator<(int X,int Z)> cells;private List<OrchardTree> bucket;private int index;
      private float best=float.PositiveInfinity;
      public OrchardTree Result { get; private set; }public bool Complete { get; private set; }
      internal NearestTreeSearch(AppleOrchards owner,int estate,MapPoint origin)
      {this.owner=owner;this.estate=estate;this.origin=origin;cells=Cells().GetEnumerator();if(!owner.available.TryGetValue(estate,out int count) || count==0){Complete=true;cells.Dispose();}}
      public void Step()
      {
        if(Complete)return;
        if(bucket!=null && index<bucket.Count)
        {
          var tree=bucket[index++];if(!tree.Harvestable)return;float d=MapPoint.Distance(origin,tree.Position);
          if(d>best || d==best && Result!=null && tree.Id>=Result.Id || !owner.Navigation.CanReach(origin,tree.Approach))return;
          Result=tree;best=d;return;
        }
        if(!cells.MoveNext()){Complete=true;cells.Dispose();return;}
        var cell=cells.Current;owner.buckets.TryGetValue((estate,cell.X,cell.Z),out bucket);index=0;
      }
      private IEnumerable<(int X,int Z)> Cells()
      {
        int cx=(int)Math.Floor(origin.X/16),cz=(int)Math.Floor(origin.Z/16);
        int maxX=(owner.Navigation.Width-1)/owner.Navigation.Resolution/16,maxZ=(owner.Navigation.Height-1)/owner.Navigation.Resolution/16;
        int limit=Math.Max(Math.Max(cx,cz),Math.Max(maxX-cx,maxZ-cz));
        for(int ring=0;ring<=limit;ring++)
        {
          if(ring>0 && Math.Min(Math.Min(origin.X-(cx-ring+1)*16,(cx+ring)*16-origin.X),Math.Min(origin.Z-(cz-ring+1)*16,(cz+ring)*16-origin.Z))>best)yield break;
          for(int z=Math.Max(0,cz-ring);z<=Math.Min(maxZ,cz+ring);z++)for(int x=Math.Max(0,cx-ring);x<=Math.Min(maxX,cx+ring);x++)
            if(ring==0 || Math.Abs(x-cx)==ring || Math.Abs(z-cz)==ring)yield return(x,z);
        }
      }
    }
  }
}
