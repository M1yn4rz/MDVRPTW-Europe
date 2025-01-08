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
using NetTopologySuite.Algorithm;
using GMap.NET;
using GMap.NET.WindowsForms;
using System.Xml.Linq;
using System.Globalization;
using System.Configuration;



namespace Evolution
{
    class Data
    {
        Colour colour = new Colour();

        public Dictionary<string, List<string>> regions = new Dictionary<string, List<string>>() {
            {"france|andorra|monaco", new List<string> {
                "alsace",
                "aquitaine",
                "auvergne",
                "basse-normandie",
                "bourgogne",
                "bretagne",
                "centre",
                "champagne-ardenne",
                "corse",
                "franche-comte",
                "guadeloupe",
                "guyane",
                "haute-normandie",
                "ile-de-france",
                "languedoc-roussillon",
                "limousin",
                "lorraine",
                "martinique",
                "mayotte",
                "midi-pyrenees|andorra",
                "nord-pas-de-calais",
                "pays-de-la-loire",
                "picardie",
                "poitou-charentes",
                "provence-alpes-cote-d-azur|monaco",
                "reunion",
                "rhone-alpes"
            }},
            {"germany", new List<string> {
                "baden-wuerttemberg",
                "bayern",
                "brandenburg",
                "hamburg|schleswig-holstein",
                "hessen",
                "mecklenburg-vorpommern",
                "niedersachsen",
                "nordrhein-westfalen",
                "rheinland-pfalz|saarland",
                "sachsen",
                "sachsen-anhalt",
                "thueringen"
            }},
            {"italy", new List<string> {
                "centro",
                "isole",
                "nord-est",
                "nord-ovest",
                "sud"
            }},
            {"netherlands", new List<string> {
                "drenthe|friesland|groningen",
                "flevoland|gelderland|noord-holland|overijssel|utrecht",
                "limburg|noord-brabant|zeeland|zuid-holland"
                }},
            {"poland", new List<string> {
                "dolnoslaskie",
                "kujawsko-pomorskie",
                "lodzkie",
                "lubelskie",
                "lubuskie",
                "malopolskie",
                "mazowieckie",
                "opolskie",
                "podkarpackie",
                "podlaskie",
                "pomorskie",
                "slaskie",
                "swietokrzyskie",
                "warminsko-mazurskie",
                "wielkopolskie",
                "zachodniopomorskie"
            }},
            {"russia", new List<string> {
                "central-fed-district",
                "northwestern-fed-district",
            }},
            {"spain", new List<string> {
                "andalucia",
                "aragon",
                "asturias",
                "cantabria",
                "castilla-la-mancha",
                "castilla-y-leon",
                "cataluna",
                "ceuta",
                "extremadura",
                "galicia",
                "islas-baleares",
                "la-rioja",
                "madrid",
                "melilla",
                "murcia",
                "navarra",
                "pais-vasco",
                "valencia"
            }}
        };


        public List<string> countries = new List<string> {
            "albania",
            "austria",
            "belarus",
            "belgium",
            "bosnia-herzegovina",
            "bulgaria",
            "croatia",
            "czech-republic",
            "denmark",
            "estonia",
            "finland",
            "france|andorra|monaco",
            "germany",
            "greece",
            "hungary",
            "italy",
            "kosovo",
            "latvia",
            "lithuania",
            "luxembourg",
            "macedonia",
            "moldova",
            "montenegro",
            "netherlands",
            "norway",
            "poland",
            "portugal",
            "romania",
            "russia",
            "serbia",
            "slovakia",
            "slovenia",
            "spain",
            "sweden",
            "switzerland|liechtenstein",
            "ukraine"
        };


        public async Task DownloadFile(string urlPath, string area, string outPath)
        {
            string url = "https://download.geofabrik.de/" + urlPath + area + "-latest.osm.pbf";
            string outputPath = outPath + area + "-latest.osm.pbf";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Console.WriteLine($"\nStart downloading file {area}...");
                    
                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                        using (FileStream streamToWriteTo = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            await streamToReadFrom.CopyToAsync(streamToWriteTo);
                            colour.WriteLine("g", $"File {area} was downloaded complete!\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    colour.WriteLine("r", $"Error with downloading file {area}: {ex.Message}\n");
                }
            }
        }


