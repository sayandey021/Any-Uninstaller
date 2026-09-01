using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Klocman.IO;

namespace AnyUninstaller.Avalonia.Services
{
    public enum TempCategory
    {
        UserTemp,
        SystemTemp,
        CrashDumps,
        UpdateCache,
        WebCache
    }

    public class TempItemInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TempCategory Category { get; set; }
        public string CategoryName => Category switch
        {
            TempCategory.UserTemp => "User Temp",
            TempCategory.SystemTemp => "Windows Temp",
            TempCategory.CrashDumps => "Crash Dumps & Diagnostics",
            TempCategory.UpdateCache => "Windows Update Cache",
            TempCategory.WebCache => "Web & App Caches",
            _ => "Temporary Files"
        };
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public FileSize Size => FileSize.FromBytes(SizeBytes);
        public int FileCount { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsRecommended { get; set; } = true;
    }

    public class TempCleanResult
    {
        public int DeletedFilesCount { get; set; }
        public int SkippedFilesCount { get; set; }
        public long DeletedBytes { get; set; }
        public FileSize DeletedSize => FileSize.FromBytes(DeletedBytes);
        public long SkippedBytes { get; set; }
        public FileSize SkippedSize => FileSize.FromBytes(SkippedBytes);
        public List<string> ErrorMessages { get; set; } = new();
    }

    public class TempCleaningService
    {
        public static readonly TempCleaningService Instance = new();

