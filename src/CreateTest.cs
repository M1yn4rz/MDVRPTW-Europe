using System;
using System.Xml;
using Evolution;
using System.Collections.Generic;
using System.Linq;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using System.Xml.Linq;
using System.Globalization;
using CsvHelper;
using System.IO.Ports;
using System.Diagnostics;



namespace Evolution 
{
    class CreateTest
    {
        Data data = new Data();
        Random random = new Random();
        Colour colour = new Colour();
        GraphImport graphImport = new GraphImport();
        Dictionary<string, long> wareHouses = new Dictionary<string, long>();
        Dictionary<string, List<long>> wareHousesBase = new Dictionary<string, List<long>>();


        public void CreateNewTest(string name, int number_of_packages, List<string> countriesList)
        {
            var records = new List<string[]> 
            {
                new string[] {"ID","From","FromID","To","ToID","Weight"}
            };

            for (int i = 0; i < number_of_packages; i++) {records.Add(new string[] {(i+1).ToString(),"","","","",""}); }

            List<int> idFrom = Enumerable.Range(1, number_of_packages).ToList();
            List<int> idTo = Enumerable.Range(1, number_of_packages).ToList();

            Dictionary<string, List<int>> seeds = new Dictionary<string, List<int>>();
            var progs = RegionsSeeds();

            foreach (var country in data.countries)
            {
                if (countriesList.Contains(country))
                {
                    if (data.regions.Keys.Contains(country))
                    {
                        foreach (var region in data.regions[country])
                        {
                            seeds[country + "/" + region] = new List<int> {0, 0};
                        }
                    }
                    else
                    {
                        seeds[country] = new List<int> {0, 0};
                    }
                }
            }

            var keys = seeds.Keys.ToList();

            for (int k = 0; k < 2; k++)
            {
                for (int i = 0; i < number_of_packages; i++)
                {
                    double randomValue = random.NextDouble();
                    string randomKey = "";

                    foreach (var region in progs)
                    {
                        randomKey = region.Key;
                        if (randomValue <= region.Value) {break; }
                    }

                    seeds[randomKey][k] = seeds[randomKey][k] + 1;
                }
            }

            var m = seeds.Keys.Count;
            var n = 0;
            
            foreach (var region in seeds)
            {
                ++n;
                colour.WriteLine("b",$"----------[{n}/{m}]----------");

                var nodeDict = new Dictionary<string, PointLatLng>(1250000);
                var names = region.Key.Split('/');

                if (names.Length == 2)
                {
                    var elem = names[1].Split('|');

                    foreach (var e in elem)
                    {
                        using (var reader = XmlReader.Create($"data/osm/xml/regions/{names[0]}/{e}-tertiary-roads.osm"))
                        {
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
                        }
                    }
                }
                else
                {
                    var elem = names[0].Split('|');

                    foreach (var e in elem)
                    {
                        using (var reader = XmlReader.Create($"data/osm/xml/countries/tertiary-roads/{e}-tertiary-roads.osm"))
                        {
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
                        }
                    }
                }

                while (seeds[region.Key][0] > 0)
                {
                    var num = idFrom[random.Next(idFrom.Count)];
                    var id = nodeDict.Keys.ToList()[random.Next(nodeDict.Keys.ToList().Count)];
                    records[num][1] = region.Key;
                    records[num][2] = id;

                    idFrom.Remove(num);
                    seeds[region.Key][0] = seeds[region.Key][0] - 1;
                }

                while (seeds[region.Key][1] > 0)
                {
                    var num = idTo[random.Next(idTo.Count)];
                    var id = nodeDict.Keys.ToList()[random.Next(nodeDict.Keys.ToList().Count)];
                    records[num][3] = region.Key;
                    records[num][4] = id;

                    idTo.Remove(num);

                    var randomLevel = random.Next(0, 10);
                    var randomWeight = 0;
                    if (randomLevel < 6) { randomWeight = random.Next(1, 6); }
                    else if (randomLevel < 9) { randomWeight = random.Next(6, 11); }
                    else if (randomLevel < 10) { randomWeight = random.Next(11, 26); }
                    records[num][5] = randomWeight.ToString();

                    seeds[region.Key][1] = seeds[region.Key][1] - 1;
                }
            }

            using (var writer = new StreamWriter("data/tests/" + name + ".csv"))
            {
                foreach (var record in records)
                {
                    var line = string.Join(",", record);
                    writer.WriteLine(line);
                }
            }

            CheckTest(seeds, name);
            // CheckTest(seeds, name);
            PointsToWarehouses(countriesList, name, seeds);
        }


