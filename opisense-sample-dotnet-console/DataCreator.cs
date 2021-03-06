using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using opisense_sample_dotnet_console.Model;

namespace opisense_sample_dotnet_console
{
    public class DataCreator
    {
        private static readonly string OpisensePush = ConfigurationManager.AppSettings["OpisensePush"];
        private static readonly string OpisenseApi = ConfigurationManager.AppSettings["OpisenseApi"];
        private static readonly string DefaultTimezone = ConfigurationManager.AppSettings["DefaultTimezone"];
        private readonly VariableSelector variableSelector;
        private readonly Authenticator authenticator;

        public DataCreator(VariableSelector variableSelector, Authenticator authenticator)
        {
            this.variableSelector = variableSelector;
            this.authenticator = authenticator;
        }

        public async Task DemoSetup()
        {
            using (var client = await authenticator.GetAuthenticatedClient())
            {
                Console.WriteLine("Creating 3 sites in Opisense");
                

                Console.WriteLine("Creating site1");
                var siteId1 = await CreateSite(client, "site1");
                Console.WriteLine("Creating site2");
                var siteId2 = await CreateSite(client, "site2");
                Console.WriteLine("Creating site3");
                var siteId3 = await CreateSite(client, "site3");
                
                Console.WriteLine("Creating source1 and variables");
                var source1 = await CreateSource(client, siteId1, "source1");
                // Add 
                var source1Variable1 = await CreateVariable(client, source1.Id, 0);
                // Ajout de la variable d'index
                var source1Variable2 = await CreateVariable(client, source1.Id, 25);

                Console.WriteLine("Creating source2 and variables");
                var source2 = await CreateSource(client, siteId1, "source2");
                // Ajout de la variable de consommation
                var source2Variable1 = await CreateVariable(client, source2.Id, 0);
                // Ajout de la variable d'index
                var source2Variable2 = await CreateVariable(client, source2.Id, 25);

                Console.WriteLine("Creating source3 and variables");
                var source3 = await CreateSource(client, siteId2, "source3");
                // Ajout de la variable de consommation
                var source3Variable1 = await CreateVariable(client, source3.Id, 0);
                // Ajout de la variable d'index
                var source3Variable2 = await CreateVariable(client, source3.Id, 25);

                Console.WriteLine("Creating source4 and variables");
                var source4 = await CreateSource(client, siteId2, "source4");
                // Ajout de la variable de consommation
                var source4Variable1 = await CreateVariable(client, source4.Id, 0);
                // Ajout de la variable d'index
                var source4Variable2 = await CreateVariable(client, source4.Id, 25);

                Console.WriteLine("Creating source5 and variables");
                var source5 = await CreateSource(client, siteId3, "source5");
                // Ajout de la variable de consommation
                var source5Variable1 = await CreateVariable(client, source5.Id, 0);
                // Ajout de la variable d'index
                var source5Variable2 = await CreateVariable(client, source5.Id, 25);


                //    Ajouter dans chaque source pr�c�demment cr�� 2 variables(type double et type int) avec 2 ans d'historique des valeurs au points de 10mins (=6*24*365*2 points), sauf pour le site3 ou il faut avoir un "trou" pour tous les 10 valeurs

                Console.WriteLine("Adding data in source 1");
                await CreateData(client, source1Variable1, source1Variable2);
                Console.WriteLine("Adding data in source 2");
                await CreateData(client, source2Variable1, source2Variable2);
                Console.WriteLine("Adding data in source 3");
                await CreateData(client, source3Variable1, source3Variable2);
                Console.WriteLine("Adding data in source 4");
                await CreateData(client, source4Variable1, source4Variable2);
                Console.WriteLine("Adding data in source 5");
                await CreateData(client, source5Variable1, source5Variable2, holesEveryXDatapoint: 10);


                // ATTENTION: LORS DE L'INGESTION, LES DONNEES PASSENT PAS PLUSIEURS ETAPES (ENRICHISSEMENT, VARIABLE CALCULEES, ALERTES)
                // IL Y A DONC UN DELAIS POUVANT ALLER JUSQU'A 10 MINUTES AVANT QU'ELLES SOIENT ACCESSIBLE VIA L'API

                //    Pour le site1 source1 dupliquer tous les valeurs de variable1 en variable3
                //    Supprimer le site2
            }
        }

