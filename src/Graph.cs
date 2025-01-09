using System;
using Evolution;
using System.IO;
using OsmSharp;
using OsmSharp.Streams;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Globalization;
using GMap.NET;
using GMap.NET.WindowsForms;



namespace Evolution
{
    public class Graph
    {
        public Dictionary<long,List<(long neighborNodeId,double weight,long wayId)>> AdjacencyList { get; } = new Dictionary<long,List<(long,double,long)>>();
        public Dictionary<long,List<(long neighborNodeId,double weight,long wayId)>> OptimalizationList { get; } = new Dictionary<long,List<(long,double,long)>>();
        public Dictionary<long,Tuple<double,double>> nodeDict = new Dictionary<long,Tuple<double,double>>();


        public void AddEdge(long nodeId1, long nodeId2, double weight, long wayId)
        {
            if (!AdjacencyList.ContainsKey(nodeId1))
            {
                AdjacencyList[nodeId1] = new List<(long,double,long)>();
            }
            if (!AdjacencyList.ContainsKey(nodeId2))
            {
                AdjacencyList[nodeId2] = new List<(long,double,long)>();
            }
            AdjacencyList[nodeId1].Add((nodeId2,weight,wayId));
            AdjacencyList[nodeId2].Add((nodeId1,weight,wayId));
        }


        public void ConnectedGraph(Dictionary<long,List<(long neighborNodeId,double weight,long wayId)>> anotherGraph)
        {
            foreach (var kvp in anotherGraph)
            {
                if (AdjacencyList.ContainsKey(kvp.Key))
                {
                    AdjacencyList[kvp.Key].AddRange(kvp.Value);
                }
                else
                {
                    AdjacencyList[kvp.Key] = new List<(long neighborNodeId,double weight,long wayId)> (kvp.Value);
                }
            }
        }


        public void ConnectedNodes(Dictionary<long,Tuple<double,double>> anotherGraph)
        {
            foreach (var kvp in anotherGraph)
            {
                nodeDict[kvp.Key] = kvp.Value;
            }
        }


        public void OptimalizationGraph(List<long> Ignored)
        {
            var i = 0;

            foreach (var elem in AdjacencyList)
            {
                OptimalizationList[elem.Key] = new List<(long,double,long)>(elem.Value);
                if (elem.Value.Count == 2) {++i; }
            }

            var j = 0;
            long lookId = 0;

            foreach (var elem in AdjacencyList)
            {
                if (OptimalizationList[elem.Key].Count == 2 && !Ignored.Contains(elem.Key)) // && OptimalizationList[elem.Key][0].wayId == OptimalizationList[elem.Key][1].wayId)
                {
                    var id1 = OptimalizationList[elem.Key][0].neighborNodeId;
                    var id2 = OptimalizationList[elem.Key][1].neighborNodeId;
                    var weight1 = OptimalizationList[elem.Key][0].weight;
                    var weight2 = OptimalizationList[elem.Key][1].weight;
                    var wayId1 = OptimalizationList[elem.Key][0].wayId;
                    var wayId2 = OptimalizationList[elem.Key][1].wayId;

                    if (id1 == id2 || elem.Key == id1 || elem.Key == id2) {lookId = elem.Key; ++j; continue; }

                    OptimalizationList.Remove(elem.Key);
                    OptimalizationList[id1].Remove((elem.Key, weight1, wayId1));
                    OptimalizationList[id2].Remove((elem.Key, weight2, wayId2));
                    OptimalizationList[id1].Add((id2, weight1 + weight2, 0));
                    OptimalizationList[id2].Add((id1, weight1 + weight2, 0));
                }
            }
        }


        public void DisplayGraph()
        {
            Console.WriteLine($"Size of graph: {AdjacencyList.Count}");
            byte i = 0;

            foreach (var node in AdjacencyList)
            {
                ++i;
                if (i > 10) {break; }
                Console.Write($"Node {node.Key}: ");
                foreach (var (neighbor, weight, wayId) in node.Value)
                {
                    Console.Write($"({neighbor}, {weight}, {wayId}) ");
                }
                Console.WriteLine();
            }
        }


        public void DisplayOptGraph()
        {
            Console.WriteLine($"Size of graph: {OptimalizationList.Count}");
            byte i = 0;

            foreach (var node in OptimalizationList)
            {
                ++i;
                if (i > 10) {break; }
                Console.Write($"Node {node.Key}: ");
                foreach (var (neighbor, weight, wayId) in node.Value)
                {
                    Console.Write($"({neighbor}, {weight}, {wayId}) ");
                }
                Console.WriteLine();
            }
        }