        public void CheckTest(Dictionary<string, List<int>> seeds, string name)
        {
            var n = 0;
            var m = seeds.Keys.Count;

            foreach (var region in seeds.Keys)
            {
                ++n;
                colour.WriteLine("y",$"----------[{n}/{m}]----------");

                var names = region.Split('/');
                List<Graph> graphs = new List<Graph>();
                Graph graph = new Graph();
                List<long> ignored = new List<long>();

                if (names.Length == 2)
                {
                    var elem = names[1].Split('|');

                    using (var reader = new StreamReader("data/tests/" + name + ".csv"))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var lines = csv.GetRecords<dynamic>().ToList();

                        if (elem.Length != 1)
                        {
                            foreach (var e in elem)
                            {
                                graphs.Add(graphImport.GraphImportByFile($"regions/{names[0]}/",e,"tertiary-roads"));
                            }

                            for (int l = 0; l < graphs.Count; l++)
                            {
                                graph.ConnectedGraph(graphs[l].AdjacencyList);
                            }
                        }
                        else
                        {
                            graph = graphImport.GraphImportByFile($"regions/{names[0]}/",elem[0],"tertiary-roads");
                        }

                        foreach (var line in lines)
                        {
                            if (line.From == region)
                            {
                                ignored.Add(long.Parse(line.FromID));
                            }

                            if (line.To == region)
                            {
                                ignored.Add(long.Parse(line.ToID));
                            }
                        }
                    }
                }
                else
                {
                    var elem = names[0].Split('|');

                    using (var reader = new StreamReader("data/tests/" + name + ".csv"))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var lines = csv.GetRecords<dynamic>().ToList();

                        if (elem.Length != 1)
                        {
                            foreach (var e in elem)
                            {
                                graphs.Add(graphImport.GraphImportByFile($"countries/tertiary-roads/",e,"tertiary-roads"));
                            }

                            for (int l = 0; l < graphs.Count; l++)
                            {
                                graph.ConnectedGraph(graphs[l].AdjacencyList);
                            }
                        }
                        else
                        {
                            graph = graphImport.GraphImportByFile($"countries/tertiary-roads/",elem[0],"tertiary-roads");
                        }

                        foreach (var line in lines)
                        {
                            if (line.From == region)
                            {
                                ignored.Add(long.Parse(line.FromID));
                            }

                            if (line.To == region)
                            {
                                ignored.Add(long.Parse(line.ToID));
                            }
                        }
                    }
                }

                long warehouseID = 0;

                using (var reader2 = new StreamReader("data/RegionHubsAddresses.csv"))
                using (var csv2 = new CsvReader(reader2, CultureInfo.InvariantCulture))
                {
                    var lines2 = csv2.GetRecords<dynamic>().ToList();

                    foreach (var line in lines2)
                    {
                        if (line.Region.Remove(line.Region.Length - 1) == region)
                        {
                            ignored.Add(long.Parse(line.ID));
                            warehouseID = long.Parse(line.ID);
                        }
                    }
                }

                List<long> wrongNodes = new List<long>();
                List<long> correctNodes = new List<long>();

                graph.OptimalizationGraph(ignored);

                foreach (var ignore in ignored)
                {
                    if (ignore == warehouseID) {continue; }
                    if (!graph.IsPathExists(ignore, warehouseID))
                    {
                        wrongNodes.Add(ignore);
                        colour.WriteLine("r",$"The {ignore} is wrong Node!!!");
                    }
                    else
                    {
                        correctNodes.Add(ignore);
                    }
                }

