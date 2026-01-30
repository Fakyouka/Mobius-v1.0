using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Mobius.Models;

namespace Mobius.Services.Storage
{
    public sealed class ConfigService
    {
        private readonly string _configPath;

        public ConfigService()
        {
            _configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "mobius.config.json"
            );
        }
        public List<AppEntryModel> LoadApps()
        {
            if (!File.Exists(_configPath))
                return new List<AppEntryModel>();

            try
            {
                var json = File.ReadAllText(_configPath);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<AppEntryModel>();

                var apps = JsonConvert.DeserializeObject<List<AppEntryModel>>(json);
                return apps ?? new List<AppEntryModel>();
            }
            catch
            {
                return new List<AppEntryModel>();
            }
        }

        public void SaveApps(IEnumerable<AppEntryModel> apps)
        {
            var tmp = _configPath + ".tmp";

            var json = JsonConvert.SerializeObject(
                apps ?? new List<AppEntryModel>(),
                Formatting.Indented
            );

            File.WriteAllText(tmp, json);

            if (File.Exists(_configPath))
                File.Delete(_configPath);

            File.Move(tmp, _configPath);
        }
    }
}
