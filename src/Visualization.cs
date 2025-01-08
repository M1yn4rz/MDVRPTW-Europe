using System;
using Evolution;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;
using OsmSharp.Streams;
using NetTopologySuite.Geometries;
using OsmSharp;
using System.Collections.Generic;
using System.Linq;
using System;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Xml.Linq;
using System.Globalization;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.Diagnostics;
using OsmSharp.IO.Xml;



namespace Evolution
{
    class Visualization
    {
        public Visualization(string flag, string urlPath, string area, string fileName, List<long>? path = null, 
        List<string>? points = null, List<List<long>>? paths = null, List<List<List<long>>>? listPaths = null, 
        Dictionary<string, long>? wareHouses = null, List<string>? countriesList = null)
        {
            if (flag != "None")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Map(flag, urlPath, area, fileName, path, points, paths, listPaths, 
                wareHouses, countriesList));
            }
        }
    }

    public class Map : Form
    {
        Data data = new Data();
        Colour colour = new Colour();
        GraphImport graphImport = new GraphImport();
        private GMapControl gMapControl;


        public Map(string flag, string urlPath, string area, string fileName, List<long>? path = null, 
        List<string>? points = null, List<List<long>>? paths = null, List<List<List<long>>>? listPaths = null, 
        Dictionary<string, long>? wareHouses = null, List<string>? countriesList = null)
        {
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            GMaps.Instance.UseRouteCache = true;
            GMaps.Instance.UseGeocoderCache = true;

            gMapControl = new GMapControl();
            gMapControl.Dock = DockStyle.Fill;
            gMapControl.MapProvider = GMap.NET.MapProviders.BingMapProvider.Instance;
            gMapControl.MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionWithoutCenter;
            gMapControl.Position = new GMap.NET.PointLatLng(52.2297, 21.0122);
            gMapControl.MinZoom = 1;
            gMapControl.MaxZoom = 20;
            gMapControl.Zoom = 12;

            Controls.Add(gMapControl);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            colour.WriteLine("b","\nStart Import");

            switch (flag)
            {
                case "ImportMap":
                    ImportMap(urlPath, area, fileName);
                    break;

                case "ImportEuropeMap":
                    ImportEuropeMap();
                    break;

                case "ShowPath":
                    ShowPath(path, urlPath, area, fileName, Color.Green, 5);
                    break;

                case "ShopEuropePath":
                    ShopEuropePath(path);
                    break;

                case "ShowDriversPaths":
                    ShowDriversPaths(listPaths!, wareHouses!);
                    break;

                case "ShowWareHousesPaths":
                    ShowWareHousesPaths(listPaths!, wareHouses!, countriesList!);
                    break;

                case "DisplayNodesRoads":
                    DisplayNodesRoads(urlPath, area, fileName);
                    break;

                case "DisplayNodes":
                    DisplayNodes(urlPath, area, fileName, points!);
                    break;

                default:
                    colour.WriteLine("r",$"ERROR - UNVAILABLE FLAG [{flag}]\nAvailable flags:");
                    colour.WriteLine("r","[None]\n[ImportMap]\n[ImportEuropeMap]\n[ShowPath]\n[ShowEuropePath]\n[DisplayNodesRoads]\n[DisplayNodes]\n[ShowDriversPathsRegion]");
                    break;
            }
            
            sw.Stop();
            colour.WriteLine("b",$"Stop Import, time: {sw.Elapsed.TotalSeconds} s\n");
        } 


        private void AddRoad(PointLatLng start, PointLatLng end, Color colour, int size)
        {
            var route = new GMapRoute(new[] { start, end }, "MyRoad")
            {
                Stroke = new Pen(colour, size)
            };

            var routesOverlay = new GMapOverlay("routes");
            routesOverlay.Routes.Add(route);
            gMapControl.Overlays.Add(routesOverlay);
        }


        private void ImportMap(string urlPath, string area, string fileName)
        {
            using (var reader = XmlReader.Create("data/osm/xml/" + urlPath + area + "-" + fileName + ".osm"))
            {
                var nodeDict = new Dictionary<string, PointLatLng>(1250000);
                var ways = new List<(string id, string highwayType, List<string> wayNodes)>(200000);

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
                    {
                        var id = reader.GetAttribute("id");
                        var lat = reader.GetAttribute("lat");
                        var lon = reader.GetAttribute("lon");

                        if (id != null && lat != null && lon != null)
                        {
                            nodeDict[id] = new PointLatLng(double.Parse(lat, CultureInfo.InvariantCulture), double.Parse(lon, CultureInfo.InvariantCulture));
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
                    {
                        var id = reader.GetAttribute("id");
                        var highwayType = "";
                        var wayNodes = new List<string>();

                        while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Name == "way"))
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (reader.Name == "tag" && reader.GetAttribute("k") == "highway")
                                {
                                    highwayType = reader.GetAttribute("v");
                                }
                                else if (reader.Name == "nd")
                                {
                                    var refId = reader.GetAttribute("ref");
                                    if (refId != null)
                                    {
                                        wayNodes.Add(refId);
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(id))
                        {
                            ways.Add((id, highwayType, wayNodes));
                        }
                    }
                }

                foreach (var way in ways)
                {
                    if (way.id == null) continue;

                    var color = way.highwayType switch
                    {
                        "trunk" or "trunk_link" => Color.Red,
                        "primary" or "primary_link" => Color.Green,
                        "secondary" or "secondary_link" => Color.Orange,
                        "rest_area" or "services" => Color.Purple,
                        "tertiary" or "tertiary_link" => Color.Gray,
                        _ => Color.Blue
                    };

                    for (int i = 0; i < way.wayNodes.Count - 1; i+=20)
                    {
                        var g = i + 20;
                        if (g >= way.wayNodes.Count - 1) {g = way.wayNodes.Count - 1; }
                        var startNode = nodeDict[way.wayNodes[i]];
                        var endNode = nodeDict[way.wayNodes[g]];

                        AddRoad(startNode, endNode, color, 5);
                    }
                }

                DisplayNodesRoads(urlPath, area, fileName);
            }
        }


        public void ShowWareHousesPaths(List<List<List<long>>> paths, Dictionary<string, long> sortOffices, List<string> countriesList)
        {
            var i = 0;

            foreach (var sortOffice in sortOffices)
            {
                if (!countriesList.Contains(sortOffice.Key)) { continue; }

                ShowDriversPathsWareHouse(paths[i], $"countries/secondary-roads/", sortOffice.Key, "secondary-roads", sortOffices);
                i++;
            }
        }


        public void ShowDriversPaths(List<List<List<long>>> paths, Dictionary<string, long> wareHouses)
        {
            var i = 0;
            bool reset = true;
            string previousFileName = "";
            Graph graph = new Graph();

            foreach (var wareHouse in wareHouses)
            {
                var fileName = wareHouse.Key.Remove(wareHouse.Key.Length - 1);
                var names = fileName.Split('/');

                reset = fileName != previousFileName;

                if (data.regions.Keys.Contains(names[0]))
                {
                    ShowDriversPathsRegion(graph, paths[i], $"regions/{names[0]}/", names[1], "tertiary-roads", reset);
                }
                else
                {
                    ShowDriversPathsRegion(graph, paths[i], $"countries/tertiary-roads/", names[0], "tertiary-roads", reset);
                }

                i++;
                previousFileName = fileName;
            }
        }


        public void ShowDriversPathsWareHouse(List<List<long>> paths, string urlPath, string area, string fileName, Dictionary<string, long> sortOffices)
        {
            Console.WriteLine($"{urlPath} {area} {fileName}");

            Graph graph = new Graph();
            var namesCountries = area.Split('|');

            foreach (var nameCountry in namesCountries)
            {
                graph.ConnectedGraph(graphImport.GraphImportByFile(urlPath, nameCountry, fileName).AdjacencyList);
                graph.ConnectedNodes(graphImport.ImportNodes(urlPath, nameCountry, fileName));
            }

            List<long> actuallyPath = new List<long>();
            List<string> displayPoints = new List<string>();

            foreach (var path in paths)
            {
                for (var j = 0; j < path.Count - 1; j++)
                {
                    (actuallyPath, _) = graph.Dijkstra(path[j], path[j + 1]);
                    ShowDriverPath(graph, actuallyPath, Color.Red, 4);
                }
            }
            
            foreach (var path in paths)
            {
                for (var j = 0; j < path.Count - 1; j++)
                {
                    displayPoints.Add(path[j].ToString());
                }

                DisplayDriverNodes(graph, displayPoints, GMarkerGoogleType.orange_dot);
                displayPoints.Clear();
            }

            var sortOffice = graph.nodeDict[sortOffices[area]];
            DisplayNodeType(new PointLatLng(sortOffice.Item1, sortOffice.Item2), sortOffices[area].ToString(), GMarkerGoogleType.blue_dot);
        }


        public void ShowDriversPathsRegion(Graph graph, List<List<long>> paths, string urlPath, string area, string fileName, bool reset)
        {
            List<Color> colours = new List<Color>() {Color.Black, Color.Red, Color.Blue, Color.Purple, Color.Gray, Color.Orange};
            List<GMarkerGoogleType> nodesTypes = new List<GMarkerGoogleType>() {GMarkerGoogleType.gray_small, GMarkerGoogleType.red_small, GMarkerGoogleType.blue_small, GMarkerGoogleType.purple_small, GMarkerGoogleType.gray_small, GMarkerGoogleType.orange_small};
            Console.WriteLine($"{urlPath} {area} {fileName}");

            var namesRegions = area.Split('|');

            if (reset)
            {
                graph.AdjacencyList.Clear();
                graph.nodeDict.Clear();

                foreach (var nameRegion in namesRegions)
                {
                    graph.ConnectedGraph(graphImport.GraphImportByFile(urlPath, nameRegion, fileName).AdjacencyList);
                    graph.ConnectedNodes(graphImport.ImportNodes(urlPath, nameRegion, fileName));
                }
            }
            
            List<long> actuallyPath = new List<long>();
            List<string> displayPoints = new List<string>();

            var i = 0;
            var size = (paths.Count - 1) * 2 + 2;

            foreach (var path in paths)
            {
                if (colours.Count == i) {i = 0; }

                for (var j = 0; j < path.Count - 1; j++)
                {
                    (actuallyPath, _) = graph.Dijkstra(path[j], path[j + 1]);
                    ShowDriverPath(graph, actuallyPath, colours[i], size);
                }

                size -= 2;
                i++;
            }

            i = 0;
            
            foreach (var path in paths)
            {
                if (colours.Count == i) {i = 0; }
                
                for (var j = 0; j < path.Count - 2; j++)
                {
                    displayPoints.Add(path[j + 1].ToString());
                }

                DisplayDriverNodes(graph, displayPoints, nodesTypes[i]);
                displayPoints.Clear();
                i++;
            }

            var wareHouse = graph.nodeDict[paths[0][0]];
            DisplayNodeType(new PointLatLng(wareHouse.Item1, wareHouse.Item2), paths[0][0].ToString(), GMarkerGoogleType.orange_dot);
        }


        public void ShowPath(List<long>? path, string urlPath, string area, string fileName, Color oneColour, int size)
        {
            Console.WriteLine("\tReading file...");

            var xml = XDocument.Load("data/osm/xml/" + urlPath + area + "-" + fileName + ".osm");

            Console.WriteLine("\tCreate variables...");

            var nodeDict = xml.Descendants("node")
                    .ToDictionary(n => n.Attribute("id")?.Value, n => new PointLatLng(
                    double.Parse(n.Attribute("lat")?.Value, CultureInfo.InvariantCulture),
                    double.Parse(n.Attribute("lon")?.Value, CultureInfo.InvariantCulture))
                );

            for (int i = 0; i < path!.Count - 1; ++i)
            {
                var startNode = nodeDict[path[i].ToString()];
                var endNode = nodeDict[path[i + 1].ToString()];

                AddRoad(startNode, endNode, oneColour, size);
            }
        }


        public void ShowDriverPath(Graph graph, List<long>? path, Color oneColour, int size)
        {
            for (int i = 0; i < path!.Count - 1; ++i)
            {
                var startNode = graph.nodeDict[path[i]];
                var endNode = graph.nodeDict[path[i + 1]];

                AddRoad(new PointLatLng(startNode.Item1, startNode.Item2), new PointLatLng(endNode.Item1, endNode.Item2), oneColour, size);
            }
        }


        private void ShopEuropePath(List<long>? path)
        {
            int k = data.countries.Count + 3;
            int m = 0;

            foreach (var country in data.countries)
            {
                var parties = country.Split('|');

                foreach (var part in parties)
                {
                    Console.WriteLine(country + " + " + parties + " + " + part);
                    m++;
                    colour.WriteLine("b",$"------[{m}/{k}]------");
                    Console.WriteLine("\tReading file...");

                    var xml = XDocument.Load("data/osm/xml/countries/primary-roads/" + part + "-primary-roads.osm");

                    Console.WriteLine("\tCreate variables...");

                    var nodeDict = xml.Descendants("node")
                            .ToDictionary(n => n.Attribute("id")?.Value, n => new PointLatLng(
                            double.Parse(n.Attribute("lat")?.Value, CultureInfo.InvariantCulture),
                            double.Parse(n.Attribute("lon")?.Value, CultureInfo.InvariantCulture))
                        );

                    var cultureInfo = CultureInfo.InvariantCulture;

                    for (int i = 0; i < path!.Count - 1; ++i)
                    {
                        if (nodeDict.Keys.Contains(path[i].ToString()) && nodeDict.Keys.Contains(path[i + 1].ToString()))
                        {
                            var startNode = nodeDict[path[i].ToString()];
                            var endNode = nodeDict[path[i + 1].ToString()];

                            AddRoad(startNode, endNode, Color.Blue, 5);
                        }
                    }
                }
            }
        }


        private void ImportEuropeMap()
        {
            int k = data.countries.Count + 4;
            int m = 0;

            foreach (var country in data.countries)
            {
                string[] parties = new string[0];
                string urlPath = "countries/primary-roads/";

                if (country == "russia")
                {
                    parties = parties.Concat(new string[] { "central-fed-district", "northwestern-fed-district" }).ToArray();
                    urlPath = "regions/russia/";
                }
                else
                {
                    parties = country.Split('|');
                }

                foreach (var part in parties)
                {
                    m++;
                    colour.WriteLine("b",$"------[{m}/{k}]------");

                    using (var reader = XmlReader.Create("data/osm/xml/" + urlPath + part + "-primary-roads.osm"))
                    {
                        var nodeDict = new Dictionary<string, PointLatLng>(1250000);
                        var ways = new List<(string id, string highwayType, List<string> wayNodes)>(200000);

                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
                            {
                                var id = reader.GetAttribute("id");
                                var lat = reader.GetAttribute("lat");
                                var lon = reader.GetAttribute("lon");

                                if (id != null && lat != null && lon != null)
                                {
                                    nodeDict[id] = new PointLatLng(double.Parse(lat, CultureInfo.InvariantCulture), double.Parse(lon, CultureInfo.InvariantCulture));
                                }
                            }
                            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
                            {
                                var id = reader.GetAttribute("id");
                                var highwayType = "";
                                var wayNodes = new List<string>();

                                while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Name == "way"))
                                {
                                    if (reader.NodeType == XmlNodeType.Element)
                                    {
                                        if (reader.Name == "tag" && reader.GetAttribute("k") == "highway")
                                        {
                                            highwayType = reader.GetAttribute("v");
                                        }
                                        else if (reader.Name == "nd")
                                        {
                                            var refId = reader.GetAttribute("ref");
                                            if (refId != null)
                                            {
                                                wayNodes.Add(refId);
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(id))
                                {
                                    ways.Add((id, highwayType, wayNodes));
                                }
                            }
                        }

                        foreach (var way in ways)
                        {
                            var color = way.highwayType switch
                            {
                                "trunk" or "trunk_link" => Color.Red,
                                "primary" or "primary_link" => Color.Green,
                                _ => Color.Blue
                            };

                            for (int i = 0; i < way.wayNodes.Count - 1; i += 20)
                            {
                                var g = Math.Min(i + 20, way.wayNodes.Count - 1);

                                if (nodeDict.TryGetValue(way.wayNodes[i], out var startNode) &&
                                    nodeDict.TryGetValue(way.wayNodes[g], out var endNode))
                                {
                                    AddRoad(startNode, endNode, color, 5);
                                }
                            }
                        }
                    }
                }
            }
        }


        private void DisplayNode(PointLatLng point, string nodeId)
        {
            var marker = new GMarkerGoogle(point, GMarkerGoogleType.red_small);
            marker.ToolTipText = nodeId;

            var markersOverlay = new GMapOverlay("markers");
            markersOverlay.Markers.Add(marker);
            gMapControl.Overlays.Add(markersOverlay);
        }


        private void DisplayNodeType(PointLatLng point, string nodeId, GMarkerGoogleType nodeType)
        {
            var marker = new GMarkerGoogle(point, nodeType);
            marker.ToolTipText = nodeId;

            var markersOverlay = new GMapOverlay("markers");
            markersOverlay.Markers.Add(marker);
            gMapControl.Overlays.Add(markersOverlay);
        }


        private void DisplayNodesRoads(string urlPath, string area, string fileName)
        {
            using (var reader = XmlReader.Create("data/osm/xml/" + urlPath + area + "-" + fileName + ".osm"))
            {
                var nodeDict = new Dictionary<string, PointLatLng>(1250000);

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
                    {
                        var id = reader.GetAttribute("id");
                        var lat = reader.GetAttribute("lat");
                        var lon = reader.GetAttribute("lon");

                        if (id != null && lat != null && lon != null)
                        {
                            nodeDict[id] = new PointLatLng(double.Parse(lat, CultureInfo.InvariantCulture), double.Parse(lon, CultureInfo.InvariantCulture));
                        }
                    }
                }
                
                byte k = 100;

                foreach (var node in nodeDict)
                {
                    if (k > 0) {k--; continue; }
                    else {k = 100; }

                    DisplayNode(node.Value, node.Key!);
                }
            }
        }


        public void DisplayNodes(string urlPath, string area, string fileName, List<string> points)
        {
            var xml = XDocument.Load("data/osm/xml/" + urlPath + area + "-" + fileName + ".osm");

            var nodeDict = xml.Descendants("node")
                    .ToDictionary(n => n.Attribute("id")?.Value, n => new PointLatLng(
                    double.Parse(n.Attribute("lat")?.Value, CultureInfo.InvariantCulture),
                    double.Parse(n.Attribute("lon")?.Value, CultureInfo.InvariantCulture))
                );

            foreach (var point in points)
            {
                Console.WriteLine("Point: " + point);
                DisplayNode(nodeDict[point], point);
            }
        }


        public void DisplayDriverNodes(Graph graph, List<string> points, GMarkerGoogleType nodeType)
        {
            foreach (var point in points)
            {
                var node = graph.nodeDict[long.Parse(point)];
                DisplayNodeType(new PointLatLng(node.Item1, node.Item2), point, nodeType);
            }
        }
    }
}