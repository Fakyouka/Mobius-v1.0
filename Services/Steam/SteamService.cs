using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Mobius.Services.Steam
{
    public sealed class SteamGameInfo
    {
        public int AppId { get; set; }
        public string Name { get; set; }
        public string InstallDir { get; set; }
        public string SteamAppsPath { get; set; }
    }

    public static class SteamService
    {
        public static string TryGetSteamPath()
        {
            // HKCU\Software\Valve\Steam -> SteamPath
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var v = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
                }
            }
            catch { }

            // HKLM fallback
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    var v = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
                }
            }
            catch { }

            return null;
        }

        public static List<SteamGameInfo> GetInstalledGames()
        {
            var steamPath = TryGetSteamPath();
            if (string.IsNullOrWhiteSpace(steamPath)) return new List<SteamGameInfo>();

            var librarySteamApps = GetSteamAppsFolders(steamPath);

            var result = new List<SteamGameInfo>();
            foreach (var steamApps in librarySteamApps)
            {
                if (!Directory.Exists(steamApps)) continue;

                var manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
                foreach (var mf in manifests)
                {
                    var txt = SafeReadAllText(mf);
                    if (string.IsNullOrWhiteSpace(txt)) continue;

                    var appIdStr = ExtractVdfValue(txt, "appid");
                    var name = ExtractVdfValue(txt, "name");
                    var installDir = ExtractVdfValue(txt, "installdir");

                    if (!int.TryParse(appIdStr, out var appId)) continue;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    result.Add(new SteamGameInfo
                    {
                        AppId = appId,
                        Name = name,
                        InstallDir = string.IsNullOrWhiteSpace(installDir) ? null : Path.Combine(steamApps, "common", installDir),
                        SteamAppsPath = steamApps
                    });
                }
            }

            // иногда бывают дубликаты (если странно настроены библиотеки)
            return result
                .GroupBy(x => x.AppId)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetSteamAppsFolders(string steamPath)
        {
            var folders = new List<string>();

            // default steamapps
            var defaultSteamApps = Path.Combine(steamPath, "steamapps");
            folders.Add(defaultSteamApps);

            // additional libraries from libraryfolders.vdf
            var vdfPath = Path.Combine(defaultSteamApps, "libraryfolders.vdf");
            var vdf = SafeReadAllText(vdfPath);
            if (!string.IsNullOrWhiteSpace(vdf))
            {
                // ищем все "path" "X:\...."
                foreach (var path in ExtractAllVdfPaths(vdf))
                {
                    var p = path.Replace(@"\\", @"\");
                    var steamApps = Path.Combine(p, "steamapps");
                    if (!folders.Contains(steamApps, StringComparer.OrdinalIgnoreCase))
                        folders.Add(steamApps);
                }
            }

            return folders;
        }

        private static IEnumerable<string> ExtractAllVdfPaths(string vdf)
        {
            // очень простой парсер: ищем строки вида "path"  "C:\\SteamLibrary"
            // плюс старый формат: "1" "D:\\SteamLibrary"
            var lines = vdf.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var t = line.Trim();

                // "path" "..."
                if (t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                {
                    var val = ExtractQuotedValue(t);
                    if (!string.IsNullOrWhiteSpace(val)) yield return val;
                }

                // старый формат: "1" "D:\\SteamLibrary"
                if (t.Length > 3 && t.StartsWith("\"") && t.Contains("\" \"") && char.IsDigit(t.TrimStart('"')[0]))
                {
                    var parts = SplitTwoQuoted(t);
                    if (parts != null && parts.Item1.All(char.IsDigit))
                    {
                        yield return parts.Item2;
                    }
                }
            }
        }

        private static string ExtractVdfValue(string text, string key)
        {
            // ищем: "key"  "value"
            var token = "\"" + key + "\"";
            var idx = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            idx = text.IndexOf('"', idx + token.Length);
            if (idx < 0) return null;

            // после ключа идёт "value"
            // найдём следующую кавычку
            idx = text.IndexOf('"', idx + 1);
            if (idx < 0) return null;

            var start = idx + 1;
            var end = text.IndexOf('"', start);
            if (end < 0) return null;

            return text.Substring(start, end - start);
        }

        private static string ExtractQuotedValue(string line)
        {
            // line: "path" "C:\\SteamLibrary"
            var parts = SplitTwoQuoted(line);
            return parts?.Item2;
        }

        private static Tuple<string, string> SplitTwoQuoted(string line)
        {
            // returns (key, value)
            var first1 = line.IndexOf('"');
            if (first1 < 0) return null;
            var first2 = line.IndexOf('"', first1 + 1);
            if (first2 < 0) return null;

            var second1 = line.IndexOf('"', first2 + 1);
            if (second1 < 0) return null;
            var second2 = line.IndexOf('"', second1 + 1);
            if (second2 < 0) return null;

            var k = line.Substring(first1 + 1, first2 - first1 - 1);
            var v = line.Substring(second1 + 1, second2 - second1 - 1);
            return Tuple.Create(k, v);
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path);
            }
            catch { return null; }
        }
    }
}
