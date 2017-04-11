using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class ResourceBunch

    {
        private HashSet<Node> Resources { get; }

        public bool Contains(Node node)
        {
            return Resources.Contains(node);
        }

        public ResourceBunch(IEnumerable<Node> resources)
        {
            resources = resources.ToList();
            if (resources.FirstOrDefault(e => e.Data.ResourcePile == null) != null)
                throw new ArgumentException("All nodes should contain resources");
            Resources = new HashSet<Node>(resources);
        }

        public Dictionary<Resource, int> GetResources()
        {
            return Resources
                .GroupBy(e => e.Data.ResourcePile.Resource)
                .ToDictionary(e => e.Key, e => e.Select(f => f.Data.ResourcePile.Amount).Aggregate((u, v) => u + v));
        }
    }
}
