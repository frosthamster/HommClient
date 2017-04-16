using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class Graph
    {
        private readonly Node[,] nodes;
        public Graph(MapData map)
        {
            var width = map.Width;
            var height = map.Height;
            var matrix = GetMatrixMap(map);
            nodes = new Node[width, height];
            for (var i = 0; i < width; i++)
                for (var j = 0; j < height; j++)
                    nodes[i, j] = new Node(i, j, matrix[i, j]);
            AssociateNodes();
        }

        private static MapObjectData[,] GetMatrixMap(MapData map)
        {
            var result = new MapObjectData[map.Width, map.Height];
            foreach (var mapObject in map.Objects)
                result[mapObject.Location.X, mapObject.Location.Y] = mapObject;
            return result;
        }

        private void AssociateNodes()
        {
            foreach (var node in nodes)
            {
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        if (IsExistOnMap(node.X + dx, node.Y + dy) && IsNeighbours(dx, dy, node.X))
                            Node.Connect(node, nodes[node.X + dx, node.Y + dy]);
            }
        }

        private bool IsNeighbours(int dx, int dy, int y)
        {
            return y % 2 == 0 ? !((dx == 1 || dx == -1) && dy == 1) : !((dx == 1 || dx == -1) && dy == -1);
        }

        private bool IsExistOnMap(int x, int y)
        {
            return x >= 0 && y >= 0 && x < nodes.GetLength(0) && y < nodes.GetLength(1);
        }

        public Node this[int x, int y] => nodes[x, y];

        public IEnumerable<Node> Nodes => nodes.Cast<Node>();

        public class DijkstraData
        {
            public Node Previous { get; }
            public double Price { get; }

            public DijkstraData(double price, Node previous)
            {
                Previous = previous;
                Price = price;
            }
        }

        private IEnumerable<SearchResult> FindPathsToObjects(Node start, Dictionary<UnitType, int> startArmy,
            Func<Node, bool> isTarget)
        {
            var notVisited = Nodes.ToList();
            var track = new Dictionary<Node, DijkstraData> { [start] = new DijkstraData(0, null) };
            var army = new Dictionary<Node, Dictionary<UnitType, int>> { [start] = startArmy };

            while (true)
            {
                Node toOpen = null;
                var bestPrice = double.PositiveInfinity;
                foreach (var e in notVisited.Where(e => e.Data != null))
                {
                    if (!track.ContainsKey(e) || !(track[e].Price < bestPrice)) continue;
                    bestPrice = track[e].Price;
                    toOpen = e;
                }

                if (toOpen == null) yield break;
                if (isTarget(toOpen))
                    yield return ExtractPath(track, toOpen);

                foreach (var nextNode in toOpen.IncidentNodes.Where(e => e.Data != null))
                {
                    var sumPrice = track[toOpen].Price + Metrics.PathCost[toOpen.Data.Terrain];
                    if (!nextNode.IsPossibleToMove || nextNode.GetArmy() != null &&
                        Combat.Resolve(new ArmiesPair(army[toOpen], nextNode.GetArmy())).IsDefenderWin)
                        continue;

                    if (track.ContainsKey(nextNode) && !(track[nextNode].Price > sumPrice)) continue;
                    track[nextNode] = new DijkstraData(sumPrice, toOpen);
                    army[nextNode] = nextNode.GetArmy() == null
                        ? army[toOpen]
                        : Combat.Resolve(new ArmiesPair(army[toOpen], nextNode.GetArmy())).AttackingArmy;
                }
                notVisited.Remove(toOpen);
            }
        }

        private SearchResult ExtractPath(IReadOnlyDictionary<Node, DijkstraData> track, Node end)
        {
            var result = new List<Node>();
            var pathItem = end;
            while (pathItem != null)
            {
                result.Add(pathItem);
                pathItem = track[pathItem].Previous;
            }
            result.Reverse();
            return new SearchResult(GetPath(result), result, end);
        }

        private IEnumerable<Direction> GetPath(IEnumerable<Node> nodeChain)
        {
            return nodeChain.GetBigramms().Select(e => e.Item1.GetDirection(e.Item2));
        }

        public SearchResult FindPathTo(Node start, Dictionary<UnitType, int> heroArmy, Node destination)
        {
            return FindPathsToObjects(start, heroArmy, e => e.Equals(destination)).FirstOrDefault();
        }

        private IEnumerable<ResourceBunch> FindResourceBunches()
        {
            var markedNodes = new HashSet<Node>();
            var resources = Nodes
                .Where(e => e.Data?.ResourcePile != null)
                .ToList();

            while (true)
            {
                var nextNode = resources.FirstOrDefault(node => !markedNodes.Contains(node));
                if (nextNode == null) break;
                var breadthSearch = nextNode.BreadthSearch(e => e.Data?.ResourcePile != null).ToList();
                yield return new ResourceBunch(breadthSearch);
                foreach (var node in breadthSearch)
                    markedNodes.Add(node);
            }
        }

        public IEnumerable<SearchResult> FindPathsToBoundaryPointsOfWarFog(Node start, Dictionary<UnitType, int> heroArmy)
        {
            return FindPathsToObjects(start, heroArmy, e => e.IsBoundaryPointOfWarFog);
        }

        public IEnumerable<SearchResult> FindPathsToArmies(Node start, Dictionary<UnitType, int> heroArmy)
        {
            return FindPathsToObjects(start, heroArmy, e => e.GetArmy() != null);
        }

        public IEnumerable<SearchResult> FindPathsToMines(Node start, Dictionary<UnitType, int> heroArmy)
        {
            return FindPathsToObjects(start, heroArmy, e => e.Data.Mine != null);
        }

        public IEnumerable<Tuple<SearchResult, ResourceBunch>> FindPathsToResourceBunches(Node start, Dictionary<UnitType, int> heroArmy)
        {
            var bunches = FindResourceBunches().ToDictionary(e => e, e => false);
            return FindPathsToObjects(start, heroArmy, e =>
            {
                var foundBunch = bunches.Where(f => !f.Value).FirstOrDefault(f => f.Key.Contains(e)).Key;
                var resultIsFound = foundBunch != null;
                if (resultIsFound)
                    bunches[foundBunch] = true;
                return resultIsFound;
            }).Select(e => Tuple.Create(e, bunches.Keys.FirstOrDefault(f => f.Contains(e.Destination))));
        }

        public IEnumerable<SearchResult> FindPathsToDwellings(Node start, Dictionary<UnitType, int> heroArmy)
        {
            return FindPathsToObjects(start, heroArmy, e => e.Data.Dwelling != null);
        }

        public void Update(List<MapObjectData> objects)
        {
            foreach (var data in objects)
                nodes[data.Location.X, data.Location.Y].UpdateData(data);
        }
    }
}
