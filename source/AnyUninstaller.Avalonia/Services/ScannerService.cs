using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UninstallTools;
using UninstallTools.Factory;

namespace AnyUninstaller.Avalonia.Services
{
    public class ScannerService
    {
        public static readonly ScannerService Instance = new();

        static ScannerService()
        {
            // Set base scanner defaults
            UninstallToolsGlobalConfig.ScanRegistry = true;
            UninstallToolsGlobalConfig.ScanPreDefined = true;
            UninstallToolsGlobalConfig.ScanSteam = true;
            UninstallToolsGlobalConfig.ScanOculus = true;
            UninstallToolsGlobalConfig.ScanWinFeatures = true;
            UninstallToolsGlobalConfig.ScanChocolatey = true;
            UninstallToolsGlobalConfig.ScanScoop = true;
            UninstallToolsGlobalConfig.AutoDetectScanRemovable = true;

            // Sync user preferences
            UninstallToolsGlobalConfig.ScanDrives = AppSettingsService.Instance.ScanDrives;
            UninstallToolsGlobalConfig.ScanStoreApps = AppSettingsService.Instance.ScanStoreApps;
            UninstallToolsGlobalConfig.ScanWinUpdates = AppSettingsService.Instance.ScanWindowsUpdates;
            UninstallToolsGlobalConfig.AutoDetectCustomProgramFiles = AppSettingsService.Instance.AutoDetectCustomProgramFiles;
        }

        public async Task<List<ApplicationUninstallerEntry>> ScanApplicationsAsync(
            IProgress<(int current, int total, string message)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Ensure settings are synced before starting scan
            UninstallToolsGlobalConfig.ScanDrives = AppSettingsService.Instance.ScanDrives;
            UninstallToolsGlobalConfig.ScanStoreApps = AppSettingsService.Instance.ScanStoreApps;
            UninstallToolsGlobalConfig.ScanWinUpdates = AppSettingsService.Instance.ScanWindowsUpdates;
            UninstallToolsGlobalConfig.AutoDetectCustomProgramFiles = AppSettingsService.Instance.AutoDetectCustomProgramFiles;

            return await Task.Run(() =>
            {
                var entries = ApplicationUninstallerFactory.GetUninstallerEntries(p =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var msg = p.Message;
                    if (p.Inner != null && !string.IsNullOrEmpty(p.Inner.Message))
                    {
                        msg = $"{p.Message}: {p.Inner.Message}";
                    }
                    progress?.Report((p.CurrentCount, p.TotalCount, msg));
                });

                return entries.ToList();
            }, cancellationToken);
        }
    }
}
