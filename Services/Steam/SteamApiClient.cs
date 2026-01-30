using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Mobius.Services.Steam
{
    public class SteamApiClient
    {
        private static readonly HttpClient Http = new HttpClient();
        private const string CacheFolder = "cache";

        public SteamApiClient()
        {
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);
        }

        public List<SteamGameResult> Search(string query)
        {
            var url = "https://store.steampowered.com/api/storesearch/?term="
                      + Uri.EscapeDataString(query)
                      + "&l=english&cc=us";

            var json = Http.GetStringAsync(url).Result;
            var root = JObject.Parse(json);

            var results = new List<SteamGameResult>();
            var items = root["items"];
            if (items == null) return results;

            foreach (var item in items)
            {
                int appId = item.Value<int>("id");
                string name = item.Value<string>("name");
                string icon = DownloadIcon(appId);

                results.Add(new SteamGameResult
                {
                    AppId = appId,
                    Name = name,
                    IconPath = icon
                });
            }

            return results;
        }

        private string DownloadIcon(int appId)
        {
            var path = Path.Combine(CacheFolder, appId + ".jpg");
            if (File.Exists(path))
                return path;

            string square = "https://cdn.cloudflare.steamstatic.com/steam/apps/" + appId + "/capsule_184x184.jpg";
            string header = "https://cdn.cloudflare.steamstatic.com/steam/apps/" + appId + "/header.jpg";

            try
            {
                var bytes = Http.GetByteArrayAsync(square).Result;
                File.WriteAllBytes(path, bytes);
                return path;
            }
            catch
            {
                try
                {
                    var bytes = Http.GetByteArrayAsync(header).Result;
                    File.WriteAllBytes(path, bytes);
                    return path;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public class SteamGameResult
    {
        public int AppId { get; set; }
        public string Name { get; set; }
        public string IconPath { get; set; }
    }
}