        public long DistanceNodes(long startNode, long stopNode)
        {
            double distance = 0;
            var pointOne = nodeDict[startNode];
            var pointTwo = nodeDict[stopNode];

            distance = Math.Sqrt((pointOne.Item1-pointTwo.Item1)*(pointOne.Item1-pointTwo.Item1)+(pointOne.Item2-pointTwo.Item2)*(pointOne.Item2-pointTwo.Item2));

            return (long) (111.32 * distance);
        }


        public Tuple<List<long>, double> Dijkstra(long startNode, long endNode)
        {
            var distances = new Dictionary<long, double>();
            var previousNodes = new Dictionary<long, long?>();
            var priorityQueue = new SortedSet<(double distance, long node)>();

            foreach (var node in AdjacencyList.Keys)
            {
                distances[node] = double.PositiveInfinity;
                previousNodes[node] = null;
            }

            distances[startNode] = 0;
            priorityQueue.Add((0, startNode));

            while (priorityQueue.Count > 0)
            {
                var (currentDistance, currentNode) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);

                if (currentNode == endNode)
                    break;

                foreach (var (neighbor,weight,_) in AdjacencyList[currentNode])
                {
                    double newDistance = currentDistance + weight;

                    if (newDistance < distances[neighbor])
                    {
                        priorityQueue.Remove((distances[neighbor], neighbor));
                        distances[neighbor] = newDistance;
                        previousNodes[neighbor] = currentNode;
                        priorityQueue.Add((newDistance, neighbor));
                    }
                }
            }

            return new(ConstructPath(previousNodes, startNode, endNode), distances[endNode]);
        }


        public Tuple<List<long>, double> OptimalizationDijkstra(long startNode, long endNode)
        {
            var distances = new Dictionary<long, double>();
            var previousNodes = new Dictionary<long, long?>();
            var priorityQueue = new SortedSet<(double distance, long node)>();

            foreach (var node in OptimalizationList.Keys)
            {
                distances[node] = double.PositiveInfinity;
                previousNodes[node] = null;
            }

            distances[startNode] = 0;
            priorityQueue.Add((0, startNode));

            while (priorityQueue.Count > 0)
            {
                var (currentDistance, currentNode) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);

                if (currentNode == endNode)
                    break;

                foreach (var (neighbor,weight,_) in OptimalizationList[currentNode])
                {
                    double newDistance = currentDistance + weight;

                    if (newDistance < distances[neighbor])
                    {
                        priorityQueue.Remove((distances[neighbor], neighbor));
                        distances[neighbor] = newDistance;
                        previousNodes[neighbor] = currentNode;
                        priorityQueue.Add((newDistance, neighbor));
                    }
                }
            }

