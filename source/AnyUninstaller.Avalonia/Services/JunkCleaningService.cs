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
                long lastReportTicks = 0;
                int lastStep = -1;
                const int throttleMs = 35;

                var targetList = targets.ToList();
                var results = JunkManager.FindJunk(targetList, allUninstallers, p =>
                {
                    long now = Environment.TickCount64;
                    bool isStepChange = p.CurrentCount != lastStep;
                    bool isComplete = p.CurrentCount == p.TotalCount;
                    bool shouldReport = isStepChange || isComplete || (now - lastReportTicks >= throttleMs);

                    if (shouldReport && progress != null)
                    {
                        lastReportTicks = now;
                        lastStep = p.CurrentCount;
                        progress.Report((p.CurrentCount, p.TotalCount, p.Message));
                    }
                });

                var resultList = results.ToList();
                progress?.Report((resultList.Count, resultList.Count, $"Found {resultList.Count} junk items."));
                return resultList;
            });
        }
    }
}
