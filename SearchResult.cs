using System.Collections.Generic;
using System.Linq;
using HoMM;

namespace Homm.Client
{
    class SearchResult
    {
        public Node Destination { get; }
        public List<Direction> Track { get; }
        public List<Node> NodeChain { get; }

        public SearchResult(IEnumerable<Direction> track, List<Node> nodeChain, Node destination)
        {
            Destination = destination;
            Track = track.ToList();
            NodeChain = nodeChain;
        }
    }
}
