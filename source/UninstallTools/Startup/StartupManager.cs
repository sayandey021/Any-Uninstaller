using System;
using System.Collections.Generic;
using System.Linq;
using Klocman.Tools;
using Microsoft.Win32.TaskScheduler;
using UninstallTools.Properties;
using UninstallTools.Startup.Browser;
using UninstallTools.Startup.Normal;
using UninstallTools.Startup.Service;
using UninstallTools.Startup.Task;

namespace UninstallTools.Startup
{
    public static class StartupManager
    {
        static StartupManager()
        {
            Factories = new Dictionary<string, Func<IEnumerable<StartupEntryBase>>>
            {
                { Localisation.StartupEntries, StartupEntryFactory.GetStartupItems },
                { Localisation.Startup_ShortName_Task, TaskEntryFactory.GetTaskStartupEntries },
                { Localisation.Startup_Shortname_BrowserHelper, BrowserEntryFactory.GetBrowserHelpers },
                { Localisation.Startup_ShortName_Service, ServiceEntryFactory.GetServiceEntries }
            };
        }

        public static Dictionary<string, Func<IEnumerable<StartupEntryBase>>> Factories { get; }

        /// <summary>
        /// Fill in the ApplicationUninstallerEntry.StartupEntries property with a list of related StartupEntry objects.
        /// Old data is not cleared, only overwritten if necessary.
        /// </summary>
        /// <param name="allUninstallerEntries">Uninstaller entries to assign to</param>
        /// <param name="allStartupEntries">Startup entries to assign</param>
        public static void AssignStartupEntries(IEnumerable<ApplicationUninstallerEntry> allUninstallerEntries,
            IEnumerable<StartupEntryBase> allStartupEntries)
        {
            var startups = allStartupEntries.ToList();
            var uninstallers = allUninstallerEntries.ToList();

            if (startups.Count == 0 || uninstallers.Count == 0)
                return;

            var validInstallLocations = uninstallers
                .Where(e => e.IsInstallLocationValid())
                .Select(e => e.InstallLocation)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validUninLocations = uninstallers
                .Where(e => !string.IsNullOrEmpty(e.UninstallerLocation))
                .Select(e => e.UninstallerLocation)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var uninstaller in uninstallers)
            {
                var instLoc = uninstaller.IsInstallLocationValid() ? uninstaller.InstallLocation : null;
                var uninLoc = !string.IsNullOrEmpty(uninstaller.UninstallerLocation) ? uninstaller.UninstallerLocation : null;
                var dispNameTrimmed = uninstaller.DisplayNameTrimmed;

                var positives = startups.Where(startup =>
                {
                    if (dispNameTrimmed != null && startup.ProgramNameTrimmed?.Equals(dispNameTrimmed, StringComparison.OrdinalIgnoreCase) == true)
                        return true;

                    var cmd = startup.CommandFilePath;
                    if (string.IsNullOrEmpty(cmd))
                        return false;

                    if (instLoc != null && cmd.StartsWith(instLoc, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!validInstallLocations.Any(i => i.Length > instLoc.Length && cmd.StartsWith(i, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }

                    if (uninLoc != null && cmd.StartsWith(uninLoc, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!validUninLocations.Any(i => i.Length > uninLoc.Length && cmd.StartsWith(i, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }

                    return false;
                }).ToList();

                if (positives.Count > 0)
                    uninstaller.StartupEntries = positives;
            }
        }

        /// <summary>
        /// Look for and return all of the startup items present on this computer.
        /// </summary>
        public static IEnumerable<StartupEntryBase> GetAllStartupItems()
        {
            return Factories.Values.Aggregate(Enumerable.Empty<StartupEntryBase>(),
                (result, factory) => result.Concat(factory()));
        }

        /// <summary>
        /// Open locations of the startup entries in respective applications. (regedit, win explorer, task scheduler)
        /// </summary>
        public static void OpenStartupEntryLocations(IEnumerable<StartupEntryBase> selection)
        {
            var startupEntryBases = selection as IList<StartupEntryBase> ?? selection.ToList();
            var regOpened = false;

            if (startupEntryBases.Any(x => x is TaskEntry))
                TaskService.Instance.StartSystemTaskSchedulerManager();

            var browserAddon = startupEntryBases.OfType<BrowserHelperEntry>().FirstOrDefault();
            if (browserAddon != null)
            {
                RegistryTools.OpenRegKeyInRegedit(browserAddon.FullLongName);
                regOpened = true;
            }

            foreach (var item in startupEntryBases)
            {
                if (item is StartupEntry s && s.IsRegKey)
                {
                    if (!regOpened)
                    {
                        RegistryTools.OpenRegKeyInRegedit(item.ParentLongName);
                        regOpened = true;
                    }
                }
                else if (!string.IsNullOrEmpty(item.FullLongName))
                    WindowsTools.OpenExplorerFocusedOnObject(item.FullLongName);
                else if (!string.IsNullOrEmpty(item.CommandFilePath))
                    WindowsTools.OpenExplorerFocusedOnObject(item.CommandFilePath);
            }
        }
    }
}