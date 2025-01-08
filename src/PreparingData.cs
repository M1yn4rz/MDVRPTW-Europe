namespace Evolution
{
    class PreparingData
    {
        Data data = new Data();
        Colour colour = new Colour();


        public PreparingData(List<string> countriesList)
        {
            List<string> folderList = new List<string>()
            {
                "data/algorithm",
                "data/outputs",
                "data/osm",
                "data/osm/pbf",
                "data/osm/xml",
                "data/osm/pbf/countries",
                "data/osm/pbf/regions",
                "data/osm/xml/countries",
                "data/osm/xml/regions",
                "data/osm/xml/countries/primary-roads",
                "data/osm/xml/countries/secondary-roads",
                "data/osm/xml/countries/tertiary-roads",
            };

            foreach (var folder in folderList)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            foreach (var country in countriesList)
            {
                if (!Directory.Exists($"outputs/{country}"))
                {
                    Directory.CreateDirectory($"outputs/{country}");
                }

                if (data.regions.Keys.Contains(country) && !Directory.Exists($"data/pbf/countries/{country}"))
                {
                    Directory.CreateDirectory($"data/pbf/regions/{country}");
                }

                if (data.regions.Keys.Contains(country) && !Directory.Exists($"data/xml/countries/{country}"))
                {
                    Directory.CreateDirectory($"data/xml/regions/{country}");
                }
            }

            DownloadAndFilteredData(countriesList);
            colour.WriteLine("g","----DANE PRZYGOTOWANE----");
        }



        public async void DownloadAndFilteredData(List<string> countriesList)
        {
            await data.DownloadCountriesByCL(countriesList);
            await data.DownloadRegionsByCL(countriesList);

            data.FilteringCountriesPrimaryByCL(countriesList);
            data.FilteringCountriesSecondaryByCL(countriesList);
            data.FilteringCountriesTertiaryByCL(countriesList);
        }
    }
}