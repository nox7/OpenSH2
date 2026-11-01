using System;
using System.Collections.Generic;

namespace Assets.Code.Simulation.AI
{
    [Serializable] public sealed class AiSettings
    {
        public bool Enabled=true;
        public double EconomyIntervalSeconds=1, StrategyIntervalSeconds=5, ReactionDelaySeconds=.25;
        public int MaximumEvaluationsPerAdvance=8;
        public int MaximumThreatExpiriesPerAdvance=32;
        public int WorkUnitsPerEvaluation=128;
        public void Validate()
        {
            foreach(double v in new[]{EconomyIntervalSeconds,StrategyIntervalSeconds,ReactionDelaySeconds})
                if(!(v>0)||double.IsInfinity(v))throw new ArgumentOutOfRangeException(nameof(AiSettings));
            if(MaximumEvaluationsPerAdvance<2)throw new ArgumentOutOfRangeException(nameof(MaximumEvaluationsPerAdvance));
            if(MaximumThreatExpiriesPerAdvance<1)throw new ArgumentOutOfRangeException(nameof(MaximumThreatExpiriesPerAdvance));
            if(WorkUnitsPerEvaluation<1)throw new ArgumentOutOfRangeException(nameof(WorkUnitsPerEvaluation));
        }
    }
    /// <summary>Cooperative logical budget. A planner must yield its resumable job when TrySpend fails.</summary>
    public sealed class AiWorkBudget
    {
        public int Remaining { get; private set; }
        internal void Reset(int units)=>Remaining=units;
        public bool TrySpend(int units=1)
        {if(units<1)throw new ArgumentOutOfRangeException(nameof(units));if(units>Remaining)return false;Remaining-=units;return true;}
    }
    [Flags] public enum AiWork { None=0, Economy=1, Strategy=2, Reaction=4 }
    public readonly struct AiEvaluation
    {
        public readonly AiTerritory Territory;
        public readonly IAiEconomyView Economy;
        public readonly AiWork Work;
        public readonly AiChange Changes;
        public readonly double Now;
        public readonly AiWorkBudget Budget;
        public AiEvaluation(AiTerritory territory,IAiEconomyView economy,AiWork work,AiChange changes,double now,AiWorkBudget budget)
        {Territory=territory;Economy=economy;Work=work;Changes=changes;Now=now;Budget=budget;}
    }
    /// <summary>Shared, bounded, simulation-thread policy. No Unity calls, blocking I/O or unbounded searches.</summary>
    public interface IAiPlanner { void Evaluate(in AiEvaluation evaluation); }