        public async Task UpdateData()
        {
            //Pour le site1 source1 variable1 mettre � jour un an des valeurs
            //    Pour le site1 source1 dupliquer tous les valeurs de variable1 en variable3
            using (var client = await authenticator.GetAuthenticatedClient())
            {
                var variable = await variableSelector.SelectVariable(client);
                Console.WriteLine($"Generating data for variable id {variable.Id}");
                var data = GenerateData(DateTime.Today.AddYears(-1), DateTime.Today, TimeSpan.FromMinutes(10), 100);
                await AddOrUpdateData(client, variable.Id, data);
            }
        }

        private async Task CreateData(HttpClient client, Variable variable1, Variable variable2, int? holesEveryXDatapoint = null)
        {
            Console.WriteLine($"Generating data for variable id {variable1.Id} and {variable2.Id}");
            var data = GenerateData(DateTime.Today.AddYears(-1), DateTime.Today, TimeSpan.FromMinutes(10), 100, holesEveryXDatapoint);
            await AddOrUpdateData(client, variable1.Id, data);
            var incrementalData = FakeIndexes(data);
            await AddOrUpdateData(client, variable2.Id, incrementalData);
        }

        private List<Data> FakeIndexes(List<Data> data)
        {
            var result = new List<Data>();
            Data previousValue = null;
            foreach (var value in data)
            {
                if (previousValue == null)
                    previousValue = new Data { Date = value.Date, Value = 0 };
                else
                    previousValue = new Data { Date = value.Date, Value = previousValue.Value + value.Value };
                result.Add(previousValue);
            }
            return result;
        }

        private List<Data> GenerateData(DateTime from, DateTime today, TimeSpan period, int maxValue, int? holesEveryXDatapoint = null)
        {
            var result = new List<Data>();
            var rand = new Random();

            for (var date = from; date < today; date = date + period)
            {
                result.Add(new Data { Date = date, Value = rand.NextDouble() * maxValue });
            }

            return holesEveryXDatapoint == null
                ? result
                : result.Where((r, i) => i % holesEveryXDatapoint != 0).ToList();
        }

        private async Task AddOrUpdateData(HttpClient client, int variableId, List<Data> data)
        {
            Console.WriteLine($"Importing {data.Count} datapoints for variable id {variableId}");
            var response = await client.PostAsJsonAsync($"{OpisensePush}standard", new
            {
                Data = data.Select(d => new { VariableId = variableId, Date = d.Date, Value = d.Value }).ToList()
            });
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"Finished importing data for variable id {variableId}");
        }

        private static async Task<Variable> CreateVariable(HttpClient client, int sourceId, int variableTypeId)
        {
            var response = await client.PostAsJsonAsync($"{OpisenseApi}variables/source/{sourceId}", new Variable
            {
                VariableTypeId = variableTypeId,
                UnitId = 8,
                Divider = 1,
                Granularity = 10,
                GranularityTimeBase = TimePeriod.Minute
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<Variable>();
        }

        private static async Task<Source> CreateSource(HttpClient client, int siteId, string sourceName)
        {
            var response = await client.PostAsJsonAsync($"{OpisenseApi}sources", new
            {
                Name = sourceName,
                SiteId = siteId,
                TimeZoneId = DefaultTimezone,
                SourceTypeId = 72,
                EnergyTypeId = 1
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<Source>();
        }

        private static async Task<int> CreateSite(HttpClient client, string siteName)
        {
            //TypeID can be retrieved from GET /sitetypes
            //TimeZoneId can be retrieved from GET /timezones
            var response = await client.PostAsJsonAsync($"{OpisenseApi}sites", new { Name = siteName, TypeId = 1, TimeZoneId = DefaultTimezone, City = "Mont-Saint-Guibert", Country = "Belgium", Street = "Rue Emile Francqui, 6", PostalCode = "1435" });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<int>();
        }
    }
}