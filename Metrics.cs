using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    static class Metrics
    {
        public static double GetPathRationality(SearchResult path, HommSensorData sensorData, ResourceBunch bunch, Node heroLocation)
        {
            if (path.Destination.Equals(heroLocation))
                return 0;
            var profit = 0d;
            var treasury = new Dictionary<Resource, int>(sensorData.MyTreasury);
            foreach (var node in path.NodeChain)
            {
                var data = node.Data;
                if (data.Dwelling != null)
                    profit += GetBarrackMetric(node, treasury);
                if (data.Mine != null)
                    profit += GetMineMetric(sensorData.WorldCurrentTime, treasury, node, sensorData.MyRespawnSide);
                if (data.ResourcePile != null)
                    profit += GetResourceMetric(data.ResourcePile.Resource);
                if (node.IsBoundaryPointOfWarFog)
                    profit += GetBoundaryPointOfWarFogMetric();

                var enemyArmy = node.GetArmy();

                if (enemyArmy == null) continue;
                if (Combat.Resolve(new ArmiesPair(sensorData.MyArmy, enemyArmy)).IsAttackerWin)
                    profit += GetArmyMetric(enemyArmy);
                else
                {
                    profit = 0;
                    break;
                }
            }
            if (bunch != null)
                profit += GetResourceMetric(bunch.GetResources());

            return profit / GetPathCost(path.NodeChain);
        }

        private static double GetBoundaryPointOfWarFogMetric()
        {
            return 1;
        }

        private static double GetBarrackMetric(Node dwellingLocation, Dictionary<Resource, int> treasury)
        {
            if (dwellingLocation.Data.Dwelling == null)
                throw new ArgumentException();
            var barrack = dwellingLocation.Data.Dwelling;
            var availableToBuy = Math.Min(GetAvailableToBuy(barrack.UnitType, treasury), barrack.AvailableToBuyCount);
            var speciarResource = ResourceTypes[barrack.UnitType];
            treasury[speciarResource] -= availableToBuy * UnitsConstants.Current.UnitCost[barrack.UnitType][speciarResource];
            treasury[Resource.Gold] -= availableToBuy * UnitsConstants.Current.UnitCost[barrack.UnitType][Resource.Gold];
            var profit = PrioritiesConstants.PotentialMurdersInWar * availableToBuy
                         * UnitsConstants.Current.Scores[barrack.UnitType];
            profit *= barrack.UnitType == UnitType.Militia ? PrioritiesConstants.MilitiaUtility : 1;
            return profit;
        }

        private static double GetMineMetric(double currentTime, Dictionary<Resource, int> treasury, Node mineLocation, string heroSide)
        {
            if (mineLocation.Data.Mine == null)
                throw new ArgumentException();
            var mine = mineLocation.Data.Mine;
            var profit = PrioritiesConstants.EstimatedMiningTime * (90 - currentTime) * HommRules.Current.MineOwningDailyScores;
            profit *= mine.Resource == Resource.Gold ? PrioritiesConstants.GoldMinePriority : 1;
            profit *= GetAvailableToBuy(UnitTypes[mine.Resource], treasury) > 5 ? PrioritiesConstants.MineDepreciation : 1;
            profit *= mine.Owner == heroSide ? 0 : 1;
            return profit;
        }

        public static int GetAvailableToBuy(UnitType unit, Dictionary<Resource, int> treasury)
        {
            var prices = UnitsConstants.Current.UnitCost[unit];
            var costInGold = prices[Resource.Gold];
            var specialResource = ResourceTypes[unit];
            var costInSpecial = prices[specialResource];

            return Math.Min(treasury[Resource.Gold] / costInGold,
                treasury[specialResource] / costInSpecial);
        }

        private static double GetResourceMetric(Resource resource)
        {
            return HommRules.Current.ResourcesGainScores +
                   PrioritiesConstants.PotentialTroopsAcquisition * UnitsConstants.Current.Scores[UnitTypes[resource]];
        }

        private static double GetResourceMetric(Dictionary<Resource, int> resources)
        {
            return resources
                .Select(e => GetResourceMetric(e.Key) * e.Value)
                .Aggregate((u, v) => u + v);
        }

        public static readonly Dictionary<UnitType, Resource> ResourceTypes = new Dictionary<UnitType, Resource>
        {
            {UnitType.Cavalry, Resource.Ebony},
            {UnitType.Infantry, Resource.Iron},
            {UnitType.Ranged, Resource.Glass},
            {UnitType.Militia, Resource.Gold}
        };

        public static readonly Dictionary<Resource, UnitType> UnitTypes = new Dictionary<Resource, UnitType>
        {
            {Resource.Ebony, UnitType.Cavalry},
            {Resource.Iron, UnitType.Infantry},
            {Resource.Glass, UnitType.Ranged},
            {Resource.Gold, UnitType.Militia}
        };

        public static readonly Dictionary<Terrain, double> PathCost = new Dictionary<Terrain, double>
        {
            {Terrain.Road, 0.75},
            {Terrain.Grass, 1.0},
            {Terrain.Snow, 1.3},
            {Terrain.Desert, 1.15},
            {Terrain.Marsh, 1.3}
        };

        private static double GetPathCost(List<Node> path)
        {
            if (path.Count <= 1)
                return 1;
            return path
                .Take(path.Count - 1)
                .Select(e => PathCost[e.Data.Terrain])
                .Aggregate((u, v) => u + v);
        }

        private static int GetArmyMetric(Dictionary<UnitType, int> army)
        {
            var scores = UnitsConstants.Current.Scores;
            return army
                .Select(e => e.Value * scores[e.Key])
                .Aggregate((u, v) => u + v);
        }
    }
}
