using Mobius.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Mobius.Services.Steam
{
    public class SteamService
    {
        public IEnumerable<AppModel> GetInstalledGames()
        {
            var steamPath = TryGetSteamPath();
            Debug.WriteLine($"[STEAM] steamPath resolved = {steamPath}");

            if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
                yield break;

            foreach (var lib in EnumerateLibrarySteamAppsFolders(steamPath))
            {
                var steamApps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamApps))
                    continue;

                foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                {
                    string txt;
                    try { txt = File.ReadAllText(manifest); }
                    catch { continue; }

                    var appIdStr = ExtractVdfValue(txt, "appid");
                    var name = ExtractVdfValue(txt, "name");
                    var installDir = ExtractVdfValue(txt, "installdir");

                    Debug.WriteLine($"[STEAM] manifest: appid={appIdStr}, name={name}");

                    if (string.IsNullOrWhiteSpace(appIdStr) || string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!int.TryParse(appIdStr, out var appId))
                        continue;

                    string fullInstallDir = null;
                    if (!string.IsNullOrWhiteSpace(installDir))
                    {
                        var common = Path.Combine(steamApps, "common", installDir);
                        if (Directory.Exists(common))
                            fullInstallDir = common;
                    }

                    yield return new AppModel
                    {
                        Name = name,
                        SourceType = AppSourceType.Steam,
                        SourceId = appId.ToString(),
                        SourceText = $"steam:{appId}",
                        InstallDir = fullInstallDir,
                        ExePath = null, // для Steam не обязателен
                        IconPath = null
                    };
                }
            }
        }

        public string TryGetSteamPath()
        {
            // 1) реестр (частый вариант)
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var p = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                    return p.Replace('/', '\\');
            }
            catch { /* ignore */ }

            // 2) альтернативный реестр
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                var p = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                    return p.Replace('/', '\\');
            }
            catch { /* ignore */ }

            // 3) стандартные пути
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
            };

            foreach (var c in candidates)
            {
                if (Directory.Exists(c))
                    return c;
            }

            return null;
        }

        private IEnumerable<string> EnumerateLibrarySteamAppsFolders(string steamPath)
        {
            // Основная библиотека (SteamPath)
            yield return steamPath;

            // Дополнительные библиотеки из libraryfolders.vdf
            var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                yield break;

            string txt;
            try { txt = File.ReadAllText(vdf); }
            catch { yield break; }

            // Новый формат: "path" "D:\\SteamLibrary"
            // Старый формат: "1" "D:\\SteamLibrary"
            var matches = Regex.Matches(txt, "\"path\"\\s*\"(?<p>[^\"]+)\"|\"\\d+\"\\s*\"(?<p>[^\"]+)\"");
            foreach (Match m in matches)
            {
                var p = m.Groups["p"].Value;
                if (string.IsNullOrWhiteSpace(p)) continue;

                p = p.Replace("\\\\", "\\").Replace('/', '\\');
                if (Directory.Exists(p))
                    yield return p;
            }
        }

        // ВАЖНО: корректный парсер для строк "key"  "value"
        private static string ExtractVdfValue(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
                return null;

            var pattern = $"\"{key}\"\\s*\"(?<val>[^\"]*)\"";
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["val"].Value : null;
        }
    }
}
