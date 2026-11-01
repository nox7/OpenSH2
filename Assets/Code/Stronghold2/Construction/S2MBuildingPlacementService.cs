using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.Construction
{
    public enum BuildingPlacementFailure { None,InvalidProposal,TerrainOrOccupancy,Troops,Navigation,AdditionalRule }
    public readonly struct BuildingPlacementCheck
    {
        public readonly BuildingPlacementFailure Failure;public readonly float RawHeight;
        public bool Allowed=>Failure==BuildingPlacementFailure.None;
        public BuildingPlacementCheck(BuildingPlacementFailure failure,float height=0){Failure=failure;RawHeight=height;}
    }
    /// <summary>Read-only placement queries for AI searches, future cursor previews and blueprint sites. No costs or actors are created.</summary>
    public sealed class S2MBuildingPlacementService
    {
        private readonly S2MFile map;private readonly NavigationGrid navigation;private readonly StartupBuildingSettings settings;
        private S2MStartupBuildings.PlacementWorld world;
        private IReadOnlyList<BuildingPlacement> buildings;private IReadOnlyList<ActorPlacement> actors;
        private readonly Dictionary<(int,int,float),bool[]> troopMasks=new();
        public S2MBuildingPlacementService(S2MFile map,NavigationGrid navigation=null,StartupBuildingSettings settings=null)
        {this.map=map;this.navigation=navigation;this.settings=settings??new StartupBuildingSettings();this.settings.Validate();}
        public BuildingPlacementCheck Check(BuildingPlacement p,float troopRadius=7,Func<BuildingPlacement,bool> additionalRule=null)
        {
            if(p==null || p.FootprintWidth<1 || p.FootprintHeight<1 || !p.RuntimeEstateId.HasValue || Math.Abs(p.RotationDegrees/90-Math.Round(p.RotationDegrees/90))>.0001
                ||float.IsNaN(p.RotationDegrees)||float.IsInfinity(p.RotationDegrees)||Math.Abs(p.MapX-Math.Round(p.MapX))>.001||Math.Abs(p.MapZ-Math.Round(p.MapZ))>.001||float.IsNaN(p.RawX)||float.IsInfinity(p.RawX)||float.IsNaN(p.RawZ)||float.IsInfinity(p.RawZ)||float.IsNaN(troopRadius)||float.IsInfinity(troopRadius)||troopRadius<0||troopRadius>32)return new BuildingPlacementCheck(BuildingPlacementFailure.InvalidProposal);
            Refresh();
            if(!world.CanPlace(p,out float height))return new BuildingPlacementCheck(BuildingPlacementFailure.TerrainOrOccupancy);
            var key=(p.FootprintWidth,p.FootprintHeight,troopRadius);
            if(!troopMasks.TryGetValue(key,out var mask))troopMasks.Add(key,mask=world.CreateTroopMask(troopRadius,p.FootprintWidth*.5f+1.5f,p.FootprintHeight*.5f+1.5f));
            if(mask[(int)p.MapZ*map.MapSize+(int)p.MapX])return new BuildingPlacementCheck(BuildingPlacementFailure.Troops);
            if(navigation!=null)
            {
                var f=S2MBuildingFootprint.FromPlacement(p);int r=navigation.Resolution;
                for(int z=f.MinZ*r;z<f.MaxZExclusive*r;z++)for(int x=f.MinX*r;x<f.MaxXExclusive*r;x++)
                    if(!navigation.IsWalkable(new MapPoint((x+.5f)/r,(z+.5f)/r)))return new BuildingPlacementCheck(BuildingPlacementFailure.Navigation);
            }
            if(additionalRule!=null && !additionalRule(p))return new BuildingPlacementCheck(BuildingPlacementFailure.AdditionalRule);
            return new BuildingPlacementCheck(BuildingPlacementFailure.None,height);
        }
        private void Refresh()
        {
            if(world!=null && ReferenceEquals(buildings,map.BuildingPlacements) && ReferenceEquals(actors,map.Actors))return;
            if(world!=null && ReferenceEquals(actors,map.Actors) && buildings.Count<=map.BuildingPlacements.Count)
            {
                bool append=true;for(int i=0;i<buildings.Count;i++)if(!ReferenceEquals(buildings[i],map.BuildingPlacements[i])){append=false;break;}
                if(append){for(int i=buildings.Count;i<map.BuildingPlacements.Count;i++)world.Reserve(map.BuildingPlacements[i]);buildings=map.BuildingPlacements;return;}
            }
            world=new S2MStartupBuildings.PlacementWorld(map,map.BuildingPlacements,map.Keeps,settings);
            buildings=map.BuildingPlacements;actors=map.Actors;troopMasks.Clear();
        }
        public void Invalidate(){world=null;troopMasks.Clear();}
    }
}
