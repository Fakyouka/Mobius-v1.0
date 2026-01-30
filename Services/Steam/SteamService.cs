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
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var v = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
                }
            }
            catch { }

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

            var steamAppsFolders = GetSteamAppsFolders(steamPath);
            var result = new List<SteamGameInfo>();

            foreach (var steamApps in steamAppsFolders)
            {
                if (!Directory.Exists(steamApps)) continue;

                foreach (var mf in Directory.GetFiles(steamApps, "appmanifest_*.acf"))
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

            return result
                .GroupBy(x => x.AppId)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetSteamAppsFolders(string steamPath)
        {
            var folders = new List<string>();

            var defaultSteamApps = Path.Combine(steamPath, "steamapps");
            folders.Add(defaultSteamApps);

            var vdfPath = Path.Combine(defaultSteamApps, "libraryfolders.vdf");
            var vdf = SafeReadAllText(vdfPath);

            if (!string.IsNullOrWhiteSpace(vdf))
            {
                foreach (var p in ExtractAllVdfPaths(vdf))
                {
                    var steamApps = Path.Combine(p.Replace(@"\\", @"\"), "steamapps");
                    if (!folders.Contains(steamApps, StringComparer.OrdinalIgnoreCase))
                        folders.Add(steamApps);
                }
            }

            return folders;
        }

        private static IEnumerable<string> ExtractAllVdfPaths(string vdf)
        {
            var lines = vdf.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var t = line.Trim();

                if (t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                {
                    var val = ExtractSecondQuoted(t);
                    if (!string.IsNullOrWhiteSpace(val)) yield return val;
                }

                if (t.StartsWith("\"") && t.Contains("\" \""))
                {
                    var parts = SplitTwoQuoted(t);
                    if (parts != null && parts.Item1.All(char.IsDigit))
                        yield return parts.Item2;
                }
            }
        }

        private static string ExtractVdfValue(string text, string key)
        {
            var token = "\"" + key + "\"";
            var idx = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            // find first quote after token
            idx = text.IndexOf('"', idx + token.Length);
            if (idx < 0) return null;

            // next quote begins value
            idx = text.IndexOf('"', idx + 1);
            if (idx < 0) return null;

            int start = idx + 1;
            int end = text.IndexOf('"', start);
            if (end < 0) return null;

            return text.Substring(start, end - start);
        }

        private static string ExtractSecondQuoted(string line)
        {
            var parts = SplitTwoQuoted(line);
            return parts?.Item2;
        }

        private static Tuple<string, string> SplitTwoQuoted(string line)
        {
            int a1 = line.IndexOf('"'); if (a1 < 0) return null;
            int a2 = line.IndexOf('"', a1 + 1); if (a2 < 0) return null;

            int b1 = line.IndexOf('"', a2 + 1); if (b1 < 0) return null;
            int b2 = line.IndexOf('"', b1 + 1); if (b2 < 0) return null;

            var k = line.Substring(a1 + 1, a2 - a1 - 1);
            var v = line.Substring(b1 + 1, b2 - b1 - 1);
            return Tuple.Create(k, v);
        }

        private static string SafeReadAllText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }
    }
}
