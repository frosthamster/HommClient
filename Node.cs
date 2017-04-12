using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    public class Node
    {
        private readonly List<Node> incidentNodes = new List<Node>();
        public readonly int X;
        public readonly int Y;
        public MapObjectData Data { get; private set; }

        public Node(int x, int y, MapObjectData data)
        {
            X = x;
            Y = y;
            Data = data;
        }

        public bool IsPossibleToMove => Data.Wall == null;
        public bool IsBoundaryPointOfWarFog => IncidentNodes.FirstOrDefault(e => e.Data == null) != null;

        public void UpdateData(MapObjectData data)
        {
            if (data == null)
                throw new ArgumentException();
            Data = data;
        }

        public Direction GetDirection(Node destination)
        {
            if (!IncidentNodes.Contains(destination)) throw new ArgumentException();
            return Data.Location.ToLocation().GetDirectionTo(destination.Data.Location.ToLocation());
        }

        public IEnumerable<Node> IncidentNodes
        {
            get
            {
                foreach (var node in incidentNodes)
                    yield return node;
            }
        }

        public bool Equals(Node other)
        {
            return X == other.X && Y == other.Y;
        }

        public IEnumerable<Node> BreadthSearch(Func<Node, bool> incidentNodesSelector)
        {
            if (!incidentNodesSelector(this))
                yield break;
            var visited = new HashSet<Node>();
            var queue = new Queue<Node>();
            queue.Enqueue(this);
            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                if (visited.Contains(node)) continue;
                visited.Add(node);
                yield return node;
                foreach (var incidentNode in node.IncidentNodes.Where(incidentNodesSelector))
                    queue.Enqueue(incidentNode);
            }
        }

        public IEnumerable<Node> DepthSearch(Func<Node, bool> incidentNodesSelector)
        {
            var visited = new HashSet<Node>();
            var stack = new Stack<Node>();
            stack.Push(this);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                if (visited.Contains(node)) continue;
                visited.Add(node);
                yield return node;
                foreach (var incidentNode in node.IncidentNodes.Where(incidentNodesSelector))
                    stack.Push(incidentNode);
            }
        }

        public Dictionary<UnitType, int> GetArmy()
        {
            return Data.NeutralArmy != null
                ? Data.NeutralArmy.Army
                : Data.Garrison?.Army;
        }

        public override string ToString()
        {
            return $"{X}, {Y}";
        }

        public static void Connect(Node node1, Node node2)
        {
            if (node1.incidentNodes.Contains(node2) || node1 == node2)
                return;
            node1.incidentNodes.Add(node2);
            node2.incidentNodes.Add(node1);
        }
    }
}
