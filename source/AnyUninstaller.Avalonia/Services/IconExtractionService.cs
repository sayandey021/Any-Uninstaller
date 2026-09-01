using System;
using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using UninstallTools;

namespace AnyUninstaller.Avalonia.Services
{
    public class IconExtractionService
    {
        public static readonly IconExtractionService Instance = new();

        private readonly ConcurrentDictionary<string, Bitmap> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        private static Bitmap? _defaultAppIcon;
        private static Bitmap? _defaultInvalidIcon;
        private static readonly object _lock = new();

        public static Bitmap DefaultApplicationIcon
        {
            get
            {
                if (_defaultAppIcon == null)
                {
                    lock (_lock)
                    {
                        if (_defaultAppIcon == null)
                        {
                            try
                            {
                                using var icon = System.Drawing.SystemIcons.Application;
                                using var bmp = icon.ToBitmap();
                                using var ms = new MemoryStream();
                                bmp.Save(ms, ImageFormat.Png);
                                ms.Position = 0;
                                _defaultAppIcon = new Bitmap(ms);
                            }
                            catch
                            {
                                using var ms = new MemoryStream();
                                using var bmp = new System.Drawing.Bitmap(16, 16);
                                bmp.Save(ms, ImageFormat.Png);
                                ms.Position = 0;
                                _defaultAppIcon = new Bitmap(ms);
                            }
                        }
                    }
                }
                return _defaultAppIcon;
            }
        }

        public static Bitmap DefaultInvalidIcon
        {
            get
            {
                if (_defaultInvalidIcon == null)
                {
                    lock (_lock)
                    {
                        if (_defaultInvalidIcon == null)
                        {
                            try
                            {
                                using var icon = System.Drawing.SystemIcons.Exclamation;
                                using var bmp = icon.ToBitmap();
                                using var ms = new MemoryStream();
                                bmp.Save(ms, ImageFormat.Png);
                                ms.Position = 0;
                                _defaultInvalidIcon = new Bitmap(ms);
                            }
                            catch
                            {
                                _defaultInvalidIcon = DefaultApplicationIcon;
                            }
                        }
                    }
                }
                return _defaultInvalidIcon ?? DefaultApplicationIcon;
            }
        }

        public bool TryGetCachedIcon(ApplicationUninstallerEntry entry, out Bitmap? icon)
        {
            icon = null;
            if (entry == null) return false;

            var cacheKey = !string.IsNullOrEmpty(entry.DisplayName)
                ? entry.DisplayName
                : (entry.CacheIdOverride ?? entry.Comment ?? string.Empty);

            if (!string.IsNullOrEmpty(cacheKey) && _iconCache.TryGetValue(cacheKey, out var cached))
            {
                icon = cached;
                return true;
            }

            return false;
        }

        public async Task<Bitmap> GetIconAsync(ApplicationUninstallerEntry entry)
        {
            if (entry == null)
                return DefaultApplicationIcon;

            if (TryGetCachedIcon(entry, out var cached) && cached != null)
                return cached;

            return await Task.Run(() => GetIcon(entry));
        }

        public Bitmap GetIcon(ApplicationUninstallerEntry entry)
        {
            if (entry == null)
                return DefaultApplicationIcon;

            var cacheKey = !string.IsNullOrEmpty(entry.DisplayName)
                ? entry.DisplayName
                : (entry.CacheIdOverride ?? entry.Comment ?? Guid.NewGuid().ToString());

            if (_iconCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                using var icon = entry.GetIcon();
                if (icon != null)
                {
                    using var ms = new MemoryStream();
                    using var bmp = icon.ToBitmap();
                    bmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    var avaloniaBitmap = new Bitmap(ms);
                    _iconCache[cacheKey] = avaloniaBitmap;
                    return avaloniaBitmap;
                }
            }
            catch
            {
                // Fallback to default icon
            }

            var fallback = GetFallbackIcon(entry);
            _iconCache[cacheKey] = fallback;
            return fallback;
        }

        public static Bitmap GetFallbackIcon(ApplicationUninstallerEntry entry)
        {
            if (entry != null && !entry.IsValid)
                return DefaultInvalidIcon;

            return DefaultApplicationIcon;
        }

        public void ClearCache()
        {
            _iconCache.Clear();
        }
    }
}
