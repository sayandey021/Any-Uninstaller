/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Klocman.IO;
using Klocman.Tools;
using UninstallTools.Properties;

namespace UninstallTools.Factory
{
    public class WindowsUpdateFactory : IIndependantUninstallerFactory
    {
        private static string HelperPath { get; } = Path.Combine(UninstallToolsGlobalConfig.AssemblyLocation, @"WinUpdateHelper.exe");
        private static bool IsHelperAvailable() => File.Exists(HelperPath);

        public IList<ApplicationUninstallerEntry> GetUninstallerEntries(ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var results = new List<ApplicationUninstallerEntry>();

            // 1. Direct in-process Windows Update Agent (WUA) COM discovery
            results.AddRange(QueryUpdatesDirectly());

            // 2. Fallback to external helper if direct query returned no results and helper exists
            if (results.Count == 0 && IsHelperAvailable())
            {
                var output = FactoryTools.StartHelperAndReadOutput(HelperPath, "list");
                if (!string.IsNullOrEmpty(output) && !output.Trim().StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var group in FactoryTools.ExtractAppDataSetsFromHelperOutput(output))
                    {
                        var entry = new ApplicationUninstallerEntry
                        {
                            UninstallerKind = UninstallerType.WindowsUpdate,
                            IsUpdate = true,
                            Publisher = "Microsoft Corporation"
                        };
                        foreach (var valuePair in group)
                        {
                            switch (valuePair.Key)
                            {
                                case "UpdateID":
                                    entry.RatingId = valuePair.Value;
                                    if (GuidTools.TryExtractGuid(valuePair.Value, out var result))
                                        entry.BundleProviderKey = result;
                                    break;
                                case "RevisionNumber":
                                    entry.DisplayVersion = ApplicationEntryTools.CleanupDisplayVersion(valuePair.Value);
                                    break;
                                case "Title":
                                    entry.RawDisplayName = valuePair.Value;
                                    break;
                                case "IsUninstallable":
                                    if (bool.TryParse(valuePair.Value, out var isUnins))
                                        entry.IsProtected = !isUnins;
                                    break;
                                case "SupportUrl":
                                    entry.AboutUrl = valuePair.Value;
                                    break;
                                case "MinDownloadSize":
                                    if (long.TryParse(valuePair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                                        entry.EstimatedSize = FileSize.FromBytes(size);
                                    break;
                                case "LastDeploymentChangeTime":
                                    if (DateTime.TryParse(valuePair.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                                        !DateTime.MinValue.Equals(date))
                                        entry.InstallDate = date;
                                    break;
                            }
                        }

                        if (!string.IsNullOrEmpty(entry.RawDisplayName))
                        {
                            var kbMatch = Regex.Match(entry.RawDisplayName, @"\b(KB\d+)\b", RegexOptions.IgnoreCase);
                            if (kbMatch.Success)
                            {
                                var kbNum = kbMatch.Value.Substring(2);
                                entry.UninstallString = $"wusa.exe /uninstall /kb:{kbNum}";
                                entry.QuietUninstallString = $"wusa.exe /uninstall /kb:{kbNum} /quiet /norestart";
                            }
                            else
                            {
                                entry.UninstallString = $"\"{HelperPath}\" uninstall {entry.RatingId}";
                                entry.QuietUninstallString = entry.UninstallString;
                            }
                        }

                        results.Add(entry);
                    }
                }
            }

            return results;
        }

        private static List<ApplicationUninstallerEntry> QueryUpdatesDirectly()
        {
            var results = new List<ApplicationUninstallerEntry>();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null) return results;

                var session = Activator.CreateInstance(sessionType);
                if (session == null) return results;

                var searcher = sessionType.InvokeMember("CreateUpdateSearcher", BindingFlags.InvokeMethod, null, session, null);
                if (searcher == null) return results;

                var searchResult = searcher.GetType().InvokeMember("Search", BindingFlags.InvokeMethod, null, searcher, new object[] { "IsInstalled=1 and IsPresent=1 and Type='Software'" });
                if (searchResult == null) return results;

                var updates = searchResult.GetType().InvokeMember("Updates", BindingFlags.GetProperty, null, searchResult, null);
                if (updates == null) return results;

                var countObj = updates.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, updates, null);
                if (countObj is int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var update = updates.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, updates, new object[] { i });
                            if (update == null) continue;

                            var updateType = update.GetType();
                            var title = updateType.InvokeMember("Title", BindingFlags.GetProperty, null, update, null)?.ToString();
                            var isUninsObj = updateType.InvokeMember("IsUninstallable", BindingFlags.GetProperty, null, update, null);
                            bool isUninstallable = isUninsObj is bool b && b;
                            var supportUrl = updateType.InvokeMember("SupportUrl", BindingFlags.GetProperty, null, update, null)?.ToString();
                            var minSizeObj = updateType.InvokeMember("MinDownloadSize", BindingFlags.GetProperty, null, update, null);
                            var dateObj = updateType.InvokeMember("LastDeploymentChangeTime", BindingFlags.GetProperty, null, update, null);

                            var identity = updateType.InvokeMember("Identity", BindingFlags.GetProperty, null, update, null);
                            string? updateId = null;
                            string? revisionNumber = null;
                            if (identity != null)
                            {
                                var idType = identity.GetType();
                                updateId = idType.InvokeMember("UpdateID", BindingFlags.GetProperty, null, identity, null)?.ToString();
                                revisionNumber = idType.InvokeMember("RevisionNumber", BindingFlags.GetProperty, null, identity, null)?.ToString();
                            }

                            var entry = new ApplicationUninstallerEntry
                            {
                                UninstallerKind = UninstallerType.WindowsUpdate,
                                IsUpdate = true,
                                Publisher = "Microsoft Corporation",
                                RawDisplayName = title ?? "Windows Update",
                                IsProtected = !isUninstallable,
                                AboutUrl = supportUrl
                            };

                            if (!string.IsNullOrEmpty(updateId))
                            {
                                entry.RatingId = updateId;
                                if (GuidTools.TryExtractGuid(updateId, out var guid))
                                    entry.BundleProviderKey = guid;
                            }

                            if (!string.IsNullOrEmpty(revisionNumber))
                                entry.DisplayVersion = ApplicationEntryTools.CleanupDisplayVersion(revisionNumber);

                            if (minSizeObj is decimal or long or int or double)
                            {
                                long sizeLong = Convert.ToInt64(minSizeObj);
                                if (sizeLong > 0)
                                    entry.EstimatedSize = FileSize.FromBytes(sizeLong);
                            }

                            if (dateObj is DateTime dt && dt != DateTime.MinValue)
                                entry.InstallDate = dt;

                            if (!string.IsNullOrEmpty(title))
                            {
                                var kbMatch = Regex.Match(title, @"\b(KB\d+)\b", RegexOptions.IgnoreCase);
                                if (kbMatch.Success)
                                {
                                    var kbNum = kbMatch.Value.Substring(2);
                                    entry.UninstallString = $"wusa.exe /uninstall /kb:{kbNum}";
                                    entry.QuietUninstallString = $"wusa.exe /uninstall /kb:{kbNum} /quiet /norestart";
                                }
                            }

                            results.Add(entry);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Error parsing update item #{i}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Direct WUA COM query error: {ex.Message}");
            }

            return results;
        }

        public bool IsEnabled() => UninstallToolsGlobalConfig.ScanWinUpdates;
        public string DisplayName => Localisation.Progress_AppStores_WinUpdates;
    }
}
