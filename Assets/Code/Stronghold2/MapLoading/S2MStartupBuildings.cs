using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;
namespace Assets.Code.Stronghold2.MapLoading
{
    [Serializable]
    public sealed class StartupBuildingSettings
    {
        public bool Enabled = true;
        public KeepSize DefaultKeepSize = KeepSize.Medium;
        public int BlueprintKeepSearchRadius=12;
        public bool AlignBlueprintAnchorsToEvenTiles=true;
        [UnityEngine.Tooltip("Maximum placement search distances in map cells. Searches stay inside the marker's estate.")]
        public int KeepSearchRadius = 6, EstateSearchRadius = 10, StoreSearchRadius = 40;
        [UnityEngine.Tooltip("Keep entrance offset from its model origin, in local map cells. The default matches the medium keep.")]
        public float KeepEntranceX = 2, KeepEntranceZ = -1;
        [UnityEngine.Tooltip("Preferred store origins relative to the estate marker, in map cells. Occupied sites trigger a nearby search.")]
        public float StockpileOffsetX = -4, StockpileOffsetZ = 0;
        [UnityEngine.Tooltip("Horizontal troop exclusion radius for estate flagpole placement, in cells. Includes all allegiances and wall troops. Zero disables this check.")]
        public float EstateTroopExclusionRadius = 6;
        [UnityEngine.Tooltip("Maximum ground height difference across a footprint in raw map units (1024 = one standard cell).")]
        public float MaximumHeightVariation = 512;
        public void Validate()
        {
            if (KeepSearchRadius < 0 || BlueprintKeepSearchRadius<0 || BlueprintKeepSearchRadius>64 || EstateSearchRadius < 0 || StoreSearchRadius < 0 || KeepSearchRadius > 64 || EstateSearchRadius > 64 || StoreSearchRadius > 128 || !Enum.IsDefined(typeof(KeepSize), DefaultKeepSize)) throw new ArgumentOutOfRangeException(nameof(StartupBuildingSettings));
            foreach (float f in new[] { KeepEntranceX, KeepEntranceZ, StockpileOffsetX, StockpileOffsetZ, MaximumHeightVariation, EstateTroopExclusionRadius }) if (float.IsNaN(f) || float.IsInfinity(f)) throw new ArgumentOutOfRangeException(nameof(StartupBuildingSettings));
            if (MaximumHeightVariation < 0) throw new ArgumentOutOfRangeException(nameof(MaximumHeightVariation));
            if (EstateTroopExclusionRadius < 0 || EstateTroopExclusionRadius > 32) throw new ArgumentOutOfRangeException(nameof(EstateTroopExclusionRadius));
        }
    }
    /// <summary>Deterministic, once-per-map simulation startup. The original map file is never edited.</summary>
    public static class S2MStartupBuildings
    {
        public static void Apply(S2MFile map, string installation, StartupBuildingSettings settings = null,S2MCastleBlueprints blueprints=null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map)); settings ??= new StartupBuildingSettings(); settings.Validate();
            if (map.StartupBuildingsInitialized || !settings.Enabled) return;
            using var temporaryBlueprints=blueprints==null?S2MAiRuntime.PrepareCastleBlueprints(map,installation):null;
            blueprints??=temporaryBlueprints;
            if(!blueprints.OwnsMap(map))throw new ArgumentException("Castle blueprints belong to another map or were disposed.");
            var castleLayouts=new Dictionary<int,AicCastleLayout>();
            var placements = new List<BuildingPlacement>(map.BuildingPlacements); var keeps = new List<Keep>(map.Keeps); var generated = new List<BuildingPlacement>(); var issues = new List<string>();
            var world = new PlacementWorld(map, placements, keeps, settings); var descriptors = new Dictionary<string, S2MBuildingDescriptor>(StringComparer.Ordinal);
            var ids = new HashSet<int>(map.S2GameObjects.Select(o => o.Id)); foreach (var p in placements) ids.Add(p.Id); foreach (var p in keeps) ids.Add(p.Id);
            int next = int.MaxValue;
            var villages = new List<EstateMarkerFlag>();
            var searches = new Dictionary<(int Radius, bool Pole), List<(int X, int Z)>>();
            foreach (var marker in map.EstateMarkers.OrderBy(m => m.EstateId))
            {
                if (marker.EstateId <= 0 || marker.Type == EstateType.None) continue;
                if (marker.X < 0 || marker.Z < 0 || marker.X >= map.MapSize || marker.Z >= map.MapSize) { issues.Add($"Estate {marker.EstateId} marker is outside the playable map."); continue; }
                if (marker.Type == EstateType.Castle)
                {
                    if (keeps.Any(k => S2MCampfireLayout.EstateAtBuilding(map, k) == marker.EstateId)) continue;
                    KeepSize size=settings.DefaultKeepSize;BuildingPlacement placed=null;AicCastleLayout layout=null;bool blueprintKeep=false;
                    blueprints.Kingdoms.TryGetValue(marker.EstateId,out var kingdom);
                    var selectedKeep=kingdom?.Selected?.PrimaryKeep;
                    if(selectedKeep.HasValue)size=(KeepSize)((int)selectedKeep.Value.KeepKind.Value-20);
                    if(selectedKeep is AicBuildItem keepItem && keepItem.RotationCode<=3)
                    {
                        blueprintKeep=true;
                        size=(KeepSize)((int)keepItem.KeepKind.Value-20);
                        var native=Descriptor(KeepStem(size));
                        var turns=kingdom.RotationQuarterTurns.HasValue?new[]{kingdom.RotationQuarterTurns.Value}:new[]{0,1,2,3};
                        foreach(var offset in Candidates(settings.BlueprintKeepSearchRadius,false))
                        {
                            int x=marker.X+offset.X,z=marker.Z+offset.Z;
                            if(settings.AlignBlueprintAnchorsToEvenTiles && ((x&1)!=0 || (z&1)!=0))continue;
                            foreach(int turn in turns)
                            {
                                var proposed=AicCastleLayout.AtKeepMarker(kingdom.Selected,x,z,turn);
                                var site=proposed.Project(keepItem,native.Width,native.Height);
                                placed=Find(marker,"Keep"+((int)size+1),native,site.ModelX,site.ModelZ,site.RotationQuarterTurns*90,0);
                                if(placed!=null){layout=proposed;break;}
                            }
                            if(placed!=null)break;
                        }
                        // A constrained blueprint anchor may fail even when a nearby ordinary keep site is usable.
                        if(placed==null)foreach(int turn in turns)
                        {
                            var proposed=AicCastleLayout.AtKeepMarker(kingdom.Selected,marker.X,marker.Z,turn);
                            var site=proposed.Project(keepItem,native.Width,native.Height);
                            placed=Find(marker,"Keep"+((int)size+1),native,site.ModelX,site.ModelZ,site.RotationQuarterTurns*90,settings.BlueprintKeepSearchRadius);
                            if(placed==null)continue;
                            layout=new AicCastleLayout(kingdom.Selected,proposed.OriginX+(int)placed.MapX-site.ModelX,proposed.OriginZ+(int)placed.MapZ-site.ModelZ,turn);
                            issues.Add($"Estate {marker.EstateId} used a general keep site after blueprint anchor sites were blocked.");break;
                        }
                    }
                    else if(kingdom?.Selected!=null)issues.Add($"Estate {marker.EstateId} blueprint has no supported keep entry/rotation; using general keep placement.");
                    var descriptor = Descriptor(KeepStem(size));
                    for (int turn = 0; turn < 4 && placed == null && !blueprintKeep; turn++)
                    {
                        double angle = turn * Math.PI / 2; float dx = (float)(settings.KeepEntranceX * Math.Cos(angle) + settings.KeepEntranceZ * Math.Sin(angle)); float dz = (float)(settings.KeepEntranceZ * Math.Cos(angle) - settings.KeepEntranceX * Math.Sin(angle));
                        placed = Find(marker, "Keep", descriptor, marker.X - dx, marker.Z - dz, turn * 90, settings.KeepSearchRadius);
                    }
                    if (placed == null) { issues.Add($"No valid keep site for estate {marker.EstateId} within {(blueprintKeep?settings.BlueprintKeepSearchRadius:settings.KeepSearchRadius)} cells."); continue; }
                    var keep = new Keep { Id = placed.Id, Type = "Keep" + ((int)size + 1), Size = size, RawX = placed.RawX, RawY = placed.RawY, RawZ = placed.RawZ, RotationDegrees = placed.RotationDegrees, FootprintWidth = placed.FootprintWidth, FootprintHeight = placed.FootprintHeight, StartupEstateId = marker.EstateId };
                    keeps.Add(keep); placements.Add(keep); generated.Add(keep); world.Reserve(keep);
                    if(layout!=null){castleLayouts[marker.EstateId]=layout;blueprints.TrySetLayout(marker.EstateId,layout);}
                    continue;
                }
                villages.Add(marker);
                AddIfMissing(marker, "VillageFlagPole", "flagpole_village_centre", marker.X, marker.Z, settings.EstateSearchRadius);
            }
            // Reserve all village poles before looking for their stockpiles.
            foreach (var marker in villages)
                AddIfMissing(marker, "StockPile", "stockpile", marker.X + settings.StockpileOffsetX, marker.Z + settings.StockpileOffsetZ, settings.StoreSearchRadius);
            map.BuildingPlacements = placements.AsReadOnly(); map.Keeps = keeps.AsReadOnly(); map.StartupBuildings = generated.AsReadOnly(); map.StartupPlacementIssues = issues.AsReadOnly(); map.StartupBuildingsInitialized = true;
            map.StartupCastleLayouts=new ReadOnlyDictionary<int,AicCastleLayout>(castleLayouts);
            void AddIfMissing(EstateMarkerFlag marker, string type, string stem, float x, float z, int radius)
            {
                if (placements.Any(p => p.Type == type && S2MCampfireLayout.EstateAtBuilding(map, p) == marker.EstateId)) return;
                var placed = Find(marker, type, Descriptor(stem), x, z, 0, radius);
                if (placed == null) { issues.Add($"No valid {type} site for estate {marker.EstateId} within {radius} cells."); return; }
                placements.Add(placed); generated.Add(placed); world.Reserve(placed);
            }
            S2MBuildingDescriptor Descriptor(string stem)
            { if (!descriptors.TryGetValue(stem, out var result)) descriptors.Add(stem, result = S2MBuildingDescriptorReader.Read(installation, "meshes/buildings/" + stem + ".bdg")); return result; }
            BuildingPlacement Find(EstateMarkerFlag marker, string type, S2MBuildingDescriptor descriptor, float desiredX, float desiredZ, float yaw, int radius)
            {
                int x = (int)Math.Round(desiredX), z = (int)Math.Round(desiredZ); bool swap = ((int)yaw / 90) % 2 != 0;
                var placement = new BuildingPlacement { Type = type, StartupEstateId = marker.EstateId, RotationDegrees = yaw, FootprintWidth = swap ? descriptor.Height : descriptor.Width, FootprintHeight = swap ? descriptor.Width : descriptor.Height };
                bool pole = type == "VillageFlagPole";
                if (!searches.TryGetValue((radius, pole), out var candidates))
                    searches.Add((radius, pole), candidates = Candidates(radius, pole));
                foreach (var candidate in candidates)
                {
                    placement.RawX = (x + candidate.X) * 1024; placement.RawZ = (z + candidate.Z) * 1024;
                    if (!world.CanPlace(placement, out float height)) continue; placement.RawY = height;
                    while (ids.Contains(next)) next--; placement.Id = next--; ids.Add(placement.Id); return placement;
                }
                return null;
            }
        }
        private static string KeepStem(KeepSize size)=>size switch{KeepSize.Wooden=>"keep_wooden",KeepSize.Small=>"keep_small",KeepSize.Large=>"keep_large",_=>"keep_medium"};
        internal static List<(int X, int Z)> Candidates(int radius, bool pole)
        {
            var result = new List<(int X, int Z)>((radius*2+1)*(radius*2+1)) { (0, 0) };
            for (int r = 1; r <= radius; r++)
            {
                for (int x = -r; x <= r; x++) result.Add((x, -r));
                for (int z = -r+1; z <= r; z++) result.Add((r, z));
                for (int x = r-1; x >= -r; x--) result.Add((x, r));
                for (int z = r-1; z > -r; z--) result.Add((-r, z));
            }
            // Pole ordering reproduces the measured native square-ring scan.
            // Other buildings prefer distance, then clockwise N,E,S,W. North is -Z.
            if (!pole) result.Sort((a, b) => {
                int distance = (a.X*a.X+a.Z*a.Z).CompareTo(b.X*b.X+b.Z*b.Z);
                return distance != 0 ? distance : Bearing(a.X,a.Z).CompareTo(Bearing(b.X,b.Z));
            });
            return result;
        }
        private static double Bearing(int x, int z)
        {
            double angle = Math.Atan2(x, -z);
            return angle < 0 ? angle + Math.PI*2 : angle;
        }
        internal sealed class PlacementWorld
        {
            private readonly S2MFile map; private readonly bool[] occupied; private readonly StartupBuildingSettings settings;
            private readonly bool[] troopExcluded;
            public PlacementWorld(S2MFile map, IEnumerable<BuildingPlacement> buildings, IEnumerable<Keep> keeps, StartupBuildingSettings settings)
            {
                this.map = map; this.settings = settings; occupied = new bool[map.MapSize * map.MapSize];
                foreach (var p in buildings) Reserve(p);
                foreach (var keep in keeps) Reserve(keep);
                foreach (var camp in S2MCampfireLayout.Create(map)) Mark((int)Math.Floor(camp.Center.x), (int)Math.Floor(camp.Center.y));
                // Rasterize once: candidate checks are O(1), even with thousands of troops.
                // The observed scan point is origin+(2,2), not the mast centre.
                troopExcluded = CreateTroopMask(settings.EstateTroopExclusionRadius,2);
            }
            private void Mark(int x, int z) { if (x >= 0 && z >= 0 && x < map.MapSize && z < map.MapSize) occupied[z * map.MapSize + x] = true; }
            public void Reserve(BuildingPlacement placement)
            {
                var area = PlacementArea(placement);
                for (int z = area.MinZ; z < area.MaxZ; z++) for (int x = area.MinX; x < area.MaxX; x++) Mark(x, z);
                if (placement is Keep) { var p = S2MNavigationBuilder.BuildingPoint(placement, S2MNavigationBuilder.UnrotatedWidth(placement) * .5f, -2); Mark((int)Math.Floor(p.X + .5f), (int)Math.Floor(p.Z + .5f)); }
            }
            // Placement-only aprons from native building rules. These are deliberately
            // separate from navigation footprints: peasants must walk/sit around poles.
            private (int MinX, int MinZ, int MaxX, int MaxZ) PlacementArea(BuildingPlacement placement)
            {
                var f = S2MBuildingFootprint.FromPlacement(placement);
                int marginX = 0, marginZ = 0;
                if (placement.Type == "VillageFlagPole") marginX = marginZ = 1;
                else if ((placement.Type == "Granary" || placement.Type == "Hovel")) marginX = marginZ = 1;
                else if (placement.Type == "StockPile")
                {
                    bool turned = Math.Abs((int)Math.Round(placement.RotationDegrees / 90)) % 2 != 0;
                    if (turned) marginX = 1; else marginZ = 1;
                }
                if(placement.Type=="SawPit")
                {
                    int turn=((int)Math.Round(placement.RotationDegrees/90)%4+4)%4;
                    return (f.MinX-(turn==0 || turn==1?1:0),f.MinZ-(turn==0 || turn==3?1:0),
                        f.MaxXExclusive+(turn==2 || turn==3?1:0),f.MaxZExclusive+(turn==1 || turn==2?1:0));
                }
                return (f.MinX-marginX, f.MinZ-marginZ, f.MaxXExclusive+marginX, f.MaxZExclusive+marginZ);
            }
            public bool CanPlace(BuildingPlacement placement, out float height)
            {
                height = 0; var f = S2MBuildingFootprint.FromPlacement(placement);
                int estate=S2MCampfireLayout.EstateAtBuilding(map,placement);
                var area = PlacementArea(placement);
                if (area.MinX < 1 || area.MinZ < 1 || area.MaxX >= map.MapSize || area.MaxZ >= map.MapSize) return false;
                if (placement.Type == "VillageFlagPole" && troopExcluded[(int)placement.MapZ*map.MapSize+(int)placement.MapX]) return false;
                for (int z = area.MinZ; z < area.MaxZ; z++) for (int x = area.MinX; x < area.MaxX; x++)
                    if (occupied[z * map.MapSize + x] || !Ground(x, z, estate)) return false;
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int z = f.MinZ; z < f.MaxZExclusive; z++) for (int x = f.MinX; x < f.MaxXExclusive; x++)
                {
                    float h = S2MCampfireLayout.GroundHeight(map, x + .5f, z + .5f);
                    if (float.IsNaN(h) || float.IsInfinity(h)) return false;
                    min = Math.Min(min, h); max = Math.Max(max, h);
                }
                if (max - min > settings.MaximumHeightVariation) return false;
                if (placement.Type == "Keep")
                {
                    var fire = S2MNavigationBuilder.BuildingPoint(placement, S2MNavigationBuilder.UnrotatedWidth(placement) * .5f, -2);
                    int fx = (int)Math.Floor(fire.X + .5f), fz = (int)Math.Floor(fire.Z + .5f);
                    if (fx < 1 || fz < 1 || fx >= map.MapSize - 1 || fz >= map.MapSize - 1 || occupied[fz * map.MapSize + fx] || !Ground(fx, fz, estate)) return false;
                }
                height = max; return true;
            }
            internal bool[] CreateTroopMask(float radius,float offset,float? offsetZ=null)
            {
                var mask=new bool[occupied.Length];
                if(radius==0)return mask;
                foreach(var actor in map.Actors)
                {
                    if(!actor.OnMap || !actor.IsSoldier)continue;
                    float x=actor.RawX/1024f-offset,z=actor.RawZ/1024f-(offsetZ??offset);
                    if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(z)||float.IsInfinity(z))continue;
                    for(int iz=Math.Max(0,(int)Math.Floor(z-radius));iz<=Math.Min(map.MapSize-1,(int)Math.Ceiling(z+radius));iz++)
                        for(int ix=Math.Max(0,(int)Math.Floor(x-radius));ix<=Math.Min(map.MapSize-1,(int)Math.Ceiling(x+radius));ix++)
                        {float dx=ix-x,dz=iz-z;if(dx*dx+dz*dz<radius*radius)mask[iz*map.MapSize+ix]=true;}
                }
                return mask;
            }
            private bool Ground(int x, int z, int estate)
            {
                var layer = map.RadarEstateLayer; if (layer == null || x >= layer.Width || z >= layer.Height || layer.EstateIds[z * layer.Width + x] != estate) return false;
                if (map.WaterLayer != null && map.WaterLayer.HasWaterLikeSurface(x, z)) return false;
                if (map.Landscape?.TerrainCutoutValues != null && map.Landscape.TerrainCutoutValues[z * map.MapSize + x] == 1) return false;
                if (map.WallLayer != null && map.WallLayer.GetCell(x, z).HasWall) return false;
                return map.WalkingLayer == null || map.WalkingLayer.IsGroundSurface(x, z);
            }
        }
    }
}