            return new(ConstructPath(previousNodes, startNode, endNode), distances[endNode]);
        }


        public bool IsPathExists(long startNode, long endNode)
        {
            var distances = new Dictionary<long, double>();
            var priorityQueue = new SortedSet<(double distance, long node)>();

            foreach (var node in OptimalizationList.Keys)
            {
                distances[node] = double.PositiveInfinity;
            }

            distances[startNode] = 0;
            priorityQueue.Add((0, startNode));

            while (priorityQueue.Count > 0)
            {
                var (currentDistance, currentNode) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);

                if (currentNode == endNode)
                {
                    return true;
                }

                foreach (var (neighbor, weight, _) in OptimalizationList[currentNode])
                {
                    double newDistance = currentDistance + weight;

                    if (newDistance < distances[neighbor])
                    {
                        priorityQueue.Remove((distances[neighbor], neighbor));
                        distances[neighbor] = newDistance;
                        priorityQueue.Add((newDistance, neighbor));
                    }
                }
            }

            return false;
        }


        private List<long> ConstructPath(Dictionary<long, long?> previousNodes, long startNode, long endNode)
        {
            var path = new List<long>();

            for (var at = endNode; at != -1; at = previousNodes[at] ?? -1)
            {
                path.Add(at);
            }
            path.Reverse();

            return path.First() == startNode ? path : new List<long>();
        }
    }

    
    class GraphImport
    {
        Data data = new Data();
        Colour colour = new Colour();


        public Dictionary<long, Tuple<double, double>> ImportNodes(string urlPath, string area, string fileName)
        {
            string filePath = $"data/osm/xml/{urlPath}{area}-{fileName}.osm";
            var nodeDict = new Dictionary<long,Tuple<double,double>>();

            using (var fileStream = File.OpenRead(filePath))
            {
                var source = new XmlOsmStreamSource(fileStream);

                foreach (var osmGeo in source)
                {
                    if (osmGeo is Node node)
                    {
                        if (node.Id.HasValue && node.Latitude.HasValue && node.Longitude.HasValue)
                        {
                            nodeDict.Add(node.Id.Value, new(node.Latitude!.Value, node.Longitude!.Value));
                        }
                    }
                }
            }

            return nodeDict;
        }


        public Graph GraphImportByFile(string urlPath, string area, string fileName)
        {
            Graph graph = new Graph();

            string filePath = $"data/osm/xml/{urlPath}{area}-{fileName}.osm";

            using (var fileStream = File.OpenRead(filePath))
            {
                var source = new XmlOsmStreamSource(fileStream);

                var ways = new List<OsmSharp.Way>();
                var nodeDict = new Dictionary<long,Tuple<double,double>>();

                foreach (var osmGeo in source)
                {
                    if (osmGeo.Type == OsmSharp.OsmGeoType.Way)
                    {
                        var way = osmGeo as OsmSharp.Way;
                        if (way != null)
                        {
                            ways.Add(way);
                        }
                    }
                    else if (osmGeo is Node node)
                    {
                        if (node.Id.HasValue && node.Latitude.HasValue && node.Longitude.HasValue)
                        {
                            nodeDict.Add(node.Id.Value, new(node.Latitude!.Value, node.Longitude!.Value));
                        }
                    }
                }

                foreach (var way in ways)
                {
                    if (way.Id.HasValue)
                    {
                        var wayNodes = way.Nodes;
                        long wayId = way.Id.Value;

                        for (int i = 0; i < wayNodes.Length - 1; i++)
                        {
                            long nodeId1 = wayNodes[i];
                            long nodeId2 = wayNodes[i + 1];
                            Tuple<double,double> point1 = nodeDict[nodeId1];
                            Tuple<double,double> point2 = nodeDict[nodeId2];
                            double weight = Math.Sqrt(Math.Pow(point1.Item1 - point2.Item1, 2) + Math.Pow(point1.Item2 - point2.Item2, 2));

                            if (nodeDict.Keys.Contains(nodeId1) && nodeDict.Keys.Contains(nodeId2))
                            {
                                graph.AddEdge(nodeId1, nodeId2, weight, wayId);
                            }
                        }
                    }
                }
            }

            return graph;
        }


        public Graph ImportEurope()
        {
            Graph graph = new Graph();
            var k = 0;
            var m = data.countries.Count();

            foreach (var country in data.countries)
            {
                ++k;
                colour.WriteLine("b",$"[{k}/{m}]");

                string filePath = $"data/osm/xml/countries/primary-roads/{country}-primary-roads.osm";

                using (var fileStream = File.OpenRead(filePath))
                {
                    var source = new XmlOsmStreamSource(fileStream);
                    var ways = new List<OsmSharp.Way>();
                    var nodeDict = new Dictionary<long,Tuple<double,double>>();

                    foreach (var osmGeo in source)
                    {
                        if (osmGeo.Type == OsmSharp.OsmGeoType.Way)
                        {
                            var way = osmGeo as OsmSharp.Way;
                            if (way != null)
                            {
                                ways.Add(way);
                            }
                        }
                        else if (osmGeo is Node node)
                        {
                            if (node.Id.HasValue && node.Latitude.HasValue && node.Longitude.HasValue)
                            {
                                nodeDict.Add(node.Id.Value, new(node.Latitude!.Value, node.Longitude!.Value));
                            }
                        }
                    }

                    foreach (var way in ways)
                    {
                        if (way.Id.HasValue)
                        {
                            var wayNodes = way.Nodes;
                            long wayId = way.Id.Value;

                            for (int i = 0; i < wayNodes.Length - 1; i++)
                            {
                                long nodeId1 = wayNodes[i];
                                long nodeId2 = wayNodes[i + 1];
                                Tuple<double,double> point1 = nodeDict[nodeId1];
                                Tuple<double,double> point2 = nodeDict[nodeId2];
                                double weight = Math.Sqrt(Math.Pow(point1.Item1 - point2.Item1, 2) + Math.Pow(point1.Item2 - point2.Item2, 2));

                                if (nodeDict.Keys.Contains(nodeId1) && nodeDict.Keys.Contains(nodeId2))
                                {
                                    graph.AddEdge(nodeId1, nodeId2, weight, wayId);
                                }
                            }
                        }
                    }
                }
            }

            return graph;
        }
    }
}