        public void FilteringData(string urlPath, string area, string outPath, string fileName, string flag)
        {
            using (var osmBase = File.OpenRead("data/osm/pbf/" + urlPath + area + "-latest.osm.pbf"))
            {
                Console.WriteLine($"\nStart flirting data for {area}...");

                var source = new PBFOsmStreamSource(osmBase);

                var roadTypes = new List<string>();

                var roadRegions = new List<string> {
                    //"residential",
                    "tertiary",
                    "secondary",
                    "primary",
                    "trunk",
                    "motorway",
                    "motorway_link",
                    "trunk_link",
                    "primary_link",
                    "secondary_link",
                    "tertiary_link",
                    "motorway_junction",
                    "rest_area",
                    "services"
                    //"unclassified",
                    //"mini_roundabout"
                };

                var roadCountries = new List<string> {
                    //"residential",
                    //"tertiary",
                    //"secondary",
                    "primary",
                    "trunk",
                    "motorway",
                    "motorway_link",
                    "trunk_link",
                    "primary_link",
                    //"secondary_link",
                    //"tertiary_link",
                    "motorway_junction",
                    "rest_area",
                    "services"
                    //"unclassified",
                    //"mini_roundabout"
                };

                var roadEurope = new List<string> {
                    //"residential",
                    //"tertiary",
                    //"secondary",
                    //"primary",
                    "trunk",
                    "motorway",
                    "motorway_link",
                    "trunk_link",
                    //"primary_link",
                    //"secondary_link",
                    //"tertiary_link",
                    "motorway_junction",
                    "rest_area",
                    "services"
                    //"unclassified",
                    //"mini_roundabout"
                };

                Console.WriteLine("\t[1/7]");

                switch(flag)
                {
                    case "p1":
                        roadTypes = roadEurope;
                        break;

                    case "p2":
                        roadTypes = roadCountries;
                        break;

                    case "p3":
                        roadTypes = roadRegions;
                        break;

                    default:
                        colour.WriteLine("r","Error - wrong flag");
                        break;
                }

                var ways = from osmGeo in source
                        where osmGeo.Type == OsmSharp.OsmGeoType.Way 
                                && osmGeo.Tags != null 
                                && osmGeo.Tags.Any(tag => tag.Key == "highway" && roadTypes.Contains(tag.Value))
                        select osmGeo;

                Console.WriteLine("\t[2/7]");

                var nodeIds = ways
                            .OfType<OsmSharp.Way>()
                            .SelectMany(way => way.Nodes)
                            .ToHashSet();

                Console.WriteLine("\t[3/7]");

                var nodes = from osmGeo in source
                            where osmGeo.Type == OsmSharp.OsmGeoType.Node 
                                && osmGeo.Id.HasValue 
                                && nodeIds.Contains(osmGeo.Id.Value)
                            select osmGeo;

                Console.WriteLine("\t[4/7]");

                var filtered = ways.Concat(nodes);

                Console.WriteLine("\t[5/7]");

                using (var fileStream = File.OpenWrite("data/osm/xml/" + outPath + area + "-" + fileName + ".osm"))
                using (var osmWriter = new OsmSharp.Streams.XmlOsmStreamTarget(fileStream))
                {
                    Console.WriteLine("\t[6/7]");
                    osmWriter.RegisterSource(filtered);
                    osmWriter.Pull();
                }

                Console.WriteLine("\t[7/7]");
                colour.WriteLine("g",$"Filtered data complete for {area}!\n");
            }
        }



        public async void DownloadCountries()
        {
            string urlPath = "";

            foreach(var country in countries)
            {
                if (country != "russia") {urlPath = "europe/"; }
                else {urlPath = ""; }

                await DownloadFile(urlPath, country, "data/osm/pbf/countries/");
            }
        }


