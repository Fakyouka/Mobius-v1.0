using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Mobius.Models;

namespace Mobius.Services.Storage
{
    public class ConfigService
    {
        private const string FileName = "mobius_config.json";

        public List<AppEntryModel> LoadOrDefault()
        {
            if (!File.Exists(FileName))
                return new List<AppEntryModel>();

            using (var fs = File.OpenRead(FileName))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<AppEntryModel>));
                return (List<AppEntryModel>)serializer.ReadObject(fs);
            }
        }

        public void Save(IEnumerable<AppEntryModel> apps)
        {
            using (var fs = File.Create(FileName))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<AppEntryModel>));
                serializer.WriteObject(fs, new List<AppEntryModel>(apps));
            }
        }
    }
}
