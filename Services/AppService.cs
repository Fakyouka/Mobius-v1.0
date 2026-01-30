using System;
using System.IO;
using Mobius.Services.Steam;
using Mobius.Services.Storage;
using Mobius.Services.Voice;

namespace Mobius.Services
{
    public sealed class AppServices : IDisposable
    {
        public AppServices()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            Config = new ConfigService();

            var cacheDir = Path.Combine(baseDir, "cache");
            Steam = new SteamApiClient();

            Voice = new VoskVoiceService
            {
                ModelPath = Path.Combine(baseDir, "vosk-model"),
                DeviceNumber = 0
            };
        }

        public ConfigService Config { get; }
        public SteamApiClient Steam { get; }
        public VoskVoiceService Voice { get; }

        public void Dispose()
        {
            try { Voice?.Dispose(); } catch { /* ignore */ }
        }
    }
}
