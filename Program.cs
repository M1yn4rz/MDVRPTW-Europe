using System;
using Evolution;
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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CsvHelper;
using System.Globalization;



namespace Evolution
{
    class Program
    {
        static int Main(string[] args)
        {   
            Data data = new Data();
            Colour colour = new Colour();
            List<string> points = new List<string>();
            Random random = new Random();

            List<string> countriesList = new List<string>() {
                //"austria",
                //"belgium",
                //"czech-republic",
                //"germany",
                "hungary",
                //"luxembourg",
                //"netherlands",
                //"poland",
                //"slovakia",
                //"switzerland|liechtenstein"
            };
            
            int numberPopulation = 3000;
            int numberEpochs = 1000;
            int percentLeft = 5;

            double mutationSwapPower = 0.4;
            double mutationScramblePower = 0.3;
            double crossoverSwapPower = 0.4;

            double mutationSwapFreq = 0.3;
            double mutationScrambleFreq = 0.2;
            double crossoverSwapFreq = 0.15;
            double crossoverLinearOrderFreq = 0.35;

            int packagesPerDriver = 40;

            string mode = "warehouses";
            
            bool showWarehousesMap = true;
            bool showSortOfficesMap = false;

            int warehousesMaxHours = 9;
            int sortOfficesMaxHours = 7;

            if (mutationSwapFreq + mutationScrambleFreq + crossoverSwapFreq + crossoverLinearOrderFreq != 1)
            {
                colour.WriteLine("r","ERROR - The value of the sum of frequencies must be equal to 1");
                return 0;
            }

            Algorithm algorithm = new Algorithm(countriesList, "Test3kP10C", numberPopulation, numberEpochs, percentLeft, 
                new List<double>() {mutationSwapPower, mutationScramblePower, crossoverSwapPower}, 
                new List<double>() {mutationSwapFreq, mutationScrambleFreq, crossoverSwapFreq, crossoverLinearOrderFreq},
                packagesPerDriver, 
                showWarehousesMap, showSortOfficesMap, mode, warehousesMaxHours, sortOfficesMaxHours);
            
            return 0;
        }   
    }
}