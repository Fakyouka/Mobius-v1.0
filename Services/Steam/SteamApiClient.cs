using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mobius.Services.Steam
{
    public class SteamApiClient
    {
        private static readonly HttpClient _http = new HttpClient();

        public async Task<string> DownloadIconAsync(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return null;

            try
            {
                // Абсолютный путь, чтобы WPF точно находил картинку
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconsDir = Path.Combine(baseDir, "cache", "icons");
                Directory.CreateDirectory(iconsDir);

                var outPath = Path.Combine(iconsDir, $"{appId}.jpg");
                if (File.Exists(outPath))
                    return outPath;

                // Простой CDN-фоллбек
                // (в будущем можно подтянуть точную иконку через appdetails API,
                // но это уже сетевой парсинг JSON — пока хватит этого)
                var url = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";

                var bytes = await _http.GetByteArrayAsync(url);
                if (bytes == null || bytes.Length == 0)
                    return null;

                await File.WriteAllBytesAsync(outPath, bytes);
                return outPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[STEAM] DownloadIconAsync ERROR: " + ex);
                return null;
            }
        }
    }
}
