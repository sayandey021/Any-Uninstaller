using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UninstallTools;
using UninstallTools.Uninstaller;

namespace AnyUninstaller.Avalonia.Services
{
    public class UninstallerExecutionService
    {
        public static readonly UninstallerExecutionService Instance = new();

        public BulkUninstallTask CreateBulkTask(
            IEnumerable<ApplicationUninstallerEntry> targets,
            bool quiet,
            bool simulate = false)
        {
            var config = new BulkUninstallConfiguration(
                ignoreProtection: false,
                preferQuiet: quiet,
                simulate: simulate,
                autoKillStuckQuiet: AppSettingsService.Instance.AutoKillStuckProcesses,
                retryFailedQuiet: false
            );

            var bulkEntries = targets.Select(e =>
            {
                var status = UninstallStatus.Waiting;
                if (!e.IsValid)
                    status = UninstallStatus.Invalid;
                else if (e.IsProtected)
                    status = UninstallStatus.Protected;

                bool silentPossible = quiet && e.QuietUninstallPossible;
                return new BulkUninstallEntry(e, silentPossible, status);
            }).ToList();

            return UninstallManager.CreateBulkUninstallTask(bulkEntries, config);
        }
    }
}
