using System;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class Path : IComparable
    {
        public SearchResult SearchResult { get; }
        public double Rationality { get; }
        public ResourceBunch ResourceBunch { get; }

        public Path(SearchResult searchResult, HommSensorData sensorData, Node heroLocation, ResourceBunch bunch)
        {
            SearchResult = searchResult;
            Rationality = Metrics.GetPathRationality(searchResult, sensorData, bunch, heroLocation);
            ResourceBunch = bunch;
        }

        public Path(SearchResult searchResult, HommSensorData sensorData, Node heroLocation)
            : this(searchResult, sensorData, heroLocation, null)
        {
        }

        public int CompareTo(object obj)
        {
            var other = (Path) obj;
            return Rationality.CompareTo(other.Rationality);
        }
    }
}
