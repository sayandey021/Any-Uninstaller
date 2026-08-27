using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UninstallTools;
using UninstallTools.Junk;
using UninstallTools.Junk.Containers;

namespace AnyUninstaller.Avalonia.Services
{
    public class JunkCleaningService
    {
        public static readonly JunkCleaningService Instance = new();

        public async Task<List<IJunkResult>> ScanJunkAsync(
            IEnumerable<ApplicationUninstallerEntry> targets,
            ICollection<ApplicationUninstallerEntry> allUninstallers,
            IProgress<(int current, int total, string message)>? progress = null)
        {
            return await Task.Run(() =>
            {
                var targetList = targets.ToList();
                var results = JunkManager.FindJunk(targetList, allUninstallers, p =>
                {
                    progress?.Report((p.CurrentCount, p.TotalCount, p.Message));
                });

                return results.ToList();
            });
        }
    }
}