                var lines3 = new List<dynamic>();

                using (var reader2 = new StreamReader("data/tests/" + name + ".csv"))
                using (var csv2 = new CsvReader(reader2, CultureInfo.InvariantCulture))
                {
                    lines3 = csv2.GetRecords<dynamic>().ToList();
                }

                foreach (var line in lines3)
                {
                    if (wrongNodes.Contains(long.Parse(line.FromID)))
                    {
                        var correctValue = correctNodes[random.Next(correctNodes.Count)];
                        line.FromID = correctValue.ToString();
                    }

                    if (wrongNodes.Contains(long.Parse(line.ToID)))
                    {
                        var correctValue = correctNodes[random.Next(correctNodes.Count)];
                        line.ToID = correctValue.ToString();
                    }
                }

                using (var writer2 = new StreamWriter("data/tests/" + name + ".csv"))
                using (var csv2 = new CsvWriter(writer2, CultureInfo.InvariantCulture))
                {
                    csv2.WriteRecords(lines3);
                }
            }
        }


        public void PointsToWarehouses(List<string> countriesList, string name, Dictionary<string, List<int>> seeds)
        {
            ImportWareHouses(countriesList);

            var records = new List<dynamic>();

            using (var reader = new StreamReader("data/tests/" + name + ".csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                records = csv.GetRecords<dynamic>().ToList();
            }

            var m = seeds.Keys.Count();
            var n = 0;

            Stopwatch sw = new Stopwatch();
            colour.WriteLine("b","\nStart");
            sw.Start();

            foreach (var region in seeds.Keys)
            {
                n++;
                Console.WriteLine($"\t[{n}/{m}] - {region}");

                if (wareHousesBase[region].Count == 1)
                {
                    foreach (var record in records)
                    {
                        if (record.From == region)
                        {
                            record.From += "1";
                        }

                        if (record.To == region)
                        {
                            record.To += "1";
                        }
                    }
                }
                else
                {
                    var fileName = region;
                    var names = fileName.Split('/');
                    Graph graph = new Graph();
                    

                    if (names.Length == 2)
                    {
                        var namesRegions = names[1].Split('|');

                        foreach (var nameRegion in namesRegions)
                        {
                            graph.ConnectedNodes(graphImport.ImportNodes($"regions/{names[0]}/",nameRegion,"tertiary-roads"));
                        }
                    }
                    else
                    {
                        var namesCountries = names[0].Split('|');

                        foreach (var nameCountry in namesCountries)
                        {
                            graph.ConnectedNodes(graphImport.ImportNodes($"countries/tertiary-roads/",nameCountry,"tertiary-roads"));
                        }
                    }

                    foreach (var record in records)
                    {
                        if (record.From == region)
                        {
                            List<double> distances = Enumerable.Repeat(0D, wareHousesBase[region].Count).ToList();

                            for (int i = 0; i < distances.Count; ++i)
                            {
                                distances[i] = Math.Pow(graph.nodeDict[long.Parse(record.FromID)].Item1 - graph.nodeDict[wareHousesBase[region][i]].Item1, 2) 
                                + Math.Pow(graph.nodeDict[long.Parse(record.FromID)].Item2 - graph.nodeDict[wareHousesBase[region][i]].Item2, 2);
                            }

                            record.From += (distances.IndexOf(distances.Min()) + 1).ToString();
                        }

                        if (record.To == region)
                        {
                            List<double> distances = Enumerable.Repeat(0D, wareHousesBase[region].Count).ToList();

                            for (int i = 0; i < distances.Count; ++i)
                            {
                                distances[i] = Math.Pow(graph.nodeDict[long.Parse(record.ToID)].Item1 - graph.nodeDict[wareHousesBase[region][i]].Item1, 2) 
                                + Math.Pow(graph.nodeDict[long.Parse(record.ToID)].Item2 - graph.nodeDict[wareHousesBase[region][i]].Item2, 2);
                            }

                            record.To += (distances.IndexOf(distances.Min()) + 1).ToString();
                        }
                    }

                    graph.nodeDict.Clear();
                }
            }

            sw.Stop();
            colour.WriteLine("b",$"Stop, time: ... {sw.Elapsed.TotalSeconds} s");

            using (var writer2 = new StreamWriter("data/tests/" + name + ".csv"))
            using (var csv = new CsvWriter(writer2, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }
        }


         public void ImportWareHouses(List<string> countriesList)
        {
            using (var reader = new StreamReader("data/RegionHubsAddresses.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>();

                foreach (var record in records)
                {
                    foreach (var country in countriesList)
                    {
                        if (record.Region.Substring(0, 5) == country.Substring(0, 5))
                        {
                            wareHouses[record.Region] = long.Parse(record.ID);
                            var originalRegion = record.Region.Remove(record.Region.Length - 1);

                            if (!wareHousesBase.Keys.ToList().Contains(originalRegion))
                            {
                                wareHousesBase[originalRegion] = new List<long>();
                            }

                            wareHousesBase[originalRegion].Add(long.Parse(record.ID));
                        }
                    }
                }
            }
        }


        public Dictionary<string, double> RegionsSeeds()
        {
            Dictionary<string, double> seeds = new Dictionary<string, double>();
            Dictionary<string, double> valueSeeds = new Dictionary<string, double>();

            seeds["austria"] = 9.132;
            seeds["belgium"] = 11.82;
            seeds["czech-republic"] = 10.87;

            seeds["germany/baden-wuerttemberg"] = 11.28;
            seeds["germany/bayern"] = 13.37;
            seeds["germany/brandenburg"] = 2.5;
            seeds["germany/hamburg|schleswig-holstein"] = 4.867;
            seeds["germany/hessen"] = 6.391;
            seeds["germany/mecklenburg-vorpommern"] = 1.6;
            seeds["germany/niedersachsen"] = 8.003;
            seeds["germany/nordrhein-westfalen"] = 18.15;
            seeds["germany/rheinland-pfalz|saarland"] = 5.151;
            seeds["germany/sachsen"] = 4.038;
            seeds["germany/sachsen-anhalt"] = 2.169;
            seeds["germany/thueringen"] = 2.12;

            seeds["hungary"] = 9.59;
            seeds["luxembourg"] = 0.669;
            
            seeds["netherlands/drenthe|friesland|groningen"] = 1.4;
            seeds["netherlands/flevoland|gelderland|noord-holland|overijssel|utrecht"] = 7.076;
            seeds["netherlands/limburg|noord-brabant|zeeland|zuid-holland"] = 7.95;
            
            seeds["poland/dolnoslaskie"] = 2.901;
            seeds["poland/kujawsko-pomorskie"] = 2.078;
            seeds["poland/lodzkie"] = 2.466;
            seeds["poland/lubelskie"] = 2.118;
            seeds["poland/lubuskie"] = 1.015;
            seeds["poland/malopolskie"] = 3.401;
            seeds["poland/mazowieckie"] = 5.403;
            seeds["poland/opolskie"] = 0.987;
            seeds["poland/podkarpackie"] = 2.129;
            seeds["poland/podlaskie"] = 1.182;
            seeds["poland/pomorskie"] = 2.334;
            seeds["poland/slaskie"] = 4.534;
            seeds["poland/swietokrzyskie"] = 1.242;
            seeds["poland/warminsko-mazurskie"] = 1.429;
            seeds["poland/wielkopolskie"] = 3.494;
            seeds["poland/zachodniopomorskie"] = 1.701;

            seeds["slovakia"] = 5.427;
            seeds["switzerland|liechtenstein"] = 8.89;

            double summary = 0;

            foreach (var region in seeds)
            {
                summary += region.Value;
            }

            foreach (var region in seeds)
            {
                valueSeeds[region.Key] = region.Value/summary;
            }

            Dictionary<string, double> progs = new Dictionary<string, double>();
            double value = 0;

            foreach (var region in valueSeeds)
            {
                value += region.Value;
                progs[region.Key] = value;
            }

            return progs;
        }
    }
}