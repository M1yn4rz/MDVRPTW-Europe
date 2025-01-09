using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Evolution;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Noding.Snapround;
using NetTopologySuite.Operation.Overlay;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices;



namespace Evolution
{
    class Algorithm
    {
        Graph graph = new Graph();
        GraphImport graphImport = new GraphImport();
        Colour colour = new Colour();
        Data data = new Data();
        Random random = new Random();

        Dictionary<string,List<long>> regionOrders = new Dictionary<string,List<long>>();

        /*
            Dictionary<string,List<List<long>>> countriesTracks :
                Dictionary.Key : country name
                Dictionary.Value : List of ways from every warehouse
                    Value[0] : distance in km
                    Value[1] : summary weights of packages
                    Value[2:] : nodes in every way
        */
        Dictionary<string,List<List<long>>> countriesTracks = new Dictionary<string, List<List<long>>>();
        
        /*
            Dictionary<string,List<List<long>>> countryOrders :
                Dictionary.Key : country name
                Dictionary.Value : List of ways from every warehouse
                    Value[0] : "1" = "toSortOffice" | "-1" = "fromSortOffice"
                    Value[1] : node of warehouse
                    Value[2] : default = "-1" | distance from every warehouse to sort office by dijkstra
                    Value[3] : summary weight all packages on this way
                    Value[4:] : List of ID's packages
        */
        Dictionary<string,List<List<long>>> countryOrders = new Dictionary<string,List<List<long>>>();
        Dictionary<string,List<List<List<long>>>> populationRegionDNA = new Dictionary<string,List<List<List<long>>>>();
        Dictionary<string, long> wareHouses = new Dictionary<string, long>();Dictionary<string, List<long>> wareHousesBase = new Dictionary<string, List<long>>();
        Dictionary<string, long> sortOffices = new Dictionary<string, long>();
        


        public Algorithm(List<string> countriesList, string testName, int numberPopulation, int numberEpochs, int percentLeft, List<double> powers, List<double> freqs, int packagesPerDriver, bool showWarehousesMap, bool showSortOfficesMap, string mode, int warehousesMaxHours, int sortOfficesMaxHours)
        {   
            if (mode != "all" && mode != "sortOffices" && mode != "warehouses")
            {
                colour.WriteLine("r","ERROR - Wrong mode\nAvailable mods:\n[all]\n[sortOffices]\n[warehouses]");
                return;
            }

            ImportWareHouses(countriesList);
            ImportSortOffices();
            ImportNotDelivered(testName);

            if (mode == "all" || mode == "warehouses")
            {
                StartSortOrdersRegions(countriesList);
                FirstPopulation(numberPopulation, packagesPerDriver);
                RegionsLoop(numberPopulation, numberEpochs, percentLeft, powers, freqs, packagesPerDriver, showWarehousesMap, false, testName, warehousesMaxHours);
                StopSortOrdersRegions();

                graph.AdjacencyList.Clear();
                graph.OptimalizationList.Clear();
                graph.nodeDict.Clear();
            }
            
            if (mode == "all" || mode == "sortOffices")
            {
                StartSortOrdersCountries(countriesList, 1);
                CountriesLoop(showSortOfficesMap, countriesList, sortOfficesMaxHours);
                StopSortOrdersCountries();

                graph.AdjacencyList.Clear();
                graph.OptimalizationList.Clear();
                graph.nodeDict.Clear();
            }
        }