    /// <summary>One coalesced urgent entry and one periodic entry per territory; bounded work per Advance.</summary>
    public sealed class AiScheduler : IDisposable
    {
        private sealed class Entry
        {
            public int Id;public double EconomyAt,StrategyAt,RoutineAt,UrgentAt=double.PositiveInfinity,LastAt=double.NegativeInfinity;
            public AiChange Dirty=AiChange.All;
        }
        private sealed class Order : IComparer<Entry>
        {
            private readonly bool urgent;
            public Order(bool urgent)=>this.urgent=urgent;
            public int Compare(Entry a,Entry b)
            {int c=(urgent?a.UrgentAt:a.RoutineAt).CompareTo(urgent?b.UrgentAt:b.RoutineAt);return c!=0?c:a.Id.CompareTo(b.Id);}
        }
        private readonly AiWorld world;
        private readonly IAiEconomyView economy;
        private readonly AiSettings settings;
        private IAiPlanner planner;
        public void SetPlanner(IAiPlanner value)
        {if(disposed)throw new ObjectDisposedException(nameof(AiScheduler));if(dispatching)throw new InvalidOperationException("Cannot replace a running planner.");planner=value;}
        private readonly AiWorkBudget budget=new();
        private readonly Dictionary<int,Entry> entries=new();
        private readonly SortedSet<Entry> routine=new(new Order(false)), urgent=new(new Order(true));
        private bool disposed,dispatching;
        public double Now { get; private set; }
        public long EvaluationCount { get; private set; }
        public long PlannerFailureCount { get; private set; }
        public event Action<int,Exception> PlannerFaulted;
        public int LastAdvanceEvaluations { get; private set; }
        public int RegisteredTerritories=>entries.Count;
        public int PendingWakeups=>urgent.Count;
        public AiScheduler(AiWorld world,IAiEconomyView economy,AiSettings settings=null,IAiPlanner planner=null,double initialTime=0)
        {
            this.world=world??throw new ArgumentNullException(nameof(world));this.economy=economy;
            this.settings=settings??new AiSettings();this.settings.Validate();this.planner=planner;
            if(double.IsNaN(initialTime)||double.IsInfinity(initialTime)||initialTime<0)throw new ArgumentOutOfRangeException(nameof(initialTime));
            Now=initialTime;world.AdvanceTime(Now,this.settings.MaximumThreatExpiriesPerAdvance);
            foreach(var t in world.Territories.Values)Register(t);
            world.Changed+=OnChange;
        }
        private void Register(AiTerritory t)
        {
            if(!t.CanPlan)return;
            // Deterministic phases spread startup work without a random generator or per-frame scan.
            double phase=((uint)t.Id*2654435761u%997)/997.0;
            var e=new Entry{Id=t.Id,EconomyAt=Now+phase*settings.EconomyIntervalSeconds,StrategyAt=Now+phase*settings.StrategyIntervalSeconds};
            e.RoutineAt=Math.Min(e.EconomyAt,e.StrategyAt);entries.Add(e.Id,e);routine.Add(e);
        }
        private void OnChange(int id,AiChange change)
        {
            if(entries.TryGetValue(id,out var e))
            {
                if(!world.TryGet(id,out var existing)||!existing.CanPlan)
                {routine.Remove(e);urgent.Remove(e);entries.Remove(id);return;}
            }
            else
            {
                if(world.TryGet(id,out var added))Register(added);
                if(!entries.TryGetValue(id,out e))return;
            }
            e.Dirty|=change;
            if(double.IsPositiveInfinity(e.UrgentAt))
            {
                e.UrgentAt=Math.Max(Now+settings.ReactionDelaySeconds,e.LastAt+settings.ReactionDelaySeconds);
                urgent.Add(e);
            }
        }
        public void Advance(double simulationSeconds)
        {
            if(disposed)throw new ObjectDisposedException(nameof(AiScheduler));
            if(dispatching)throw new InvalidOperationException("AI Advance is not reentrant.");
            if(double.IsNaN(simulationSeconds)||double.IsInfinity(simulationSeconds)||simulationSeconds<0)throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            LastAdvanceEvaluations=0;
            if(simulationSeconds==0)return;
            Now+=simulationSeconds;
            world.AdvanceTime(Now,settings.MaximumThreatExpiriesPerAdvance);
            if(!settings.Enabled)return;
            int urgentUsed=0;
            while(LastAdvanceEvaluations<settings.MaximumEvaluationsPerAdvance)
            {
                bool hasUrgent=urgent.Count>0 && urgent.Min.UrgentAt<=Now;
                bool hasRoutine=routine.Count>0 && routine.Min.RoutineAt<=Now;
                if(!hasUrgent&&!hasRoutine)break;
                bool react=hasUrgent && (!hasRoutine || urgentUsed<settings.MaximumEvaluationsPerAdvance/2);
                var e=react?urgent.Min:routine.Min;
                routine.Remove(e);urgent.Remove(e);e.UrgentAt=double.PositiveInfinity;
                var t=world.Territories[e.Id];
                var work=react?AiWork.Reaction:AiWork.None;
                if(e.EconomyAt<=Now){work|=AiWork.Economy;e.EconomyAt=Now+settings.EconomyIntervalSeconds;}
                if(e.StrategyAt<=Now){work|=AiWork.Strategy;e.StrategyAt=Now+settings.StrategyIntervalSeconds;}
                var changes=e.Dirty;e.Dirty=AiChange.None;e.LastAt=Now;
                if(changes!=AiChange.None)work|=AiWork.Reaction;
                try
                {
                    dispatching=true;
                    if(planner!=null){budget.Reset(settings.WorkUnitsPerEvaluation);var evaluation=new AiEvaluation(t,economy,work,changes,Now,budget);planner.Evaluate(in evaluation);}
                }
                catch(Exception error)
                {PlannerFailureCount++;e.Dirty|=changes;PlannerFaulted?.Invoke(e.Id,error);}
                finally
                {
                    dispatching=false;
                    // Planner callbacks may publish changes; don't lose their coalesced wakeup.
                    if(entries.TryGetValue(e.Id,out var current) && ReferenceEquals(current,e))
                    {e.RoutineAt=Math.Min(Math.Min(e.EconomyAt,e.StrategyAt),t.NextThreatExpiry);routine.Add(e);}
                }
                if(react)urgentUsed++;
                LastAdvanceEvaluations++;EvaluationCount++;
            }
        }
        public void Dispose()
        {if(disposed)return;disposed=true;world.Changed-=OnChange;entries.Clear();routine.Clear();urgent.Clear();}
    }
}
