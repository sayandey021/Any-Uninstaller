/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.IO;
using Klocman.Tools;
using Microsoft.Win32;

namespace UninstallTools.Junk.Containers
{
    public class RegistryKeyJunk : JunkResultBase
    {
        public string FullRegKeyPath { get; }

        public RegistryKey OpenRegKey(bool writable = false)
        {
            return RegistryTools.OpenRegistryKey(FullRegKeyPath, writable);
        }

        public string RegKeyParentPath => Path.GetDirectoryName(FullRegKeyPath);
        public string RegKeyName => Path.GetFileName(FullRegKeyPath);

        public bool RegKeyExists()
        {
            using (var key = OpenRegKey())
                return key != null;
        }

        public RegistryKeyJunk(string fullRegKeyPath, ApplicationUninstallerEntry application, IJunkCreator source) : base(application, source)
        {
            if (string.IsNullOrEmpty(fullRegKeyPath))
                throw new ArgumentException(@"Argument is null or empty", nameof(fullRegKeyPath));

            FullRegKeyPath = fullRegKeyPath.TrimEnd('\\', '/', ' ');
        }

        public override void Backup(string backupDirectory)
        {
            var fileName = PathTools.SanitizeFileName(FullRegKeyPath.TrimStart('\\')) + ".reg";
            var path = Path.Combine(CreateBackupDirectory(backupDirectory), fileName);
            RegistryTools.ExportRegistry(path, new[] { FullRegKeyPath });
        }

        public override void Delete()
        {
            try
            {
                using (var key = RegistryTools.OpenRegistryKey(RegKeyParentPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree(RegKeyName, false);
                        return;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Fall back to reg.exe delete if standard .NET access was denied
                if (TryRegExeDelete(FullRegKeyPath))
                    return;
                throw;
            }
            catch (Exception)
            {
                if (TryRegExeDelete(FullRegKeyPath))
                    return;
                throw;
            }

            if (!TryRegExeDelete(FullRegKeyPath))
            {
                throw new IOException($"Registry key \"{FullRegKeyPath}\" could not be found or opened for deletion.");
            }
        }

        private static bool TryRegExeDelete(string fullPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("reg.exe", $"delete \"{fullPath}\" /f")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(3000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public override void Open()
        {
            if (!RegKeyExists())
                throw new IOException($"Key \"{FullRegKeyPath}\" doesn't exist or can't be accessed");

            RegistryTools.OpenRegKeyInRegedit(FullRegKeyPath);
        }

        public override string GetDisplayName()
        {
            return FullRegKeyPath;
        }
    }
}