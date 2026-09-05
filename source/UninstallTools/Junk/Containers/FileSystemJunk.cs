/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.IO;
using System.Linq;
using Klocman.Tools;

namespace UninstallTools.Junk.Containers
{
    public class FileSystemJunk : JunkResultBase
    {
        public FileSystemJunk(FileSystemInfo path, ApplicationUninstallerEntry application, IJunkCreator source) : base(application, source)
        {
            Path = path;
        }

        public FileSystemInfo Path { get; }

        public override void Backup(string backupDirectory)
        {
            // Items are deleted to the recycle bin
        }

        public override void Delete()
        {
            if (Path is DirectoryInfo dir)
            {
                if (dir.Exists)
                {
                    DeleteDirectoryGracefully(dir);
                    dir.Refresh();

                    // If still exists and process is elevated, try taking ownership and forcing deletion
                    if (dir.Exists && WindowsTools.IsAdministrator())
                    {
                        WindowsTools.TakeOwnershipAndGrantPermissions(dir.FullName, true);
                        try
                        {
                            RemoveReadOnlyAttributes(dir);
                            dir.Delete(true);
                        }
                        catch { }
                        dir.Refresh();
                    }

                    // If directory still exists, do NOT fake success! Throw so JunkManager routes to elevated batch or reports error
                    if (dir.Exists)
                    {
                        WindowsTools.ScheduleDeleteOnReboot(dir.FullName);
                        throw new UnauthorizedAccessException($"Directory '{dir.FullName}' could not be deleted (access denied or in use).");
                    }
                }
            }
            else if (Path is FileInfo file)
            {
                if (file.Exists)
                {
                    file.Attributes = FileAttributes.Normal;
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // File locked by Explorer or other process: try moving away to temp so parent folder can be deleted
                        bool moved = false;
                        try
                        {
                            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnyUninstaller_PendingDelete");
                            Directory.CreateDirectory(tempDir);
                            var tempDest = System.IO.Path.Combine(tempDir, $"{Guid.NewGuid():N}_{file.Name}");
                            File.Move(file.FullName, tempDest);
                            WindowsTools.ScheduleDeleteOnReboot(tempDest);
                            moved = true;
                        }
                        catch { }

                        if (!moved && WindowsTools.IsAdministrator())
                        {
                            WindowsTools.TakeOwnershipAndGrantPermissions(file.FullName, false);
                            try
                            {
                                file.Attributes = FileAttributes.Normal;
                                file.Delete();
                            }
                            catch { }
                        }
                    }

                    file.Refresh();
                    if (file.Exists)
                    {
                        WindowsTools.ScheduleDeleteOnReboot(file.FullName);
                        throw new UnauthorizedAccessException($"File '{file.FullName}' could not be deleted (access denied or in use).");
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Unknown FileSystemInfo implementation");
            }
        }

        private static void DeleteDirectoryGracefully(DirectoryInfo dir)
        {
            RemoveReadOnlyAttributes(dir);
            try
            {
                dir.Delete(true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In-use lock encountered (e.g. Explorer loaded shell extension DLL).
                // Delete unlocked items, relocate/schedule locked files, and remove folder structure.
                DeleteDirectoryContentsWithRebootFallback(dir);
            }
        }

        private static void DeleteDirectoryContentsWithRebootFallback(DirectoryInfo dir)
        {
            // 1. Files
            FileInfo[] files;
            try
            {
                files = dir.GetFiles("*", SearchOption.AllDirectories);
            }
            catch
            {
                // If GetFiles failed due to permissions, try taking ownership if elevated
                if (WindowsTools.IsAdministrator())
                {
                    WindowsTools.TakeOwnershipAndGrantPermissions(dir.FullName, true);
                    try
                    {
                        files = dir.GetFiles("*", SearchOption.AllDirectories);
                    }
                    catch
                    {
                        WindowsTools.ScheduleDeleteOnReboot(dir.FullName);
                        return;
                    }
                }
                else
                {
                    WindowsTools.ScheduleDeleteOnReboot(dir.FullName);
                    return;
                }
            }

            foreach (var f in files)
            {
                try
                {
                    f.Attributes = FileAttributes.Normal;
                    f.Delete();
                }
                catch
                {
                    // Locked file (e.g. loaded by explorer.exe).
                    // Move away from parent folder so the directory can be deleted.
                    bool moved = false;
                    try
                    {
                        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnyUninstaller_PendingDelete");
                        Directory.CreateDirectory(tempDir);
                        var tempDest = System.IO.Path.Combine(tempDir, $"{Guid.NewGuid():N}_{f.Name}");
                        File.Move(f.FullName, tempDest);
                        WindowsTools.ScheduleDeleteOnReboot(tempDest);
                        moved = true;
                    }
                    catch { }

                    if (!moved)
                    {
                        WindowsTools.ScheduleDeleteOnReboot(f.FullName);
                    }
                }
            }

            // 2. Subdirectories (deepest first)
            DirectoryInfo[] subDirs = Array.Empty<DirectoryInfo>();
            try
            {
                subDirs = dir.GetDirectories("*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.FullName.Length)
                    .ToArray();
            }
            catch { }

            foreach (var d in subDirs)
            {
                try
                {
                    d.Attributes = FileAttributes.Normal;
                    d.Delete(true);
                }
                catch
                {
                    WindowsTools.ScheduleDeleteOnReboot(d.FullName);
                }
            }

            // 3. Root directory itself
            try
            {
                dir.Refresh();
                if (dir.Exists)
                {
                    dir.Attributes = FileAttributes.Normal;
                    dir.Delete(true);
                }
            }
            catch
            {
                WindowsTools.ScheduleDeleteOnReboot(dir.FullName);
            }
        }

        private static void RemoveReadOnlyAttributes(DirectoryInfo directory)
        {
            try
            {
                directory.Attributes = FileAttributes.Normal;
                foreach (var f in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { f.Attributes = FileAttributes.Normal; } catch { }
                }
            }
            catch { }
        }

        public override string GetDisplayName()
        {
            return Path.FullName;
        }

        public override void Open()
        {
            if (Path.Exists)
                WindowsTools.OpenExplorerFocusedOnObject(Path.FullName);
            else
                throw new FileNotFoundException(null, Path.FullName);
        }
    }
}