        public async Task<List<TempItemInfo>> ScanTempLocationsAsync(
            IProgress<(int current, int total, string message)>? progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var results = new List<TempItemInfo>();
                var targetLocations = GetTempTargets();

                int totalLocations = targetLocations.Count;
                int currentLoc = 0;

                foreach (var (category, directoryPath) in targetLocations)
                {
                    ct.ThrowIfCancellationRequested();
                    currentLoc++;

                    if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                        continue;

                    progress?.Report((currentLoc, totalLocations, $"Scanning {Path.GetFileName(directoryPath)}..."));

                    try
                    {
                        var dirInfo = new DirectoryInfo(directoryPath);

                        // 1. Scan direct files in this temp location
                        FileInfo[] files;
                        try
                        {
                            files = dirInfo.GetFiles();
                        }
                        catch
                        {
                            files = Array.Empty<FileInfo>();
                        }

                        foreach (var file in files)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                results.Add(new TempItemInfo
                                {
                                    FullPath = file.FullName,
                                    Name = file.Name,
                                    Category = category,
                                    IsDirectory = false,
                                    SizeBytes = file.Length,
                                    FileCount = 1,
                                    LastModified = file.LastWriteTime,
                                    IsRecommended = true
                                });
                            }
                            catch
                            {
                                // Skip unreadable file
                            }
                        }

                        // 2. Scan direct top-level subdirectories in this temp location
                        DirectoryInfo[] subDirs;
                        try
                        {
                            subDirs = dirInfo.GetDirectories();
                        }
                        catch
                        {
                            subDirs = Array.Empty<DirectoryInfo>();
                        }

                        foreach (var subDir in subDirs)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                var (dirSize, fileCount, lastMod) = CalculateDirectoryStats(subDir, ct);
                                if (fileCount > 0 || dirSize > 0)
                                {
                                    results.Add(new TempItemInfo
                                    {
                                        FullPath = subDir.FullName,
                                        Name = subDir.Name,
                                        Category = category,
                                        IsDirectory = true,
                                        SizeBytes = dirSize,
                                        FileCount = fileCount,
                                        LastModified = lastMod,
                                        IsRecommended = true
                                    });
                                }
                            }
                            catch
                            {
                                // Skip inaccessible folder
                            }
                        }
                    }
                    catch
                    {
                        // Ignore directory access exception
                    }
                }

                progress?.Report((totalLocations, totalLocations, $"Found {results.Count} items to clean."));
                return results.OrderByDescending(x => x.SizeBytes).ToList();
            }, ct);
        }

        private static List<(TempCategory category, string path)> GetTempTargets()
        {
            var targets = new List<(TempCategory category, string path)>();

            // 1. User Temp (%TEMP%, %LOCALAPPDATA%\Temp)
            try
            {
                var userTemp = Path.GetTempPath();
                if (!string.IsNullOrEmpty(userTemp))
                    targets.Add((TempCategory.UserTemp, userTemp.TrimEnd('\\', '/')));
            }
            catch { }

            // 2. Windows System Temp (C:\Windows\Temp)
            try
            {
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(winDir))
                {
                    var winTemp = Path.Combine(winDir, "Temp");
                    targets.Add((TempCategory.SystemTemp, winTemp));
                }
            }
            catch { }

            // 3. Crash Dumps & WER Diagnostics
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    targets.Add((TempCategory.CrashDumps, Path.Combine(localAppData, "CrashDumps")));
                    targets.Add((TempCategory.CrashDumps, Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive")));
                    targets.Add((TempCategory.CrashDumps, Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue")));
                    targets.Add((TempCategory.CrashDumps, Path.Combine(localAppData, "Microsoft", "Windows", "WER", "Temp")));
                }

                var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrEmpty(progData))
                {
                    targets.Add((TempCategory.CrashDumps, Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive")));
                    targets.Add((TempCategory.CrashDumps, Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue")));
                    targets.Add((TempCategory.CrashDumps, Path.Combine(progData, "Microsoft", "Windows", "WER", "Temp")));
                }
            }
            catch { }

            // 4. Windows Update Download Cache (C:\Windows\SoftwareDistribution\Download)
            try
            {
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(winDir))
                {
                    targets.Add((TempCategory.UpdateCache, Path.Combine(winDir, "SoftwareDistribution", "Download")));
                }
            }
            catch { }

            // 5. Temporary Web / App Caches (INetCache)
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    targets.Add((TempCategory.WebCache, Path.Combine(localAppData, "Microsoft", "Windows", "INetCache")));
                    targets.Add((TempCategory.WebCache, Path.Combine(localAppData, "Microsoft", "Windows", "IECompatCache")));
                }
            }
            catch { }

            return targets;
        }

        private static (long totalSize, int fileCount, DateTime lastMod) CalculateDirectoryStats(DirectoryInfo dir, CancellationToken ct)
        {
            long size = 0;
            int count = 0;
            DateTime newest = dir.LastWriteTime;

            var stack = new Stack<DirectoryInfo>();
            stack.Push(dir);

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var current = stack.Pop();

                try
                {
                    if (current.LastWriteTime > newest) newest = current.LastWriteTime;

                    foreach (var file in current.GetFiles())
                    {
                        ct.ThrowIfCancellationRequested();
                        size += file.Length;
                        count++;
                        if (file.LastWriteTime > newest) newest = file.LastWriteTime;
                    }

                    foreach (var sub in current.GetDirectories())
                    {
                        stack.Push(sub);
                    }
                }
                catch
                {
                    // Inaccessible subtree
                }
            }

            return (size, count, newest);
        }

        public async Task<TempCleanResult> CleanItemsAsync(
            IEnumerable<TempItemInfo> items,
            Action<string, bool, string?>? onItemProcessed = null,
            IProgress<(int current, int total, string message)>? progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var result = new TempCleanResult();
                var itemList = items.ToList();
                int total = itemList.Count;
                int current = 0;

                foreach (var item in itemList)
                {
                    ct.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report((current, total, $"Deleting {item.Name}..."));

                    if (item.IsDirectory)
                    {
                        var (deletedBytes, deletedFiles, skippedBytes, skippedFiles, hasError, errorMsg) = DeleteDirectoryContentsSafe(item.FullPath, ct);
                        result.DeletedBytes += deletedBytes;
                        result.DeletedFilesCount += deletedFiles;
                        result.SkippedBytes += skippedBytes;
                        result.SkippedFilesCount += skippedFiles;

                        if (deletedFiles > 0 && skippedFiles == 0)
                        {
                            onItemProcessed?.Invoke(item.FullPath, true, null);
                        }
                        else if (deletedFiles > 0 && skippedFiles > 0)
                        {
                            onItemProcessed?.Invoke(item.FullPath, true, $"Partially cleaned ({skippedFiles} file(s) in use)");
                        }
                        else
                        {
                            onItemProcessed?.Invoke(item.FullPath, false, errorMsg ?? "Files in use by another program");
                        }
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(item.FullPath))
                            {
                                File.SetAttributes(item.FullPath, FileAttributes.Normal);
                                File.Delete(item.FullPath);
                                result.DeletedBytes += item.SizeBytes;
                                result.DeletedFilesCount++;
                                onItemProcessed?.Invoke(item.FullPath, true, null);
                            }
                            else
                            {
                                onItemProcessed?.Invoke(item.FullPath, true, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.SkippedBytes += item.SizeBytes;
                            result.SkippedFilesCount++;
                            string reason = ex is IOException or UnauthorizedAccessException ? "File in use / locked" : ex.Message;
                            onItemProcessed?.Invoke(item.FullPath, false, reason);
                        }
                    }
                }

                progress?.Report((total, total, $"Cleaned {result.DeletedFilesCount} files ({result.DeletedSize})."));
                return result;
            }, ct);
        }

        private static (long deletedBytes, int deletedFiles, long skippedBytes, int skippedFiles, bool hasError, string? errorMsg)
            DeleteDirectoryContentsSafe(string dirPath, CancellationToken ct)
        {
            long deletedBytes = 0;
            int deletedFiles = 0;
            long skippedBytes = 0;
            int skippedFiles = 0;
            string? lastError = null;

            if (!Directory.Exists(dirPath))
                return (0, 0, 0, 0, false, null);

            try
            {
                var dirInfo = new DirectoryInfo(dirPath);

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        long len = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        deletedBytes += len;
                        deletedFiles++;
                    }
                    catch (Exception ex)
                    {
                        skippedBytes += file.Length;
                        skippedFiles++;
                        lastError = ex is IOException or UnauthorizedAccessException ? "In use" : ex.Message;
                    }
                }

                // Try deleting empty subdirectories bottom-up
                foreach (var sub in dirInfo.GetDirectories("*", SearchOption.AllDirectories).OrderByDescending(d => d.FullName.Length))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        sub.Delete(false);
                    }
                    catch { }
                }

                // Try deleting the root directory if all files were deleted
                try
                {
                    dirInfo.Delete(false);
                }
                catch { }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            return (deletedBytes, deletedFiles, skippedBytes, skippedFiles, skippedFiles > 0, lastError);
        }
    }
}
