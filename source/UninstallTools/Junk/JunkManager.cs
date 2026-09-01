/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Klocman.Extensions;
using Klocman.Forms.Tools;
using Klocman.Tools;
using UninstallTools.Junk.Containers;
using UninstallTools.Properties;

namespace UninstallTools.Junk
{
    public static class JunkManager
    {
        private static IEnumerable<IJunkResult> CleanUpResults(IEnumerable<IJunkResult> input)
        {
            var prohibitedLocations = GetProhibitedLocations();

            return RemoveDuplicates(input)
                .Where(x => JunkDoesNotPointToDirectories(x, prohibitedLocations))
                .Where(JunkDoesNotPointToSelf);
        }

        /// <summary>
        /// Make sure that the junk result doesn't point to this application.
        /// </summary>
        private static bool JunkDoesNotPointToSelf(IJunkResult x)
        {
            if (x is FileSystemJunk fileSystemJunk)
            {
                return fileSystemJunk.Path == null || 
                       !fileSystemJunk.Path.FullName.StartsWith(UninstallToolsGlobalConfig.AppLocation, StringComparison.OrdinalIgnoreCase);
            }

            if (x is StartupJunkNode startupJunk)
            {
                return startupJunk.Entry?.CommandFilePath == null || 
                       !startupJunk.Entry.CommandFilePath.StartsWith(UninstallToolsGlobalConfig.AppLocation, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        /// <summary>
        /// Merge duplicate junk entries and their confidence parts
        /// </summary>
        private static IEnumerable<IJunkResult> RemoveDuplicates(IEnumerable<IJunkResult> input)
        {
            foreach (var appGroup in input.GroupBy(x => x.Application))
            {
                foreach (var group in appGroup.GroupBy(x => PathTools.NormalizePath(x.GetDisplayName()).ToLowerInvariant()))
                {
                    IJunkResult firstJunkResult = null;
                    foreach (var junkResult in group)
                    {
                        if (firstJunkResult == null)
                            firstJunkResult = junkResult;
                        else
                            firstJunkResult.Confidence.AddRange(junkResult.Confidence.ConfidenceParts);
                    }

                    if (firstJunkResult != null)
                        yield return firstJunkResult;
                }
            }
        }

        private static bool JunkDoesNotPointToDirectories(IJunkResult arg, HashSet<string> prohibitedDirs)
        {
            if (arg is not FileSystemJunk fileSystemJunk)
                return true;

            return !prohibitedDirs.Contains(fileSystemJunk.Path.FullName.ToLowerInvariant());
        }

        /// <summary>
        /// Prevent suggesting removing special directories if the app for some reason was installed into them or otherwise used them
        /// </summary>
        private static HashSet<string> GetProhibitedLocations()
        {
            var results = new HashSet<string>();

            void AddRange(IEnumerable<string> paths)
            {
                foreach (var path in paths
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Attempt(System.IO.Path.GetFullPath)
                    .Select(x => x.ToLowerInvariant()))
                {
                    results.Add(path);
                }
            }

            AddRange(Enum.GetValues<Klocman.Native.CSIDL>().Attempt(WindowsTools.GetEnvironmentPath));

            var knownFolderstype = Type.GetType("Windows.Storage.KnownFolders, Microsoft.Windows.SDK.NET", false);
            // Might not be available on some systems
            if (knownFolderstype != null)
            {
                try
                {
                    AddRange(knownFolderstype.GetProperties().Attempt(p => ((Windows.Storage.StorageFolder)p.GetValue(null))!.Path));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Failed to collect KnownFolders: " + ex);
                }
            }

            try
            {
                var userTemp = Path.GetTempPath();
                if (!string.IsNullOrEmpty(userTemp))
                    results.Add(Path.GetFullPath(userTemp).TrimEnd('\\', '/').ToLowerInvariant());
            }
            catch { }

            try
            {
                var winDir = WindowsTools.GetEnvironmentPath(Klocman.Native.CSIDL.CSIDL_WINDOWS);
                if (!string.IsNullOrEmpty(winDir))
                    results.Add(Path.GetFullPath(Path.Combine(winDir, "Temp")).TrimEnd('\\', '/').ToLowerInvariant());
            }
            catch { }

            return results;
        }

        public static IEnumerable<IJunkResult> FindJunk(IEnumerable<ApplicationUninstallerEntry> targets,
            ICollection<ApplicationUninstallerEntry> allUninstallers, ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            progressCallback(new ListGenerationProgress(-1, 0, Localisation.Junk_Progress_Startup));

            var scanners = ReflectionTools.GetTypesImplementingBase<IJunkCreator>()
                .Attempt(Activator.CreateInstance)
                .Cast<IJunkCreator>()
                .ToList();

            foreach (var junkCreator in scanners)
            {
                junkCreator.Setup(allUninstallers);
            }

            var results = new List<IJunkResult>();
            var targetEntries = targets as IList<ApplicationUninstallerEntry> ?? targets.ToList();
            var progress = 0;
            foreach (var junkCreator in scanners)
            {
                var scannerProgress = new ListGenerationProgress(progress++, scanners.Count, junkCreator.CategoryName);

                var entryProgress = 0;
                foreach (var target in targetEntries)
                {
                    scannerProgress.Inner = new ListGenerationProgress(entryProgress++, targetEntries.Count, target.DisplayName);
                    progressCallback(scannerProgress);

                    try { results.AddRange(junkCreator.FindJunk(target)); }
                    catch (SystemException ex) { PremadeDialogs.GenericError(ex); }
                }
            }

            progressCallback(new ListGenerationProgress(-1, 0, Localisation.Junk_Progress_Finishing));

            foreach (var target in targetEntries)
                results.AddRange(target.AdditionalJunk);

            return CleanUpResults(results);
        }

        public static IEnumerable<IJunkResult> FindProgramFilesJunk(
            ICollection<ApplicationUninstallerEntry> allUninstallers)
        {
            var pfScanner = new ProgramFilesOrphans();
            pfScanner.Setup(allUninstallers);
            return CleanUpResults(pfScanner.FindAllJunk().ToList());
        }

        /// <summary>
        /// Deletes a collection of junk items in batch. Standard user items are deleted directly.
        /// If any protected items require administrator privileges, they are grouped and executed
        /// in a SINGLE elevated batch pass so the user is only prompted for elevation once.
        /// </summary>
        public static JunkBatchDeleteResult DeleteJunkBatch(
            IEnumerable<IJunkResult> items,
            Action<int, int, string>? progressCallback = null)
        {
            var result = new JunkBatchDeleteResult();
            var sortedItems = items
                .OrderByDescending(x => x is RunProcessJunk)
                .ThenByDescending(x => x is StartupJunkNode)
                .ToList();

            if (sortedItems.Count == 0)
                return result;

            var pendingElevation = new List<IJunkResult>();
            int current = 0;
            int total = sortedItems.Count;

            // Phase 1: Direct Standard Deletion (AppData, HKCU, and writable locations delete silently)
            foreach (var item in sortedItems)
            {
                current++;
                try
                {
                    progressCallback?.Invoke(current, total, item.GetDisplayName());
                }
                catch { }

                try
                {
                    item.Delete();
                    result.SuccessfullyDeleted.Add(item);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                {
                    pendingElevation.Add(item);
                }
                catch (Exception ex)
                {
                    result.FailedItems[item] = ex.Message;
                }
            }

            // Phase 2: Single Elevated Batch Execution for all protected items (Program Files, HKLM, etc.)
            if (pendingElevation.Count > 0)
            {
                ExecuteElevatedBatchCleanup(pendingElevation, result);
            }

            return result;
        }

        private static void ExecuteElevatedBatchCleanup(List<IJunkResult> pendingElevation, JunkBatchDeleteResult result)
        {
            var dirs = new List<string>();
            var files = new List<string>();
            var regKeys = new List<string>();
            var regValues = new List<(string Key, string Value)>();

            foreach (var item in pendingElevation)
            {
                if (item is FileSystemJunk fs)
                {
                    if (fs.Path is DirectoryInfo)
                        dirs.Add(fs.Path.FullName);
                    else if (fs.Path is FileInfo)
                        files.Add(fs.Path.FullName);
                }
                else if (item is RegistryValueJunk rv)
                {
                    regValues.Add((rv.FullRegKeyPath, rv.ValueName));
                }
                else if (item is RegistryKeyJunk rk)
                {
                    regKeys.Add(rk.FullRegKeyPath);
                }
            }

            // If there are actionable file or registry items requiring elevation
            if (dirs.Count > 0 || files.Count > 0 || regKeys.Count > 0 || regValues.Count > 0)
            {
                var tempBatchPath = Path.Combine(Path.GetTempPath(), $"anyu_cleanup_{Guid.NewGuid():N}.cmd");
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("@echo off");
                    sb.AppendLine("chcp 65001 >nul");

                    // 1. Files
                    foreach (var f in files.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var escaped = f.Replace("\"", "\\\"");
                        sb.AppendLine($"attrib -r -s -h \"{escaped}\" >nul 2>&1");
                        sb.AppendLine($"del /f /q \"{escaped}\" >nul 2>&1");
                    }

                    // 2. Directories (sorted by path length descending so children get cleaned before parents)
                    foreach (var d in dirs.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
                    {
                        var escaped = d.Replace("\"", "\\\"");
                        sb.AppendLine($"takeown /f \"{escaped}\" /r /d y >nul 2>&1");
                        sb.AppendLine($"icacls \"{escaped}\" /grant *S-1-5-32-544:F /t /c /q >nul 2>&1");
                        sb.AppendLine($"rd /s /q \"{escaped}\" >nul 2>&1");
                    }

                    // 3. Registry values
                    foreach (var rv in regValues.Distinct())
                    {
                        var keyEscaped = rv.Key.Replace("\"", "\\\"");
                        var valEscaped = rv.Value.Replace("\"", "\\\"");
                        sb.AppendLine($"reg delete \"{keyEscaped}\" /v \"{valEscaped}\" /f >nul 2>&1");
                    }

                    // 4. Registry keys (sorted by path length descending)
                    foreach (var k in regKeys.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
                    {
                        var keyEscaped = k.Replace("\"", "\\\"");
                        sb.AppendLine($"reg delete \"{keyEscaped}\" /f >nul 2>&1");
                    }

                    File.WriteAllText(tempBatchPath, sb.ToString(), new UTF8Encoding(false));

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{tempBatchPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(startInfo);
                    proc?.WaitForExit(30000);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User cancelled single UAC prompt
                    foreach (var item in pendingElevation)
                    {
                        if (!result.SuccessfullyDeleted.Contains(item) && !result.FailedItems.ContainsKey(item))
                        {
                            result.FailedItems[item] = "Administrator permission was cancelled by user.";
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    foreach (var item in pendingElevation)
                    {
                        if (!result.SuccessfullyDeleted.Contains(item) && !result.FailedItems.ContainsKey(item))
                        {
                            result.FailedItems[item] = $"Elevation error: {ex.Message}";
                        }
                    }
                    return;
                }
                finally
                {
                    try { if (File.Exists(tempBatchPath)) File.Delete(tempBatchPath); } catch { }
                }
            }

            // Verification pass
            foreach (var item in pendingElevation)
            {
                if (result.SuccessfullyDeleted.Contains(item) || result.FailedItems.ContainsKey(item))
                    continue;

                if (item is FileSystemJunk fs)
                {
                    if (fs.Path is DirectoryInfo dir)
                    {
                        if (!Directory.Exists(dir.FullName))
                            result.SuccessfullyDeleted.Add(fs);
                        else
                            result.FailedItems[fs] = "Directory could not be removed (In use or locked).";
                    }
                    else if (fs.Path is FileInfo file)
                    {
                        if (!File.Exists(file.FullName))
                            result.SuccessfullyDeleted.Add(fs);
                        else
                            result.FailedItems[fs] = "File could not be removed (In use or locked).";
                    }
                }
                else if (item is RegistryValueJunk rv)
                {
                    try
                    {
                        using var rk = RegistryTools.OpenRegistryKey(rv.FullRegKeyPath);
                        if (rk == null || rk.GetValue(rv.ValueName) == null)
                            result.SuccessfullyDeleted.Add(rv);
                        else
                            result.FailedItems[rv] = "Registry value could not be removed.";
                    }
                    catch
                    {
                        result.SuccessfullyDeleted.Add(rv);
                    }
                }
                else if (item is RegistryKeyJunk rk)
                {
                    try
                    {
                        if (!rk.RegKeyExists())
                            result.SuccessfullyDeleted.Add(rk);
                        else
                            result.FailedItems[rk] = "Registry key could not be removed.";
                    }
                    catch
                    {
                        result.SuccessfullyDeleted.Add(rk);
                    }
                }
                else
                {
                    result.SuccessfullyDeleted.Add(item);
                }
            }
        }
    }

    public class JunkBatchDeleteResult
    {
        public HashSet<IJunkResult> SuccessfullyDeleted { get; } = new();
        public Dictionary<IJunkResult, string> FailedItems { get; } = new();
    }
}