/*
    Copyright (c) 2018 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Klocman.Extensions;
using UninstallTools.Factory.InfoAdders;

namespace UninstallTools.Factory
{
    internal static class FactoryThreadedHelpers
    {
        public static int MaxThreadsPerDrive = Math.Clamp(Environment.ProcessorCount, 4, 16);

        public static IList<ApplicationUninstallerEntry> DriveApplicationScan(
            ListGenerationProgress.ListGenerationCallback progressCallback,
            List<string> dirsToSkip,
            List<DirectoryInfo> itemsToScan)
        {
            var dividedItems = SplitByPhysicalDrives(itemsToScan, d => d);

            void GetUninstallerEntriesThread(DirectoryInfo data, List<ApplicationUninstallerEntry> state)
            {
                if (UninstallToolsGlobalConfig.IsSystemDirectory(data) ||
                    data.Name.StartsWith("Windows", StringComparison.InvariantCultureIgnoreCase))
                    return;

                var detectedEntries = DirectoryFactory.TryCreateFromDirectory(data, dirsToSkip).ToList();

                ApplicationUninstallerFactory.MergeResults(state, detectedEntries, null);
            }

            var workSpreader = new ThreadedWorkSpreader<DirectoryInfo, List<ApplicationUninstallerEntry>>
                (MaxThreadsPerDrive, GetUninstallerEntriesThread, list => new List<ApplicationUninstallerEntry>(list.Count), data => data.FullName);

            workSpreader.Start(dividedItems, progressCallback);

            var results = new List<ApplicationUninstallerEntry>();

            foreach (var workerResults in workSpreader.Join())
                ApplicationUninstallerFactory.MergeResults(results, workerResults, null);

            return results;
        }

        public static void GenerateMissingInformation(IList<ApplicationUninstallerEntry> entries, 
            InfoAdderManager infoAdder, IList<Guid> msiProducts, bool skipRunLast, 
            ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            void WorkLogic(ApplicationUninstallerEntry entry, object state)
            {
                infoAdder.AddMissingInformation(entry, skipRunLast);
                if (msiProducts != null)
                    entry.IsValid = FactoryTools.CheckIsValid(entry, msiProducts);
            }

            var workSpreader = new ThreadedWorkSpreader<ApplicationUninstallerEntry, object>(MaxThreadsPerDrive,
                WorkLogic, list => null, entry => entry.DisplayName ?? entry.RatingId ?? string.Empty);

            var cDrive = new DirectoryInfo(Environment.SystemDirectory).Root;
            var dividedItems = SplitByPhysicalDrives(entries, entry =>
            {
                var loc = entry.InstallLocation ?? entry.UninstallerLocation;
                if (!string.IsNullOrEmpty(loc))
                {
                    try
                    {
                        return new DirectoryInfo(loc);
                    }
                    catch (SystemException ex)
                    {
                        Trace.WriteLine(ex);
                    }
                }
                return cDrive;
            });

            workSpreader.Start(dividedItems, progressCallback);
            workSpreader.Join();
        }

        private static IList<IList<TData>> SplitByPhysicalDrives<TData>(IList<TData> itemsToScan, Func<TData, DirectoryInfo> locationGetter)
        {
            if (itemsToScan == null || itemsToScan.Count == 0)
                return new List<IList<TData>>();

            try
            {
                // Fast grouping by drive root (e.g. C:\, D:\) without slow WMI queries
                var groups = itemsToScan
                    .GroupBy(x =>
                    {
                        try
                        {
                            var dir = locationGetter(x);
                            return dir?.Root?.FullName?.ToUpperInvariant() ?? "UNKNOWN";
                        }
                        catch
                        {
                            return "UNKNOWN";
                        }
                    })
                    .Select(g => (IList<TData>)g.ToList())
                    .ToList();

                return groups.Count > 0 ? groups : new List<IList<TData>> { itemsToScan };
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to partition items by drive: " + ex);
                return new List<IList<TData>> { itemsToScan };
            }
        }
    }
}