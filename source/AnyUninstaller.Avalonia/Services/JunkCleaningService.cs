using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UninstallTools;
using UninstallTools.Junk;
using UninstallTools.Junk.Confidence;
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

                // Ensure that for any target, its uninstaller registry key, install/uninstaller directories, and startups are included
                foreach (var target in targetList)
                {
                    // 1. Registry Key
                    if (!string.IsNullOrWhiteSpace(target.RegistryPath) && target.RegKeyStillExists())
                    {
                        var existingReg = resultList.OfType<RegistryKeyJunk>().FirstOrDefault(x =>
                            string.Equals(x.FullRegKeyPath.TrimEnd('\\'), target.RegistryPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                        if (existingReg == null)
                        {
                            var regNode = new RegistryKeyJunk(target.RegistryPath, target, null);
                            regNode.Confidence.Add(ConfidenceRecords.IsUninstallerRegistryKey);
                            regNode.Confidence.Add(ConfidenceRecords.ExplicitConnection);
                            resultList.Insert(0, regNode);
                        }
                        else
                        {
                            if (!existingReg.Confidence.ConfidenceParts.Contains(ConfidenceRecords.IsUninstallerRegistryKey))
                                existingReg.Confidence.Add(ConfidenceRecords.IsUninstallerRegistryKey);
                        }
                    }

                    // 2. Candidate Directories (InstallLocation, UninstallerLocation, UninstallerFullFilename folder)
                    // Windows Store apps (UWP/MSIX) are managed by Windows AppX deployment and must NEVER have raw uninstaller or WindowsApps folders deleted as generic junk!
                    if (target.UninstallerKind != UninstallerType.StoreApp)
                    {
                        var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(target.InstallLocation) &&
                            !ApplicationUninstallerEntry.IsSelfOrHelperDirectory(target.InstallLocation))
                            candidateDirs.Add(target.InstallLocation);
                        if (!string.IsNullOrWhiteSpace(target.UninstallerLocation) &&
                            !ApplicationUninstallerEntry.IsSelfOrHelperDirectory(target.UninstallerLocation))
                            candidateDirs.Add(target.UninstallerLocation);
                        if (!string.IsNullOrWhiteSpace(target.UninstallerFullFilename) &&
                            !ApplicationUninstallerEntry.IsSelfOrHelper(target.UninstallerFullFilename))
                        {
                            try
                            {
                                var dir = Path.GetDirectoryName(target.UninstallerFullFilename);
                                if (!string.IsNullOrWhiteSpace(dir) && !ApplicationUninstallerEntry.IsSelfOrHelperDirectory(dir))
                                    candidateDirs.Add(dir);
                            }
                            catch { }
                        }

                        foreach (var dir in candidateDirs)
                        {
                            if (Directory.Exists(dir) && IsSafeApplicationDirectory(dir))
                            {
                                bool alreadyCovered = resultList.OfType<FileSystemJunk>().Any(x =>
                                    string.Equals(x.Path?.FullName.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                                if (!alreadyCovered)
                                {
                                    var dirNode = new FileSystemJunk(new DirectoryInfo(dir), target, null);
                                    dirNode.Confidence.Add(ConfidenceRecords.ExplicitConnection);
                                    resultList.Insert(0, dirNode);
                                }
                            }
                        }
                    }

                    // 3. Startup entries
                    if (target.StartupEntries != null)
                    {
                        foreach (var startup in target.StartupEntries)
                        {
                            bool alreadyCovered = resultList.OfType<StartupJunkNode>().Any(x =>
                                x.Entry == startup ||
                                (!string.IsNullOrWhiteSpace(x.Entry?.CommandFilePath) &&
                                 !string.IsNullOrWhiteSpace(startup.CommandFilePath) &&
                                 string.Equals(x.Entry.CommandFilePath, startup.CommandFilePath, StringComparison.OrdinalIgnoreCase)));
                            if (!alreadyCovered)
                            {
                                var startupNode = new StartupJunkNode(startup, target, null);
                                resultList.Insert(0, startupNode);
                            }
                        }
                    }
                }

                // Run the merged results through JunkManager.CleanUpResults to guarantee
                // deduplication, prohibited location filtering, and self-pointing exclusion!
                var cleanResults = JunkManager.CleanUpResults(resultList).ToList();

                progress?.Report((cleanResults.Count, cleanResults.Count, $"Found {cleanResults.Count} junk items."));
                return cleanResults;
            });
        }

        private static bool IsSafeApplicationDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
                var root = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/');
                if (string.IsNullOrEmpty(root) || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Self protection: Never allow Any Uninstaller's own directories or helper folders
                if (ApplicationUninstallerEntry.IsSelfOrHelperDirectory(fullPath))
                    return false;

                var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void AddSpecial(Environment.SpecialFolder folder)
                {
                    try
                    {
                        var p = Environment.GetFolderPath(folder);
                        if (!string.IsNullOrWhiteSpace(p))
                            prohibited.Add(Path.GetFullPath(p).TrimEnd('\\', '/'));
                    }
                    catch { }
                }

                AddSpecial(Environment.SpecialFolder.Windows);
                AddSpecial(Environment.SpecialFolder.System);
                AddSpecial(Environment.SpecialFolder.SystemX86);
                AddSpecial(Environment.SpecialFolder.ProgramFiles);
                AddSpecial(Environment.SpecialFolder.ProgramFilesX86);
                AddSpecial(Environment.SpecialFolder.CommonProgramFiles);
                AddSpecial(Environment.SpecialFolder.CommonProgramFilesX86);
                AddSpecial(Environment.SpecialFolder.UserProfile);
                AddSpecial(Environment.SpecialFolder.Desktop);
                AddSpecial(Environment.SpecialFolder.CommonDesktopDirectory);
                AddSpecial(Environment.SpecialFolder.MyDocuments);

                foreach (var pf in UninstallToolsGlobalConfig.GetAllProgramFiles())
                {
                    if (!string.IsNullOrWhiteSpace(pf))
                        prohibited.Add(Path.GetFullPath(pf).TrimEnd('\\', '/'));
                }

                try
                {
                    var temp = Path.GetTempPath();
                    if (!string.IsNullOrWhiteSpace(temp))
                        prohibited.Add(Path.GetFullPath(temp).TrimEnd('\\', '/'));
                }
                catch { }

                return !prohibited.Contains(fullPath);
            }
            catch
            {
                return false;
            }
        }
    }
}

