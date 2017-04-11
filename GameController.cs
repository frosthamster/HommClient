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

        public void DoBestStep()
        {
            if (Data.IsDead)
            {
                Data = client.Wait(0.1);
                UpdateGraph();
                return;
            }

            var options = new List<Path>
            {
                GetBestPathToArmy(),
                GetBestPathToBarrack(),
                GetBestPathToMine()
            };
            var resourceBunchPath = GetBestPathToResurceBunch();
            var bestNotResourcePath = options.OrderByDescending(e => e).FirstOrDefault();

            var bestPath = resourceBunchPath?.Rationality > bestNotResourcePath?.Rationality ?
                resourceBunchPath.SearchResult : bestNotResourcePath?.SearchResult;
            Move(bestPath?.Track);
            if (resourceBunchPath != null && resourceBunchPath.ResourceBunch.Contains(Location))
                GatherResourceBunch(resourceBunchPath.ResourceBunch);
        }

        private void GatherResourceBunch(ResourceBunch bunch)
        {
            if (!bunch.Contains(Location))
                throw new ArgumentException();
            var path = Location.DepthSearch(e => e.Data!= null && bunch.Contains(e))
                .GetBigramms()
                .SelectMany(e =>
                {
                    if (e.Item1.IncidentNodes.Contains(e.Item2))
                        return new List<Direction> { e.Item1.GetDirection(e.Item2) };
                    return Graph.FindPathTo(Location, Data.MyArmy, e.Item2).Track;
                })
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
                    BuyArmy();
            }
        }

        private void BuyArmy()
        {
            if (Location.Data.Dwelling != null)
            {
                var unitsToHire = Math.Min(Metrics.GetAvailableToBuy(Location.Data.Dwelling.UnitType, Data.MyTreasury),
                    Location.Data.Dwelling.AvailableToBuyCount);
                if (unitsToHire > 0)
                {
                    Data = client.HireUnits(unitsToHire);
                    UpdateGraph();
                }
            }
        }

        private void UpdateGraph()
        {
            Graph.Update(Data.Map.Objects);
        }

        #region ChooseBestPath

        private Path GetBestPathToBarrack()
        {
            var results = Graph.FindPathsToDwellings(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToArmy()
        {
            var results = Graph.FindPathsToArmies(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToMine()
        {
            var results = Graph.FindPathsToMines(Location, Data.MyArmy);
            return GetMostProfitablePath(results.Select(CreatePath));
        }

        private Path GetBestPathToResurceBunch()
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
        static void Print(HommSensorData data)
        {
            Console.WriteLine("---------------------------------");

            Console.WriteLine($"You are here: ({data.Location.X},{data.Location.Y})");

            Console.WriteLine($"You have {data.MyTreasury.Select(z => z.Value + " " + z.Key).Aggregate((a, b) => a + ", " + b)}");

            var location = data.Location.ToLocation();

            Console.Write("W: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.Up)));

            Console.Write("E: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.RightUp)));

            Console.Write("D: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.RightDown)));

            Console.Write("S: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.Down)));

            Console.Write("A: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.LeftDown)));

            Console.Write("Q: ");
            Console.WriteLine(GetObjectAt(data.Map, location.NeighborAt(Direction.LeftUp)));
        }

        static string GetObjectAt(MapData map, Location location)
        {
            if (location.X < 0 || location.X >= map.Width || location.Y < 0 || location.Y >= map.Height)
                return "Outside";
            return map.Objects.
                Where(x => x.Location.X == location.X && x.Location.Y == location.Y)
                .FirstOrDefault()?.ToString() ?? "Nothing";
        }

        static void OnInfo(string infoMessage)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(infoMessage);
            Console.ResetColor();
        }
        #endregion
    }
}
