/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Klocman.Tools;
using UninstallTools.Junk.Confidence;
using UninstallTools.Junk.Containers;
using UninstallTools.Properties;

namespace UninstallTools.Junk.Finders.Drive
{
    public class TempFolderJunkScanner : JunkCreatorBase
    {
        public override string CategoryName => "Temporary Files & Installer Cache";

        private static readonly string[] TempRoots = GetTempRoots();

        private static string[] GetTempRoots()
        {
            var list = new List<string>();
            try
            {
                var userTemp = Path.GetTempPath();
                if (!string.IsNullOrEmpty(userTemp) && Directory.Exists(userTemp))
                    list.Add(Path.GetFullPath(userTemp).TrimEnd('\\', '/'));
            }
            catch { }

            try
            {
                var winDir = WindowsTools.GetEnvironmentPath(Klocman.Native.CSIDL.CSIDL_WINDOWS);
                if (!string.IsNullOrEmpty(winDir))
                {
                    var winTemp = Path.Combine(winDir, "Temp");
                    if (Directory.Exists(winTemp))
                        list.Add(Path.GetFullPath(winTemp).TrimEnd('\\', '/'));
                }
            }
            catch { }

            try
            {
                var localData = WindowsTools.GetEnvironmentPath(Klocman.Native.CSIDL.CSIDL_LOCAL_APPDATA);
                if (!string.IsNullOrEmpty(localData))
                {
                    var localLowTemp = Path.Combine(Path.GetDirectoryName(localData.TrimEnd('\\', '/')) ?? "", "LocalLow", "Temp");
                    if (Directory.Exists(localLowTemp))
                        list.Add(Path.GetFullPath(localLowTemp).TrimEnd('\\', '/'));
                }
            }
            catch { }

            return list.Distinct().ToArray();
        }

        public override IEnumerable<IJunkResult> FindJunk(ApplicationUninstallerEntry target)
        {
            var results = new List<IJunkResult>();
            if (target == null || string.IsNullOrWhiteSpace(target.DisplayNameTrimmed))
                return results;

            var targetName = target.DisplayNameTrimmed.ToLowerInvariant();
            var targetExe = !string.IsNullOrEmpty(target.DisplayIcon)
                ? Path.GetFileNameWithoutExtension(target.DisplayIcon)?.ToLowerInvariant()
                : null;
            var targetGuid = target.BundleProviderKey != Guid.Empty
                ? target.BundleProviderKey.ToString("B").ToLowerInvariant()
                : null;

            foreach (var tempRoot in TempRoots)
            {
                try
                {
                    var rootDir = new DirectoryInfo(tempRoot);
                    if (!rootDir.Exists) continue;

                    // 1. Scan subdirectories in temp folder
                    IEnumerable<DirectoryInfo> dirs;
                    try
                    {
                        dirs = rootDir.EnumerateDirectories();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var dirName = dir.Name.ToLowerInvariant();

                            // Skip well-known root subdirectories
                            if (dirName.Equals("microsoft", StringComparison.OrdinalIgnoreCase) ||
                                dirName.Equals("windows", StringComparison.OrdinalIgnoreCase) ||
                                dirName.Equals("system32", StringComparison.OrdinalIgnoreCase))
                                continue;

                            bool isMatch = false;
                            bool isPerfectMatch = false;

                            if (targetGuid != null && dirName.Contains(targetGuid))
                            {
                                isMatch = true;
                                isPerfectMatch = true;
                            }
                            else if (dirName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                            {
                                isMatch = true;
                                isPerfectMatch = true;
                            }
                            else if (targetName.Length > 4 && (dirName.StartsWith(targetName) || dirName.Contains(targetName)))
                            {
                                isMatch = true;
                            }
                            else if (!string.IsNullOrEmpty(targetExe) && targetExe.Length > 4 &&
                                     (dirName.StartsWith(targetExe) || dirName.Contains(targetExe)))
                            {
                                isMatch = true;
                            }

                            if (isMatch)
                            {
                                var node = new FileSystemJunk(dir, target, this);
                                if (isPerfectMatch)
                                    node.Confidence.Add(ConfidenceRecords.ProductNamePerfectMatch);
                                else
                                    node.Confidence.Add(ConfidenceRecords.ExplicitConnection);

                                if (CheckIfDirIsStillUsed(dir.FullName, GetOtherInstallLocations(target)))
                                    node.Confidence.Add(ConfidenceRecords.DirectoryStillUsed);

                                if (CheckIfPublisherIsStillUsed(target, dir.Name))
                                    node.Confidence.Add(ConfidenceRecords.PublisherIsStillUsed);

                                results.Add(node);
                            }
                        }
                        catch
                        {
                            // Skip locked or inaccessible subdirectories
                        }
                    }

                    // 2. Scan installer setup logs and temporary files matching this application
                    IEnumerable<FileInfo> files;
                    try
                    {
                        files = rootDir.EnumerateFiles();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        try
                        {
                            var fileName = file.Name.ToLowerInvariant();

                            // Match setup logs: e.g. <AppName>_*.log, <AppName>Setup.log, <AppName>_install.log
                            bool isLogMatch = false;
                            if (fileName.EndsWith(".log") || fileName.EndsWith(".txt") || fileName.EndsWith(".tmp"))
                            {
                                if (targetGuid != null && fileName.Contains(targetGuid))
                                    isLogMatch = true;
                                else if (targetName.Length > 4 && fileName.StartsWith(targetName))
                                    isLogMatch = true;
                                else if (!string.IsNullOrEmpty(targetExe) && targetExe.Length > 4 && fileName.StartsWith(targetExe))
                                    isLogMatch = true;
                            }

                            if (isLogMatch)
                            {
                                var fileNode = new FileSystemJunk(file, target, this);
                                fileNode.Confidence.Add(ConfidenceRecords.ProductNamePerfectMatch);
                                results.Add(fileNode);
                            }
                        }
                        catch
                        {
                            // Skip locked files
                        }
                    }
                }
                catch
                {
                    // Skip inaccessible temp root
                }
            }

            return results;
        }
    }
}
