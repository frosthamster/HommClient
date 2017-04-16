using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class GameController
    {
        private readonly HommClient client;
        private HommSensorData Data { get; set; }
        private readonly Guid cvarcTag = Guid.Parse("a55a4c8c-0961-45bd-978d-935c69143084");
        private Graph Graph { get; }
        private Node Location => Graph[Data.Location.X, Data.Location.Y];

        public GameController(string ip, int port)
        {
            client = new HommClient();
            client.OnSensorDataReceived += Print;
            client.OnInfo += OnInfo;
            Data = client.Configurate(
                ip, port, cvarcTag,
                timeLimit: 90,
                operationalTimeLimit: 1000,
                seed: new Random().Next(),
                spectacularView: true,
                debugMap: false,
                level: HommLevel.Level3,
                isOnLeftSide: true
            );
            Graph = new Graph(Data.Map);
        }

        public void MakeBestStep()
        {
            if (Data.IsDead)
            {
                Wait(0.1);
                return;
            }

            var resourceBunchPath = GetBestPathToResourceBunch();

            var possibleChoices = new List<Path>
            {
                GetBestPathToEnemyArmy(),
                GetBestPathToDwelling(),
                GetBestPathToMine(),
                GetBestPathToBoundaryPointOfWarFog(),
                resourceBunchPath
            };
            var bestPath = possibleChoices.OrderByDescending(e => e).FirstOrDefault();

            if (bestPath?.Rationality > 0)
                Move(bestPath.SearchResult.Track);
            else
                Wait(0.1);

            if (resourceBunchPath != null && resourceBunchPath.ResourceBunch.Contains(Location))
                GatherResourceBunch(resourceBunchPath.ResourceBunch);
        }

        private void Wait(double duration)
        {
            Data = client.Wait(duration);
            UpdateGraph();
        }

        private void GatherResourceBunch(ResourceBunch bunch)
        {
            if (!bunch.Contains(Location))
                throw new ArgumentException();
            var path = Location.DepthSearch(e => e.Data != null && bunch.Contains(e))
                .GetBigramms()
                .SelectMany(e =>
                    e.Item1.IncidentNodes.Contains(e.Item2) ?
                    new List<Direction> { e.Item1.GetDirection(e.Item2) } :
                    Graph.FindPathTo(Location, Data.MyArmy, e.Item2).Track)
                .ToList();
            Move(path);
        }

        private void Move(List<Direction> path)
        {
            if (path == null)
                return;
            foreach (var direction in path)
            {
                Data = client.Move(direction);
                UpdateGraph();
                var enemyLocation = Location.IncidentNodes.FirstOrDefault(e => e.Data.Hero != null);
                var enemy = enemyLocation?.Data.Hero;
                if (enemy != null && Combat.Resolve(new ArmiesPair(Data.MyArmy, enemy.Army)).IsAttackerWin)
                {
                    Data = client.Move(Location.GetDirection(enemyLocation));
                    UpdateGraph();
                    return;
                }
                if (Location.Data.Dwelling != null && Location.Data.Dwelling.UnitType != UnitType.Militia)
                    HireArmy();
            }
        }

        private void HireArmy()
        {
            if (Location.Data.Dwelling == null) return;

            var unitsToHire = Math.Min(Metrics.GetAvailableToBuy(Location.Data.Dwelling.UnitType, Data.MyTreasury),
                Location.Data.Dwelling.AvailableToBuyCount);
            if (unitsToHire <= 0) return;

            Data = client.HireUnits(unitsToHire);
            UpdateGraph();
        }

        private void UpdateGraph()
        {
            Graph.Update(Data.Map.Objects);
        }

        #region ChooseBestPath

        private Path GetBestPathToBoundaryPointOfWarFog()
        {
            var results = Graph.FindPathsToBoundaryPointsOfWarFog(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToDwelling()
        {
            var results = Graph.FindPathsToDwellings(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToEnemyArmy()
        {
            var results = Graph.FindPathsToArmies(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToMine()
        {
            var results = Graph.FindPathsToMines(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToResourceBunch()
        {
            var results = Graph.FindPathsToResourceBunches(Location, Data.MyArmy).ToList();
            return GetMostProfitablePath(results.Select(e => CreatePath(e.Item1, e.Item2)));
        }

        private Path CreatePath(SearchResult searchResult, ResourceBunch resourceBunch)
        {
            return new Path(searchResult, Data, Location, resourceBunch);
        }

        private Path CreatePath(SearchResult searchResult)
        {
            return new Path(searchResult, Data, Location, null);
        }

        private Path GetMostProfitablePath(IEnumerable<Path> paths)
        {
            return paths
                .OrderByDescending(e => e)
                .FirstOrDefault();
        }

        #endregion

        #region Logging

        private static void Print(HommSensorData data)
        {
            Console.WriteLine("---------------------------------");
            Console.WriteLine($"Current time: {(int)data.WorldCurrentTime}");
            var heroHealth = data.IsDead ? "dead" : "live";
            Console.WriteLine($"You are {heroHealth}");
            Console.WriteLine($"You are here: ({data.Location.X},{data.Location.Y})");
            Console.WriteLine($"You have {data.MyTreasury.Select(z => z.Value + " " + z.Key).Aggregate((a, b) => a + ", " + b)}");
        }

        private static void OnInfo(string infoMessage)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(infoMessage);
            Console.ResetColor();
        }
        #endregion
    }
}