        public async void DownloadCountriesByCL(string countriesList)
        {
            string urlPath = "";

            foreach(var country in countries)
            {
                if (countriesList.Contains(country))
                {
                    if (country != "russia") {urlPath = "europe/"; }
                    else {urlPath = ""; }

                    await DownloadFile(urlPath, country, "data/osm/pbf/countries/");
                }
            }
        }


        public void DownloadRegions()
        {
            string urlPath = "";

            foreach (var country in regions.Keys)
            {
                if (country != "russia") {urlPath = "europe/"; }
                else {urlPath = ""; }

                foreach (var region in regions[country])
                {
                    _ = DownloadFile(urlPath + country + "/", region, "data/osm/pbf/regions/" + country + "/");
                }
            }
        }


        public void DownloadRegionsByCL(string countriesList)
        {
            string urlPath = "";

            foreach (var country in regions.Keys)
            {
                if (countriesList.Contains(country))
                {
                    if (country != "russia") {urlPath = "europe/"; }
                    else {urlPath = ""; }

                    foreach (var region in regions[country])
                    {
                        _ = DownloadFile(urlPath + country + "/", region, "data/osm/pbf/regions/" + country + "/");
                    }
                }
            }
        }


        public void DisplayNodes(string urlPath, string area, string fileName)
        {
            Console.WriteLine("\tReading file...");

            var xml = XDocument.Load("data/osm/xml/" + urlPath + area + "-" + fileName + ".osm");

            Console.WriteLine("\tCreate variables...");

            var nodeDict = xml.Descendants("node")
            .Where(n => n.Descendants("tag")
                        .Any(tag => tag.Attribute("k")?.Value == "addr:city"))
            .ToDictionary(
                n => n.Attribute("id")?.Value,
                n =>
                {
                    var city = n.Descendants("tag")
                                .FirstOrDefault(tag => tag.Attribute("k")?.Value == "addr:city")?
                                .Attribute("v")?.Value ?? "Unknown";

                    var lat = double.Parse(n.Attribute("lat")?.Value, CultureInfo.InvariantCulture);
                    var lon = double.Parse(n.Attribute("lon")?.Value, CultureInfo.InvariantCulture);

                    return (city, new PointLatLng(lat, lon));
                }
            );

            byte k = 100;

            Console.WriteLine($"Number of Nodes: {nodeDict.Count}\n");

            foreach (var node in nodeDict)
            {
                if (k <= 0) {break; }
                k--;

                Console.WriteLine($"\t{node.Key} : {node}");
            }
        }


        public void FilteringCountriesPrimary()
        {
            List<string> exceptions = new List<string>(){
                "albania",
                "bosnia-herzegovina",
                "kosovo",
                "macedonia",
                "montenegro",
            };

            string filter = "p1";

            foreach (var country in countries)
            {
                if (exceptions.Contains(country)) {filter = "p2"; }
                else if (country == "monaco") {filter = "p1"; }
                else {continue; }
                FilteringData("countries/", country, "countries/primary-roads/","primary-roads",filter);
            }
        }


        public void FilteringCountriesSecondary()
        {
            int i = countries.Count;
            int j = 0;

            foreach (var country in countries)
            {
                j++;
                colour.WriteLine("y",$"----------[{j}/{i}]----------");

                FilteringData("countries/",country,"countries/secondary-roads/","secondary-roads","p2");
            }
        }


        public void FilteringCountriesTertiary()
        {
            int i = countries.Count;
            int j = 0;

            foreach (var country in countries)
            {
                j++;
                colour.WriteLine("b",$"----------[{j}/{i}]----------");

                if (regions.ContainsKey(country))
                {
                    foreach (var region in regions[country])
                    {
                        FilteringData($"regions/{country}/",region,$"regions/{country}/","tertiary-roads","p3");
                    }
                }
                else
                {
                    FilteringData("countries/",country,"countries/tertiary-roads/","tertiary-roads","p3");
                }
            }
        }
    }
}