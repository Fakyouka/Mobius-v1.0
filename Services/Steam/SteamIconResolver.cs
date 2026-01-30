using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Mobius.Services.Steam
{
    public static class SteamIconResolver
    {
        public static string TryResolveSquareIcon(string steamRoot, int appId)
        {
            if (string.IsNullOrWhiteSpace(steamRoot) || appId <= 0) return null;

            var cacheDir = Path.Combine(steamRoot, "appcache", "librarycache");
            if (!Directory.Exists(cacheDir)) return null;

            var candidates = new[]
            {
                Path.Combine(cacheDir, $"{appId}_icon.jpg"),
                Path.Combine(cacheDir, $"{appId}_icon.png"),
                Path.Combine(cacheDir, $"{appId}_library_600x900.jpg"),
                Path.Combine(cacheDir, $"{appId}_library_hero.jpg"),
                Path.Combine(cacheDir, $"{appId}_header.jpg"),
            };

            var src = candidates.FirstOrDefault(File.Exists);
            if (src == null) return null;

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "icons");
            Directory.CreateDirectory(outDir);

            var outPath = Path.Combine(outDir, $"{appId}.png");
            if (File.Exists(outPath)) return outPath;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(src, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                int size = Math.Min(bmp.PixelWidth, bmp.PixelHeight);
                int x = (bmp.PixelWidth - size) / 2;
                int y = (bmp.PixelHeight - size) / 2;

                var cropped = new CroppedBitmap(bmp, new System.Windows.Int32Rect(x, y, size, size));
                var scale = new System.Windows.Media.ScaleTransform(128.0 / size, 128.0 / size);
                var scaled = new TransformedBitmap(cropped, scale);
                scaled.Freeze();

                using (var fs = File.Create(outPath))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(scaled));
                    enc.Save(fs);
                }

                return outPath;
            }
            catch
            {
                return src;
            }
        }
    }
}