        public void ImportSortOffices()
        {
            using (var reader = new StreamReader("data/CentralHubsAddresses.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>().ToList();

                foreach (var record in records)
                {
                    sortOffices[record.Country] = long.Parse(record.ID);
                }
            }
        }


        public void StopSortOrdersCountries()
        {
            var records = new List<dynamic>();

            using (var reader = new StreamReader("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                records = csv.GetRecords<dynamic>().ToList();
            }

            foreach (var record in records)
            {
                if (record.Stage == "toSortOffice")
                {
                    if (record.From.Substring(0,5) != record.To.Substring(0,5))
                    {
                        record.Stage = "betweenSortOffice";
                    }
                    else
                    {
                        record.Stage = "fromSortOffice"; 
                    }
                    
                    foreach (var country in data.countries)
                    {
                        if (record.From.Contains(country))
                        {
                            record.FromID = sortOffices[country];
                            record.From = country;
                            break;
                        }
                    }
                }
            }

            using (var writer = new StreamWriter("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }
        }


        public void StartSortOrdersCountries(List<string> countriesList, long fromtoSortOffice)
        {
            foreach (var country in data.countries)
            {
                if (countriesList.Contains(country))
                {
                    countryOrders[country] = new List<List<long>>();
                }
            }

            using (var reader = new StreamReader("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>().ToList();

                foreach (var countryOrder in countryOrders.Keys)
                {
                    var i = 0;

                    foreach (var region in wareHouses)
                    {
                        if (region.Key.Contains(countryOrder))
                        {
                            countryOrders[countryOrder].Add(new List<long>() {fromtoSortOffice, region.Value, -1, 0});

                            foreach (var record in records)
                            {
                                if (fromtoSortOffice == 1)
                                {
                                    if (record.From == region.Key && record.Stage == "toSortOffice")
                                    {
                                        countryOrders[countryOrder][i].Add(long.Parse(record.ID));
                                    }
                                }
                                else if (fromtoSortOffice == -1)
                                {
                                    if (record.From == region.Key && record.Stage == "fromSortOffice")
                                    {
                                        countryOrders[countryOrder][i].Add(long.Parse(record.ID));
                                    }
                                }
                                else
                                {
                                    colour.WriteLine("r","ERROR - wrong value of 'fromtoSortOffice'");
                                }
                            }

                            i++;
                        }
                    }
                }
            }
        }


        public void CountriesLoop(bool showPaths, List<string> countriesList, int sortOfficesMaxHours)
        {
            var listPaths = new List<List<List<long>>>();
            var bestPaths = new List<List<List<long>>>();
            var j = 0;

            foreach (var country in countryOrders)
            {
                var countryList = country.Key.Split('|');

                foreach (var everyCountry in countryList)
                {
                    graph.ConnectedGraph(graphImport.GraphImportByFile($"countries/secondary-roads/",everyCountry,"secondary-roads").AdjacencyList);
                }
                
                listPaths.Add(new List<List<long>>());
                List<long> ignored = new List<long>();

                foreach (var wareHouse in wareHouses)
                {
                    if (wareHouse.Key.Contains(country.Key))
                    {
                        ignored.Add(wareHouse.Value);
                    }
                }

                ignored.Add(sortOffices[country.Key]);

                graph.OptimalizationGraph(ignored);

                var i = 0;
                List<long> onTime = new List<long>();
                List<long> delayed = new List<long>();

                foreach (var wh in country.Value)
                {
                    if (wh[2] == -1)
                    {
                        var output = graph.OptimalizationDijkstra(wh[1], sortOffices[country.Key]);
                        wh[2] = (long) (111.32 * output.Item2);

                        if (wh[2] / 80 < 7)
                        {
                            onTime.Add(wh[1]);
                        }
                        else if (wh[2] / 80 < 10)
                        {
                            delayed.Add(wh[1]);
                        }
                        else
                        {
                            colour.WriteLine("r","ERROR - no implementation in this case in CountriesLoop()");
                        }
                        listPaths[j].Add(new List<long>() {wh[1], sortOffices[country.Key]});
                    }
                    
                    i++;
                }

                MyWarehousesToSortOfficesAlgorithm(onTime, country.Key, sortOfficesMaxHours);
                MyWarehousesToSortOfficesAlgorithm(delayed, country.Key, sortOfficesMaxHours);

                graph.AdjacencyList.Clear();
                graph.OptimalizationList.Clear();
                j++;
            }

            if (showPaths)
            {
                var i = 0;

                foreach (var country in countriesTracks)
                {
                    bestPaths.Add(new List<List<long>>());
                    var p = 0;

                    foreach (var driver in country.Value)
                    {
                        bestPaths[i].Add(new List<long>());

                        for (int s = 2; s < driver.Count; ++s)
                        {
                            bestPaths[i][p].Add(driver[s]);
                        }

                        p++;
                    }

                    i++;
                }

                Visualization visualization = new Visualization("ShowWareHousesPaths","regions/poland/","podkarpackie","tertiary-roads", null, null, null, listPaths, sortOffices, countriesList);
            }
        }


        public void MyWarehousesToSortOfficesAlgorithm(List<long> warehousesPoints, string country, int acceptableHours)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Console.WriteLine(country);

            /*
                Dictionary<long,List<List<long>>> allWariants :
                    Key -> long -> Warehouse node value
                    Value -> List<List<long>> -> List of all ways, which starts on Key, where:
                        List<long> -> List of points on way, where:
                            Value[0] -> distance all way in km
                            Value[1] -> distance all way without sortOffice
                            Value[2] -> summary weights of packages
                            Value[3:] -> points of warehouses on way
            */
            Dictionary<long,List<List<long>>> allWariants = new Dictionary<long, List<List<long>>>();
            Dictionary<Tuple<long,long>,long> diary = new Dictionary<Tuple<long, long>, long>();

            foreach (var point in warehousesPoints)
            {
                allWariants[point] = new List<List<long>>();
                double valueDistance;
                (_, valueDistance) = graph.OptimalizationDijkstra(point, sortOffices[country]);
                allWariants[point].Add(new List<long>() {(long) (111.32 * valueDistance), 0, -1});
            }

            foreach (var firstPoint in warehousesPoints)
            {
                for (int i = 0; i < allWariants[firstPoint].Count; ++i)
                {
                    long startValue = allWariants[firstPoint][i][1];
                    long endValue = 0;
                    long sortOfficeValue = 0;
                    double helpValue;

                    foreach (var endPoint in warehousesPoints)
                    {
                        long mainVariantValue = allWariants[firstPoint][i][0];
                        mainVariantValue += allWariants[endPoint][0][0];

                        if (endPoint == firstPoint) {continue; }
                        if (allWariants[firstPoint][i].Contains(endPoint)) {continue; }

                        if (allWariants[firstPoint][i].Count > 3)
                        {
                            if (!diary.Keys.Contains(SortedTuple(allWariants[firstPoint][i][allWariants[firstPoint][i].Count - 1], endPoint)))
                            {
                                (_, helpValue) = graph.OptimalizationDijkstra(allWariants[firstPoint][i][allWariants[firstPoint][i].Count - 1], endPoint);
                                endValue = (long) (111.32 * helpValue);
                                diary[SortedTuple(allWariants[firstPoint][i][allWariants[firstPoint][i].Count - 1], endPoint)] = (long) (111.32 * helpValue);
                            }
                            else
                            {
                                endValue = diary[SortedTuple(allWariants[firstPoint][i][allWariants[firstPoint][i].Count - 1], endPoint)];
                            }
                            
                        }
                        else
                        {
                            if (!diary.Keys.Contains(SortedTuple(firstPoint, endPoint)))
                            {
                                (_, helpValue) = graph.OptimalizationDijkstra(firstPoint, endPoint);
                                endValue = (long) (111.32 * helpValue);
                                diary[SortedTuple(firstPoint, endPoint)] = (long) (111.32 * helpValue);
                            }
                            else
                            {
                                endValue = diary[SortedTuple(firstPoint, endPoint)];
                            }
                        }

                        sortOfficeValue = allWariants[endPoint][0][0];

                        if ((startValue + endValue + sortOfficeValue)/80 > acceptableHours) {continue; }
                        if (mainVariantValue < startValue + endValue + sortOfficeValue) {continue; }

                        allWariants[firstPoint].Add(new List<long>() {startValue + endValue + sortOfficeValue, startValue + endValue, -1});

                        for (int j = 3; j < allWariants[firstPoint][i].Count; ++j)
                        {
                            allWariants[firstPoint][allWariants[firstPoint].Count - 1].Add(allWariants[firstPoint][i][j]);
                        }

                        allWariants[firstPoint][allWariants[firstPoint].Count - 1].Add(endPoint);
                    }
                }
            }

            if (!countriesTracks.Keys.Contains(country))
            {
                countriesTracks[country] = new List<List<long>>();
            }

            List<long> wrotePoints = new List<long>();
            
            while (wrotePoints.Count < warehousesPoints.Count)
            {
                var maxIndexFirst = 0;
                var maxIndexSecond = 0;
                long maxPoints = 0;
                var minimumDistance = long.MaxValue;

                for (int m = 0; m < warehousesPoints.Count; ++m)
                {
                    for (int n = allWariants[warehousesPoints[m]].Count - 1; n >= 0; --n)
                    {
                        if (IsEveryPointDifferent(wrotePoints, allWariants[warehousesPoints[m]][n]) && !wrotePoints.Contains(warehousesPoints[m]))
                        {
                            if (allWariants[warehousesPoints[m]][allWariants[warehousesPoints[m]].Count - 1].Count > maxPoints)
                            {
                                maxPoints = allWariants[warehousesPoints[m]][allWariants[warehousesPoints[m]].Count - 1].Count;
                                maxIndexFirst = m;
                            }

                            break;
                        }
                    }
                }

                maxPoints = allWariants[warehousesPoints[maxIndexFirst]][allWariants[warehousesPoints[maxIndexFirst]].Count - 1].Count;

                while (minimumDistance == long.MaxValue)
                {
                    for (int m = allWariants[warehousesPoints[maxIndexFirst]].Count - 1; m >= 0; --m)
                    {
                        if (allWariants[warehousesPoints[maxIndexFirst]][m].Count == maxPoints && allWariants[warehousesPoints[maxIndexFirst]][m][0] < minimumDistance && IsEveryPointDifferent(wrotePoints, allWariants[warehousesPoints[maxIndexFirst]][m]))
                        {
                            minimumDistance = allWariants[warehousesPoints[maxIndexFirst]][m][0];
                            maxIndexSecond = m;
                        }
                    }

                    maxPoints--;
                }
                
                countriesTracks[country].Add(new List<long>() {minimumDistance, -1, warehousesPoints[maxIndexFirst]});
                wrotePoints.Add(warehousesPoints[maxIndexFirst]);

                for (int m = 3; m < allWariants[warehousesPoints[maxIndexFirst]][maxIndexSecond].Count; ++m)
                {
                    countriesTracks[country][countriesTracks[country].Count - 1].Add(allWariants[warehousesPoints[maxIndexFirst]][maxIndexSecond][m]);
                    wrotePoints.Add(allWariants[warehousesPoints[maxIndexFirst]][maxIndexSecond][m]);
                }

                countriesTracks[country][countriesTracks[country].Count - 1].Add(sortOffices[country]);
            }

            sw.Stop();
            colour.WriteLine("b",$"\tTime: ... {sw.Elapsed.TotalSeconds} s");
        }


        public Tuple<long,long> SortedTuple(long firstValue, long secondValue)
        {
            if (firstValue < secondValue)
            {
                return new (firstValue, secondValue);
            }
            else
            {
                return new (secondValue, firstValue);
            }
        }
        

        public bool IsEveryPointDifferent(List<long> firstList, List<long> secondList)
        {
            foreach (var pointA in firstList)
            {
                if (secondList.Contains(pointA))
                {
                    return false;
                }
            }

            return true;
        }


        public bool EqualTuples(Tuple<long,long> firstTuple, Tuple<long,long> secondTuple)
        {
            return firstTuple.Item1 == secondTuple.Item1 && firstTuple.Item2 == secondTuple.Item2;
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

        
        public void StopSortOrdersRegions()
        {
            var records = new List<dynamic>();

            using (var reader = new StreamReader("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                records = csv.GetRecords<dynamic>().ToList();
            }

            foreach (var record in records)
            {
                if (record.Stage == "toWarehouse")
                {
                    record.Stage = "toSortOffice";
                    
                    var i = 0;
                    foreach (var region in populationRegionDNA.Keys)
                    {
                        if (region.Contains(record.From))
                        {
                            bool check = false;

                            foreach (var driver in populationRegionDNA[region][0])
                            {
                                if (driver.Contains(long.Parse(record.FromID)))
                                {
                                    check = true;
                                    break;
                                }
                            }

                            if (check)
                            {
                                record.FromID = wareHouses[region];
                                record.From = region;
                                break;
                            }
                            else
                            {
                                i++;
                            }
                        }
                    }
                }
            }

            using (var writer = new StreamWriter("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }
        }


        public void ImportNotDelivered(string testName)
        {
            using (var reader = new StreamReader($"data/tests/{testName}.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>().ToList();

                using (var writer = new StreamWriter($"data/algorithm/notDelivered.csv"))
                {
                    writer.WriteLine("ID,From,FromID,To,ToID,Stage");

                    foreach (var record in records)
                    {
                        var line = string.Join(",",new string[] {record.ID, record.From, record.FromID, record.To, record.ToID, "toWarehouse"});
                        writer.WriteLine(line);
                    }
                }
            }
        }


        public void StartSortOrdersRegions(List<string> countriesList)
        {
            foreach (var country in data.countries)
            {
                if (countriesList.Contains(country))
                {
                    if (data.regions.Keys.Contains(country))
                    {
                        foreach (var region in data.regions[country])
                        {
                            for (int i = 0; i < wareHousesBase[country + "/" + region].Count; ++i)
                            {
                                regionOrders[country + "/" + region + $"{i + 1}"] = new List<long>();
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < wareHousesBase[country].Count; ++i)
                        {
                            regionOrders[country + $"{i + 1}"] = new List<long>();
                        }
                    }
                }
            }

            using (var reader = new StreamReader("data/algorithm/notDelivered.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>().ToList();

                foreach (var regionOrder in regionOrders.Keys)
                {
                    foreach (var record in records)
                    {
                        if (regionOrder == record.From && record.Stage == "toWarehouse")
                        {
                            regionOrders[regionOrder].Add(long.Parse(record.FromID));
                        }
                    }
                }
            }
        }


        public void FirstPopulation(int numberPopulation, int packagesPerDriver)
        {
            var actually = 1;
            var max = wareHouses.Count();

            foreach (var regionOrder in wareHouses)
            {
                var oryginalRegion = regionOrder.Key.Remove(regionOrder.Key.Length - 1);
                int numberDrivers = regionOrders[regionOrder.Key].Count / packagesPerDriver + 1;

                Console.Clear();
                colour.WriteLine("b",$"Generating start population... ");
                colour.WriteLine("b",$"Region name: {oryginalRegion}");
                colour.WriteLine("b",$"Region number: {actually}/{max}");

                GeneratePopulation(numberPopulation, regionOrder.Key, numberDrivers, true);

                actually++;
            }
        }


        public void GeneratePopulation(int numberPopulation, string region, int numberDrivers, bool clearGraph)
        {
            populationRegionDNA[region] = new List<List<List<long>>>();

            for (var i = 0; i < numberPopulation; ++i)
            {
                populationRegionDNA[region].Add(new List<List<long>>());
                var randomizePackages = regionOrders[region].OrderBy(_ => random.Next()).ToList();
                var j = 0;

                for (int k = 0; k < numberDrivers; ++k)
                {
                    populationRegionDNA[region][i].Add(new List<long>(){-1});
                }

                while (j < randomizePackages.Count)
                {
                    for (int k = 0; k < numberDrivers; ++k)
                    {
                        if (j >= randomizePackages.Count) {break; }
                        populationRegionDNA[region][i][k].Add(randomizePackages[j]);
                        j++;
                    }
                }
            }
        }


        public bool VerificationDrivers(string region, int warehousesMaxHours)
        {
            foreach (var driver in populationRegionDNA[region][0])
            {
                if (driver[0] / 70 + 0.05 * (driver.Count - 1) > warehousesMaxHours)
                {
                    return false;
                }
            }

            return true;
        }


        public void RegionsLoop(int numberPopulation, int numberEpochs, int percentLeft, List<double> powers, List<double> freqs, int packagesPerDriver, bool showPaths, bool toOutput, string testName, int warehousesMaxHours)
        {
            var actually = 1;
            var max = populationRegionDNA.Count;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            string previousFileName = "";
            int maxIndex = (int) (numberPopulation * percentLeft / 100 - 1);
            int borderA = (int) (100 * freqs[0]);
            int borderB = (int) (borderA + 100 * freqs[1]);
            int borderC = (int) (borderB + 100 * freqs[2]);
            int warehouseNumber = 0;
            string previousCountry = "";

            foreach (var regionOrder in wareHouses)
            {   
                Stopwatch swCountry = new Stopwatch();
                swCountry.Start();

                int localMinimumCount = 0;
                var fileName = regionOrder.Key.Remove(regionOrder.Key.Length - 1);
                var names = fileName.Split('/');
                List<long> ignored = new List<long>();
                List<Tuple<long,long>> functionGoalValues = new List<Tuple<long, long>>();
                long bestSolution = 0;
                
                warehouseNumber++;
                
                if (previousCountry != names[0])
                {
                    warehouseNumber = 1;
                }

                previousCountry = names[0];
                string countryFolder = "";

                if (previousCountry.Contains("|"))
                {
                    foreach(var letter in previousCountry)
                    {
                        if (letter != '|')
                        {
                            countryFolder += letter;
                        }
                        else
                        {
                            countryFolder += '_';
                        }
                    }
                }
                else
                {
                    countryFolder = previousCountry;
                }

                string algorithmValues = "";
                
                if (previousFileName != fileName)
                {
                    graph.AdjacencyList.Clear();
                    graph.OptimalizationList.Clear();
                    graph.nodeDict.Clear();

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
                }

                previousFileName = fileName;

                Start:

                var actuallyNumberDrivers = populationRegionDNA[regionOrder.Key][0].Count;
                bool localMinimum = false;

                foreach (var (solution, solutionID) in populationRegionDNA[regionOrder.Key].Select((solution, index) => (solution, index)))
                {
                    long valueGoal = 0;

                    foreach (var (driver, driverID) in solution.Select((driver, index) => (driver, index)))
                    {
                        if (driver[0] != -1) {continue; }
                        
                        List<long> points = new List<long>(driver);
                        points[0] = wareHouses[regionOrder.Key];
                        points.Add(wareHouses[regionOrder.Key]);
                        populationRegionDNA[regionOrder.Key][solutionID][driverID][0] = GoalFunctionEuklides(points);
                        valueGoal += populationRegionDNA[regionOrder.Key][solutionID][driverID][0];
                    }

                    functionGoalValues.Add(new(valueGoal, solutionID));
                }

                QuickSort(functionGoalValues, 0, numberPopulation - 1, regionOrder.Key);
                SortPopulationDNARegion(functionGoalValues, numberPopulation, percentLeft, regionOrder.Key);

                for (var i = 0; i < numberEpochs; ++i)
                {
                    functionGoalValues.Clear();
                    functionGoalValues = new List<Tuple<long,long>>();

                    while (populationRegionDNA[regionOrder.Key].Count < numberPopulation)
                    {
                        int randomValue = random.Next(1, 101);
   
                        if (randomValue <= borderA)
                        {
                            MutationSwap(regionOrder.Key, maxIndex, powers[0]);
                        }
                        else if (randomValue <= borderB)
                        {
                            MutationScramble(regionOrder.Key, maxIndex, powers[1]);
                        }
                        else if (randomValue <= borderC)
                        {
                            CrossoverSwap(regionOrder.Key, maxIndex, powers[2]);
                        }
                        else
                        {
                            CrossoverLinearOrder(regionOrder.Key, maxIndex);
                        }
                    }

                    foreach (var (solution, solutionID) in populationRegionDNA[regionOrder.Key].Select((solution, index) => (solution, index)))
                    {
                        long valueGoal = 0;
                        
                        foreach (var (driver, driverID) in solution.Select((driver, index) => (driver, index)))
                        {
                            if (driver[0] != -1) 
                            {
                                valueGoal += populationRegionDNA[regionOrder.Key][solutionID][driverID][0];
                                continue; 
                            }

                            List<long> points = new List<long>(driver);
                            points[0] = wareHouses[regionOrder.Key];
                            points.Add(wareHouses[regionOrder.Key]);
                            populationRegionDNA[regionOrder.Key][solutionID][driverID][0] = GoalFunctionEuklides(points);
                            valueGoal += populationRegionDNA[regionOrder.Key][solutionID][driverID][0];
                        }

                        functionGoalValues.Add(new(valueGoal, solutionID));
                    }

                    QuickSort(functionGoalValues, 0, numberPopulation - 1, regionOrder.Key);
                    SortPopulationDNARegion(functionGoalValues, numberPopulation, percentLeft, regionOrder.Key);

                    long minValue = -1;
                    int count = 0;

                    foreach (var solution in populationRegionDNA[regionOrder.Key])
                    {
                        long value = 0;

                        foreach (var gf in solution)
                        {
                            value += gf[0];
                        }

                        count++;

                        if (minValue == -1)
                        {
                            minValue = value;
                            
                        }
                        else
                        {
                            if (minValue != value)
                            {
                                break;
                            }
                        } 
                    }

                    if (count >= percentLeft * numberPopulation / 100)
                    {
                        localMinimumCount++;
                    }
                    else
                    {
                        localMinimumCount = 0;
                    }

                    if (localMinimumCount >= 100)
                    {
                        localMinimum = true;
                    }

                    if (algorithmValues.Length > 0 )
                    {
                        algorithmValues += '|';
                    }

                    algorithmValues += minValue.ToString();
                    bestSolution = minValue;

                    if ((i + 1) % 10 == 0)
                    {
                        Console.Clear();
                        colour.WriteLine("b",$"Region name: ......... {fileName}");
                        colour.WriteLine("b",$"Region number: ....... {actually}/{max}");
                        colour.WriteLine("b",$"Number of drivers: ... {actuallyNumberDrivers}");
                        colour.WriteLine("b",$"\tEpochs: .............................. {i+1}/{numberEpochs}");
                        colour.WriteLine("b",$"\tThe best solution: ................... {minValue}");
                        colour.WriteLine("b",$"\tNumber of best solution copies: ...... {count}/{percentLeft * numberPopulation / 100}");
                        colour.WriteLine("b",$"\tNumber of epochs in local minimum: ... {localMinimumCount}/100");
                    }

                    if (i == numberEpochs - 1 || localMinimum == true)
                    {
                        if (!VerificationDrivers(regionOrder.Key, warehousesMaxHours))
                        {
                            GeneratePopulation(numberPopulation, regionOrder.Key, actuallyNumberDrivers + 1, false);
                            algorithmValues = "";
                            goto Start;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                
                actually++;
                swCountry.Stop();

                if (!File.Exists($"data/outputs/{countryFolder}/{countryFolder}_{warehouseNumber}.csv"))
                {
                    var records = new List<string[]>
                    {
                        new string[] {"testID","bestSolution","time","packagesPerDriver","packageTestName","Parameters","algorithmValues"}
                    };

                    records.Add(new string[] 
                        {
                            "1",bestSolution.ToString(CultureInfo.InvariantCulture),
                            swCountry.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                            actuallyNumberDrivers.ToString(CultureInfo.InvariantCulture),
                            testName,
                            $"{numberPopulation}|{numberEpochs}|{percentLeft}|{powers[0].ToString(CultureInfo.InvariantCulture)}|{powers[1].ToString(CultureInfo.InvariantCulture)}|{powers[2].ToString(CultureInfo.InvariantCulture)}|{freqs[0].ToString(CultureInfo.InvariantCulture)}|{freqs[1].ToString(CultureInfo.InvariantCulture)}|{freqs[2].ToString(CultureInfo.InvariantCulture)}|{freqs[3].ToString(CultureInfo.InvariantCulture)}|{packagesPerDriver}",
                            algorithmValues.ToString(CultureInfo.InvariantCulture)
                        });

                    using (var writer = new StreamWriter($"data/outputs/{countryFolder}/{countryFolder}_{warehouseNumber}.csv"))
                    {
                        foreach (var record in records)
                        {
                            var line = string.Join(",", record);
                            writer.WriteLine(line);
                        }
                    }
                }
                else
                {
                    List<dynamic> records;
                    string[] newRecord;

                    using (var reader = new StreamReader($"data/outputs/{countryFolder}/{countryFolder}_{warehouseNumber}.csv"))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        records = csv.GetRecords<dynamic>().ToList();
                    }

                    newRecord = new string[] {
                            (records.Count + 1).ToString(),
                            bestSolution.ToString(CultureInfo.InvariantCulture),
                            swCountry.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                            actuallyNumberDrivers.ToString(CultureInfo.InvariantCulture),
                            testName,
                            $"{numberPopulation}|{numberEpochs}|{percentLeft}|{powers[0].ToString(CultureInfo.InvariantCulture)}|{powers[1].ToString(CultureInfo.InvariantCulture)}|{powers[2].ToString(CultureInfo.InvariantCulture)}|{freqs[0].ToString(CultureInfo.InvariantCulture)}|{freqs[1].ToString(CultureInfo.InvariantCulture)}|{freqs[2].ToString(CultureInfo.InvariantCulture)}|{freqs[3].ToString(CultureInfo.InvariantCulture)}|{packagesPerDriver}",
                            algorithmValues.ToString(CultureInfo.InvariantCulture)
                        };

                    using (var writer = new StreamWriter($"data/outputs/{countryFolder}/{countryFolder}_{warehouseNumber}.csv"))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(records);

                        var line = string.Join(",", newRecord);
                        writer.WriteLine(line);
                    }
                }
            }

            sw.Stop();

            colour.WriteLine("b",$"\n\n\n----------FINISH RAPORT----------\n");
            colour.WriteLine("b",$"Algorithm results:");
            colour.WriteLine("b",$"");

            long valueGenerally = 0;
            int sumDrivers = 0;

            foreach (var region in wareHouses)
            {
                long valueRegion = 0;
                List<long> valueDrivers = new List<long>();
                sumDrivers += populationRegionDNA[region.Key][0].Count;

                foreach (var driver in populationRegionDNA[region.Key][0])
                {
                    valueDrivers.Add(driver[0]);
                    valueRegion += driver[0];
                }

                colour.WriteLine("b",$"\tRegion: ... {region.Key}");
                colour.WriteLine("b",$"\t\tNumber of drivers: ..... {populationRegionDNA[region.Key][0].Count}");
                colour.WriteLine("b",$"\t\tGoal function value: ... {valueRegion}");

                for (int i = 0; i < valueDrivers.Count; ++i)
                {
                    colour.WriteLine("b",$"\t\t\tGoal function {i}'s driver: ... {valueDrivers[i]}");
                }

                colour.WriteLine("b",$"");
                valueGenerally += valueRegion;
            }

            colour.WriteLine("b",$"Summary:");
            colour.WriteLine("b",$"\tParameters:\n");
            colour.WriteLine("b",$"\t\tNumber of population: ............... {numberPopulation}");
            colour.WriteLine("b",$"\t\tNumber of epochs: ................... {numberEpochs}");
            colour.WriteLine("b",$"\t\tPercent of previous population: ..... {percentLeft}");
            colour.WriteLine("b",$"");
            colour.WriteLine("b",$"\t\tMutation swap power: ................ {powers[0]}");
            colour.WriteLine("b",$"\t\tMutation scramble power: ............ {powers[1]}");
            colour.WriteLine("b",$"\t\tCrossover swap power: ............... {powers[2]}");
            colour.WriteLine("b",$"");
            colour.WriteLine("b",$"\t\tMutation swap frequency: ............ {freqs[0]}");
            colour.WriteLine("b",$"\t\tMutation scramble frequency: ........ {freqs[1]}");
            colour.WriteLine("b",$"\t\tCrossover swap frequency: ........... {freqs[2]}");
            colour.WriteLine("b",$"\t\tCrossover linear order frequency: ... {freqs[3]}");
            colour.WriteLine("b",$"");
            colour.WriteLine("b",$"\tTime: ... {sw.Elapsed.TotalSeconds} s");
            colour.WriteLine("b",$"");
            colour.WriteLine("b",$"\t\tGenerally goal function value: ... {valueGenerally}");
            colour.WriteLine("b",$"\t\tGenerally number of drivers: ..... {sumDrivers}");
            colour.WriteLine("b",$"\n");

            if (showPaths)
            {
                List<List<List<long>>> listPaths = new List<List<List<long>>>();

                var p = 0;
                foreach (var regionOrder in wareHouses)
                {
                    listPaths.Add(new List<List<long>>());  
                    var j = 0;

                    foreach (var driver in populationRegionDNA[regionOrder.Key][0])
                    {
                        listPaths[p].Add(new List<long>());
                        listPaths[p][j].Add(regionOrder.Value);

                        foreach (var point in driver)
                        {
                            if (point < 10000) {continue; }
                            listPaths[p][j].Add(point);
                        }

                        listPaths[p][j].Add(regionOrder.Value);
                        j++;
                    }                    
                    
                    p++;
                }

                Visualization visualization = new Visualization("ShowDriversPaths","regions/poland/","podkarpackie","tertiary-roads", null, null, null, listPaths, wareHouses);
            }
        }


        public long GoalFunction(List<long> points)
        {
            long value = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {   
                value += (long) (111.32 * graph.OptimalizationDijkstra(points[i], points[i + 1]).Item2);
            }

            return value;
        }


        public long GoalFunctionEuklides(List<long> points)
        {
            long value = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {   
                value += (long) (graph.DistanceNodes(points[i], points[i + 1]));
            }

            return value;
        }


        public int QuickSort(List<Tuple<long, long>> array, int low, int high, string region)
        {
            if (low < high)
            {
                int pivotIndex = Partition(array, low, high, region);

                QuickSort(array, low, pivotIndex - 1, region);
                QuickSort(array, pivotIndex + 1, high, region);
            }

            return 0;
        }


        public int Partition(List<Tuple<long, long>> array, int low, int high, string region)
        {
            long pivot = array[high].Item1;
            int i = low - 1;

            for (int j = low; j < high; j++)
            {
                if (array[j].Item1 <= pivot)
                {
                    i++;
                    Swap(array, i, j, region);
                }
            }

            Swap(array, i + 1, high, region);
            return i + 1;
        }


        public void Swap(List<Tuple<long, long>> array, int a, int b, string region)
        {
            Tuple<long, long> temp = array[a];
            array[a] = array[b];
            array[b] = temp;
        }


        public void SortPopulationDNARegion(List<Tuple<long, long>> array, int numberPopulation, int percentLeft, string region)
        {
            List<List<List<long>>> sortedDNA = new List<List<List<long>>>();

            for (int j = numberPopulation - 1; j > numberPopulation * percentLeft / 100 - 1; j--)
            {
                array.RemoveAt(j);
            }

            for (int i = 0; i < array.Count; i++)
            {
                for (int j = 0; j < numberPopulation; j++)
                {
                    if (j == array[i].Item2)
                    {
                        sortedDNA.Add(new List<List<long>>());

                        foreach (var innerList in populationRegionDNA[region][j])
                        {
                            sortedDNA[i].Add(new List<long>(innerList));
                        }

                        break;
                    }
                }
            }

            populationRegionDNA[region].Clear();
            populationRegionDNA[region] = sortedDNA;
        }


        public int MutationSwap(string region, int maxIndex, double mutationSwapPower)
        {
            int randomIndex = random.Next(0, maxIndex);
            List<List<long>> newSolution = new List<List<long>>();

            foreach (var innerList in populationRegionDNA[region][randomIndex])
            { 
                newSolution.Add(new List<long>(innerList)); 
            }

            int numberPoints = 0;

            if ((int) (mutationSwapPower * (newSolution[0].Count - 1)) > 1)
            {
                numberPoints = random.Next(1, (int) (mutationSwapPower * (newSolution[0].Count - 1)));
            }
            else
            {
                numberPoints = 1;
            }
            
            for (var i = 0; i < numberPoints; ++i)
            {
                int randomDriver = random.Next(0, newSolution.Count);
                int randomPointA = random.Next(1, newSolution[randomDriver].Count);
                int randomPointB = randomPointA;
                
                if (newSolution[randomDriver].Count < 3)
                {
                    return 0;
                }

                while (randomPointA == randomPointB)
                {
                    randomPointB = random.Next(1, newSolution[randomDriver].Count);
                }
                 
                long helpValue =  newSolution[randomDriver][randomPointA];
                newSolution[randomDriver][0] = -1;
                newSolution[randomDriver][randomPointA] = newSolution[randomDriver][randomPointB];
                newSolution[randomDriver][randomPointB] = helpValue;
            }
            
            populationRegionDNA[region].Add(newSolution);
            return 0;
        }


        public int MutationScramble(string region, int maxIndex, double mutationScramblePower)
        {
            int randomIndex = random.Next(0, maxIndex);
            List<List<long>> newSolution = new List<List<long>>();

            foreach (var innerList in populationRegionDNA[region][randomIndex])
            { 
                newSolution.Add(new List<long>(innerList)); 
            }

            int randomDriver = random.Next(0, newSolution.Count);
            int randomLength = 0;

            if ((int) (mutationScramblePower * (newSolution[randomDriver].Count - 1) + 1) > 5)
            {
                randomLength = random.Next(5, (int) (mutationScramblePower * (newSolution[randomDriver].Count - 1) + 1));
            }
            else if (newSolution[randomDriver].Count - 1 > 2)
            {
                randomLength = random.Next(2, newSolution[randomDriver].Count - 1);
            }
            else
            {
                return 0;
            }

            int randomPoint = random.Next(1, newSolution[randomDriver].Count - randomLength);
            List<long> lstToRandom = new List<long>();

            for (var i = randomPoint; i < randomPoint + randomLength; ++i)
            { 
                lstToRandom.Add(newSolution[randomDriver][i]); 
            }

            lstToRandom = lstToRandom.OrderBy(_ => random.Next(lstToRandom.Count)).ToList();
            
            for (var i = randomPoint; i < randomPoint + randomLength; ++i)
            { 
                newSolution[randomDriver][i] = lstToRandom[i - randomPoint]; 
            }

            newSolution[randomDriver][0] = -1;
            populationRegionDNA[region].Add(newSolution);
            return 0;
        }


        public void CrossoverLinearOrder(string region, int maxIndex)
        {
            int randomIndexA = random.Next(0, maxIndex);
            int randomIndexB = randomIndexA;

            while (randomIndexA == randomIndexB)
            {
                randomIndexB = random.Next(0, maxIndex);
            }

            List<List<long>> newSolution = new List<List<long>>();
            List<long> pointsToB = new List<long>();
            List<long> pointsFromB = new List<long>();

            for (var i = 0; i < populationRegionDNA[region][randomIndexA].Count; ++i)
            {
                newSolution.Add(new List<long>() {-1});
            }

            for (var i = 0; i < populationRegionDNA[region][randomIndexA].Count; ++i)
            {
                int randomPointA = random.Next(1, populationRegionDNA[region][randomIndexA][i].Count);
                int randomPointB = random.Next(randomPointA, populationRegionDNA[region][randomIndexA][i].Count);

                for (var j = 1; j < populationRegionDNA[region][randomIndexA][i].Count; j++)
                {
                    if (j >= randomPointA && j < randomPointB)
                    {
                        newSolution[i].Add(populationRegionDNA[region][randomIndexA][i][j]);
                    }
                    else
                    {
                        newSolution[i].Add(0);
                        pointsToB.Add(populationRegionDNA[region][randomIndexA][i][j]);
                    }
                }
            }

            for (var i = 0; i < populationRegionDNA[region][randomIndexB].Count; ++i)
            {
                for (var j = 1; j < populationRegionDNA[region][randomIndexB][i].Count; j++)
                {
                    if (pointsToB.Contains(populationRegionDNA[region][randomIndexB][i][j]))
                    {
                        pointsFromB.Add(populationRegionDNA[region][randomIndexB][i][j]);
                        pointsToB.Remove(populationRegionDNA[region][randomIndexB][i][j]);
                    }
                }
            }

            int pointIndex = 0;

            for (var i = 0; i < newSolution.Count; ++i)
            {
                for (var j = 1; j < newSolution[i].Count; j++)
                {
                    if (newSolution[i][j] == 0)
                    {
                        newSolution[i][j] = pointsFromB[pointIndex];
                        pointIndex++;
                    }
                }
            }

            populationRegionDNA[region].Add(newSolution);
        }


        public int CrossoverSwap(string region, int maxIndex, double CrossowerSwapPower)
        {
            int randomIndex = random.Next(0, maxIndex);
            List<List<long>> newSolution = new List<List<long>>();

            foreach (var innerList in populationRegionDNA[region][randomIndex])
            { 
                newSolution.Add(new List<long>(innerList)); 
            }

            int numberPoints = 0;

            if ((int) (CrossowerSwapPower * (newSolution[0].Count - 1)) > 1)
            {
                numberPoints = random.Next(1, (int) (CrossowerSwapPower * (newSolution[0].Count - 1)));
            }
            else
            {
                numberPoints = 1;
            }
            
            for (var i = 0; i < numberPoints; ++i)
            {
                int randomDriverA = random.Next(0, newSolution.Count);
                int randomDriverB = randomDriverA;

                if (newSolution.Count < 2)
                {
                    return 0;
                }

                while (randomDriverA == randomDriverB)
                {
                    randomDriverB = random.Next(0, newSolution.Count);
                }
                
                int randomPointA = random.Next(1, newSolution[randomDriverA].Count);
                int randomPointB = random.Next(1, newSolution[randomDriverB].Count);
                long helpValue =  newSolution[randomDriverA][randomPointA];
                newSolution[randomDriverA][0] = -1;
                newSolution[randomDriverB][0] = -1;
                newSolution[randomDriverA][randomPointA] = newSolution[randomDriverB][randomPointB];
                newSolution[randomDriverB][randomPointB] = helpValue;
            }
            
            populationRegionDNA[region].Add(newSolution);
            return 0;
        }
    }
}