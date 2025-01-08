namespace Evolution
{
    class MakeTests
    {
        Colour colour = new Colour();
        public MakeTests()
        {
            List<string> countriesList = new List<string>() {
                "austria",
                "belgium",
                "czech-republic",
                "germany",
                "hungary",
                "luxembourg",
                "netherlands",
                "poland",
                "slovakia",
                "switzerland|liechtenstein"
            };

            List<List<float>> valuesList = new List<List<float>>() {
                new List<float>() { 1000, 500, 10, 0.4f, 0.8f, 0.6f, 0.2f, 0.3f, 0.25f, 0.25f, 25 },
                new List<float>() { 1000, 500, 20, 0.7f, 0.5f, 0.3f, 0.4f, 0.15f, 0.2f, 0.25f, 35 },
                new List<float>() { 1000, 1000, 5, 0.3f, 0.6f, 0.5f, 0.1f, 0.5f, 0.2f, 0.2f, 20 },
                new List<float>() { 1000, 1000, 10, 0.5f, 0.7f, 0.4f, 0.3f, 0.25f, 0.25f, 0.2f, 30 },
                new List<float>() { 1000, 1000, 20, 0.2f, 0.3f, 0.8f, 0.05f, 0.4f, 0.35f, 0.2f, 30 },

                new List<float>() { 2000, 500, 10, 0.6f, 0.9f, 0.1f, 0.3f, 0.3f, 0.25f, 0.15f, 40 },
                new List<float>() { 2000, 500, 20, 0.9f, 0.4f, 0.5f, 0.15f, 0.35f, 0.25f, 0.25f, 25 },
                new List<float>() { 2000, 1000, 5,  0.1f, 0.7f, 0.9f, 0.25f, 0.25f, 0.25f, 0.25f, 25 },
                new List<float>() { 2000, 1000, 10, 0.8f, 0.6f, 0.2f, 0.35f, 0.2f, 0.3f, 0.15f, 40 },
                new List<float>() { 2000, 1000, 20, 0.4f, 0.3f, 0.7f, 0.5f, 0.15f, 0.2f, 0.15f, 35 },

                new List<float>() { 3000, 500, 10, 0.3f, 0.2f, 0.6f, 0.1f, 0.5f, 0.2f, 0.2f, 20 },
                new List<float>() { 3000, 500, 20, 0.7f, 0.5f, 0.9f, 0.4f, 0.1f, 0.25f, 0.25f, 35 },
                new List<float>() { 3000, 1000, 5,  0.5f, 0.4f, 0.3f, 0.15f, 0.35f, 0.35f, 0.15f, 30 },
                new List<float>() { 3000, 1000, 10, 0.2f, 0.1f, 0.4f, 0.25f, 0.25f, 0.3f, 0.2f, 30 },
                new List<float>() { 3000, 1000, 20, 0.9f, 0.8f, 0.1f, 0.3f, 0.15f, 0.25f, 0.3f, 30 },
                
                new List<float>() { 4000, 500, 10, 0.1f, 0.5f, 0.3f, 0.05f, 0.4f, 0.3f, 0.25f, 25 },
                new List<float>() { 4000, 500, 20, 0.7f, 0.2f, 0.6f, 0.35f, 0.25f, 0.25f, 0.15f, 35 },
                new List<float>() { 4000, 1000, 5, 0.4f, 0.1f, 0.7f, 0.2f, 0.3f, 0.3f, 0.2f, 35 },
                new List<float>() { 4000, 1000, 10, 0.6f, 0.5f, 0.2f, 0.25f, 0.3f, 0.3f, 0.15f, 25 },
                new List<float>() { 4000, 1000, 20, 0.3f, 0.7f, 0.8f, 0.4f, 0.2f, 0.25f, 0.15f, 40 }
            };

            foreach (var values in valuesList)
            {
                int numberPopulation = (int) values[0];
                int numberEpochs = (int) values[1];
                int percentLeft = (int) values[2];

                double mutationSwapPower = values[3];
                double mutationScramblePower = values[4];
                double crossoverSwapPower = values[5];

                double mutationSwapFreq = values[6];
                double mutationScrambleFreq = values[7];
                double crossoverSwapFreq = values[8];
                double crossoverLinearOrderFreq = values[9];

                int packagesPerDriver = (int) values[10];

                if (values[6] + values[7] + values[8] + values[9] != 1)
                {
                    colour.WriteLine("r","ERROR - The value of the sum of frequencies must be equal to 1");
                    return;
                }

                Algorithm algorithm = new Algorithm(countriesList, "Test4kP10C", numberPopulation, numberEpochs, percentLeft, 
                    new List<double>() {mutationSwapPower, mutationScramblePower, crossoverSwapPower}, 
                    new List<double>() {mutationSwapFreq, mutationScrambleFreq, crossoverSwapFreq, crossoverLinearOrderFreq},
                    packagesPerDriver, 
                    false, false, "warehouses", 9, 7);
            }

            foreach (var values in valuesList)
            {
                int numberPopulation = (int) values[0];
                int numberEpochs = (int) values[1];
                int percentLeft = (int) values[2];

                double mutationSwapPower = values[3];
                double mutationScramblePower = values[4];
                double crossoverSwapPower = values[5];

                double mutationSwapFreq = values[6];
                double mutationScrambleFreq = values[7];
                double crossoverSwapFreq = values[8];
                double crossoverLinearOrderFreq = values[9];

                int packagesPerDriver = (int) values[10];

                if (values[6] + values[7] + values[8] + values[9] != 1)
                {
                    colour.WriteLine("r","ERROR - The value of the sum of frequencies must be equal to 1");
                    return;
                }

                Algorithm algorithm = new Algorithm(countriesList, "Test3kP10C", numberPopulation, numberEpochs, percentLeft, 
                    new List<double>() {mutationSwapPower, mutationScramblePower, crossoverSwapPower}, 
                    new List<double>() {mutationSwapFreq, mutationScrambleFreq, crossoverSwapFreq, crossoverLinearOrderFreq},
                    packagesPerDriver, 
                    false, false, "warehouses", 9, 7);
            }
        }
    }
}