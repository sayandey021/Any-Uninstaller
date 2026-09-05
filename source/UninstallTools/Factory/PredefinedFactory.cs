/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Klocman.Tools;
using Microsoft.Win32;
using UninstallTools.Properties;

namespace UninstallTools.Factory
{
    /// <summary>
    /// Get uninstallers that were manually pre-defined (e.g. OneDrive, Cortana).
    /// </summary>
    public class PredefinedFactory : IIndependantUninstallerFactory
    {
        public IList<ApplicationUninstallerEntry> GetUninstallerEntries(
            ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var items = new List<ApplicationUninstallerEntry>();

            try
            {
                var oneDriveEntry = CreateOneDriveEntry();
                if (oneDriveEntry != null)
                    items.Add(oneDriveEntry);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to create predefined OneDrive entry: " + ex);
            }

            try
            {
                var cortanaEntry = CreateLegacyCortanaEntry();
                if (cortanaEntry != null)
                    items.Add(cortanaEntry);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to create predefined Cortana entry: " + ex);
            }

            return items;
        }

        public static ApplicationUninstallerEntry CreateOneDriveEntry()
        {
            // Detect OneDrive executable paths that indicate an actual installation
            var candidateExes = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\OneDrive\\OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive\\OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive\\OneDrive.exe")
            };

            var existingExe = candidateExes.FirstOrDefault(File.Exists);

            var localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\OneDrive");
            string localAppSetup = null;
            if (Directory.Exists(localAppDataDir))
            {
                try
                {
                    localAppSetup = Directory.GetFiles(localAppDataDir, "OneDriveSetup.exe", SearchOption.AllDirectories).FirstOrDefault();
                }
                catch { }
            }

            // Installed application setup (inside the application directory, not the inbox Windows image stub)
            var installedAppSetups = new[]
            {
                localAppSetup,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\OneDrive\\Update\\OneDriveSetup.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive\\OneDriveSetup.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive\\OneDriveSetup.exe")
            };

            var installedSetup = installedAppSetups.FirstOrDefault(s => !string.IsNullOrEmpty(s) && File.Exists(s));

            bool isProcessRunning = false;
            try
            {
                isProcessRunning = Process.GetProcessesByName("OneDrive").Length > 0;
            }
            catch { }

            bool hasRegistryKey = false;
            try
            {
                using var k1 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDrive");
                if (k1 != null) hasRegistryKey = true;
                else
                {
                    using var k2 = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDrive");
                    if (k2 != null) hasRegistryKey = true;
                    else
                    {
                        using var k3 = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OneDrive");
                        if (k3 != null) hasRegistryKey = true;
                    }
                }
            }
            catch { }

            // Fallback uninstaller stubs from Windows image
            var systemStubSetups = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64\\OneDriveSetup.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OneDriveSetup.exe")
            };

            var fallbackSetup = installedSetup ?? systemStubSetups.FirstOrDefault(File.Exists);

            // Only detect OneDrive as installed if real binaries, active processes, app setups, or registry keys exist.
            // Never treat the Windows inbox stub (System32\OneDriveSetup.exe) as proof that OneDrive is installed.
            if (existingExe == null && installedSetup == null && !isProcessRunning && !hasRegistryKey)
                return null;

            var installLocation = existingExe != null ? Path.GetDirectoryName(existingExe) :
                (installedSetup != null ? Path.GetDirectoryName(installedSetup) : localAppDataDir);

            var iconPath = existingExe ?? fallbackSetup;
            var setupArg = fallbackSetup ?? "$env:SystemRoot\\System32\\OneDriveSetup.exe";

            var psScript = "& { " +
                "cmd.exe /c 'taskkill /F /T /IM OneDrive.exe 2>nul & taskkill /F /T /IM FileCoAuth.exe 2>nul & taskkill /F /T /IM OneDriveStandaloneUpdater.exe 2>nul' | Out-Null; " +
                "Stop-Process -Name OneDrive, FileCoAuth, OneDriveStandaloneUpdater -Force -ErrorAction SilentlyContinue; " +
                "if (Get-Command winget -ErrorAction SilentlyContinue) { try { winget uninstall --id Microsoft.OneDrive --silent --accept-source-agreements } catch {} }; " +
                "$userSetup = Get-ChildItem -Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\" -Filter \"OneDriveSetup.exe\" -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1; " +
                $"$s = @($userSetup, '{setupArg}', '$env:LOCALAPPDATA\\Microsoft\\OneDrive\\Update\\OneDriveSetup.exe', '$env:ProgramFiles\\Microsoft OneDrive\\OneDriveSetup.exe', '${{env:ProgramFiles(x86)}}\\Microsoft OneDrive\\OneDriveSetup.exe', '$env:SystemRoot\\SysWOW64\\OneDriveSetup.exe', '$env:SystemRoot\\System32\\OneDriveSetup.exe') | Where-Object {{ $_ -and (Test-Path $_) }} | Select-Object -First 1; " +
                "if ($s) { try { $p = Start-Process -FilePath $s -ArgumentList '/uninstall' -PassThru -ErrorAction SilentlyContinue; if ($p) { $p.WaitForExit(45000) } } catch {} }; " +
                "cmd.exe /c 'taskkill /F /T /IM OneDrive.exe 2>nul & taskkill /F /T /IM FileCoAuth.exe 2>nul & taskkill /F /T /IM OneDriveStandaloneUpdater.exe 2>nul' | Out-Null; " +
                "Stop-Process -Name OneDrive, FileCoAuth, OneDriveStandaloneUpdater -Force -ErrorAction SilentlyContinue; " +
                "Get-ScheduledTask -TaskName '*OneDrive*' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue; " +
                "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'OneDrive' -ErrorAction SilentlyContinue; " +
                "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'OneDriveSetup' -ErrorAction SilentlyContinue; " +
                "Remove-ItemProperty -Path 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'OneDrive' -ErrorAction SilentlyContinue; " +
                "$clsids = @('HKCR:\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}', 'HKCR:\\Wow6432Node\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}', 'HKCU:\\Software\\Classes\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}', 'HKCU:\\Software\\Classes\\Wow6432Node\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}'); " +
                "foreach ($c in $clsids) { if (Test-Path $c) { Set-ItemProperty -Path $c -Name 'System.IsPinnedToNameSpaceTree' -Value 0 -ErrorAction SilentlyContinue } }; " +
                "Get-ChildItem -Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\", \"$env:ProgramFiles\\Microsoft OneDrive\", \"${env:ProgramFiles(x86)}\\Microsoft OneDrive\" -Filter \"FileSyncShell*.dll\" -Recurse -ErrorAction SilentlyContinue | ForEach-Object { try { regsvr32.exe /u /s $_.FullName } catch {}; try { $tmp = Join-Path $env:TEMP ([Guid]::NewGuid().ToString('N') + '_' + $_.Name); [System.IO.File]::Move($_.FullName, $tmp) } catch {} }; " +
                "$oneDriveDirs = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\", \"$env:ProgramData\\Microsoft OneDrive\", \"$env:ProgramFiles\\Microsoft OneDrive\", \"${env:ProgramFiles(x86)}\\Microsoft OneDrive\"); " +
                "foreach ($d in $oneDriveDirs) { if (Test-Path $d) { cmd.exe /c \"takeown /f `\"$d`\" /a /r /d y /skipsl >nul 2>&1 & icacls `\"$d`\" /grant:r *S-1-5-32-544:(OI)(CI)F /t /c /q >nul 2>&1 & attrib -r -s -h `\"$d\\*.*`\" /s /d >nul 2>&1 & rd /s /q `\"$d`\" >nul 2>&1\" | Out-Null; Remove-Item -Path $d -Recurse -Force -ErrorAction SilentlyContinue } }; " +
                "try { $n = Add-Type -MemberDefinition '[DllImport(\\\"shell32.dll\\\")] public static extern void SHChangeNotify(int e, uint f, IntPtr i1, IntPtr i2);' -Name 'Shell' -Namespace 'Win' -PassThru -ErrorAction SilentlyContinue; [Win.Shell]::SHChangeNotify(0x08000000, 0x1000, [IntPtr]::Zero, [IntPtr]::Zero) } catch {}; " +
                "}";

            var uninstallCmd = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"";

            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Microsoft OneDrive",
                Publisher = "Microsoft Corporation",
                Comment = "Microsoft OneDrive Cloud Storage",
                RatingId = "Microsoft OneDrive",
                CacheIdOverride = "Microsoft.OneDrive",
                IsValid = true,
                IsProtected = false,
                SystemComponent = false,
                UninstallerKind = UninstallerType.PowerShell,
                InstallLocation = installLocation,
                UninstallString = uninstallCmd,
                QuietUninstallString = uninstallCmd,
                DisplayIcon = iconPath
            };

            if (File.Exists(iconPath))
            {
                try
                {
                    entry.IconBitmap = DrawingTools.ExtractAssociatedIcon(iconPath);
                }
                catch { }
            }

            if (existingExe != null && File.Exists(existingExe))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(existingExe);
                    entry.DisplayVersion = versionInfo.FileVersion;
                }
                catch { }
            }
            else if (fallbackSetup != null && File.Exists(fallbackSetup))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(fallbackSetup);
                    entry.DisplayVersion = versionInfo.FileVersion;
                }
                catch { }
            }

            if (Directory.Exists(installLocation))
            {
                try
                {
                    entry.InstallDate = Directory.GetCreationTime(installLocation);
                }
                catch { }
            }

            return entry;
        }

        public static ApplicationUninstallerEntry CreateLegacyCortanaEntry()
        {
            // Check if modern Cortana Store package (Microsoft.549981C3F1010) or Cortana AppX package exists
            bool hasActiveCortana = false;
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
                if (k != null)
                {
                    hasActiveCortana = k.GetSubKeyNames().Any(n => n.IndexOf("Cortana", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                   n.IndexOf("549981C3F1010", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
            catch { }

            if (!hasActiveCortana)
            {
                try
                {
                    using var k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\StateRepository\Cache\Package\Index\PackageFullName");
                    if (k2 != null)
                    {
                        hasActiveCortana = k2.GetSubKeyNames().Any(n => n.IndexOf("Cortana", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                        n.IndexOf("549981C3F1010", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                }
                catch { }
            }

            // Check if Cortana is explicitly disabled via Group Policy
            bool isCortanaDisabled = false;
            try
            {
                using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search");
                if (policyKey != null && Convert.ToInt32(policyKey.GetValue("AllowCortana", 1)) == 0)
                {
                    isCortanaDisabled = true;
                }
            }
            catch { }

            // If Cortana is disabled or has no active AppX registration, do not generate a phantom entry
            if (isCortanaDisabled || !hasActiveCortana)
                return null;

            var systemAppsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");
            string cortanaDir = null;
            if (Directory.Exists(systemAppsDir))
            {
                try
                {
                    cortanaDir = Directory.GetDirectories(systemAppsDir, "Microsoft.Windows.Cortana*").FirstOrDefault();
                }
                catch { }
            }

            var psScript = "& { " +
                "Get-AppxPackage -AllUsers *549981C3F1010* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                "Get-AppxPackage -Name *549981C3F1010* | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                "Get-AppxProvisionedPackage -Online | Where-Object { $_.PackageName -like '*549981C3F1010*' -or $_.DisplayName -like '*Cortana*' } | Remove-AppxProvisionedPackage -Online -AllUsers -ErrorAction SilentlyContinue; " +
                "Get-AppxPackage -AllUsers *Cortana* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                "Get-AppxPackage *Cortana* | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                "if (Get-Command winget -ErrorAction SilentlyContinue) { try { winget uninstall --id Microsoft.Cortana --silent --accept-source-agreements } catch {} }; " +
                "New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Force -ErrorAction SilentlyContinue | Out-Null; " +
                "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue; " +
                "Stop-Process -Name Cortana -Force -ErrorAction SilentlyContinue; " +
                "}";

            var uninstallCmd = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"";

            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Cortana",
                Publisher = "Microsoft Corporation",
                Comment = "Microsoft Cortana Voice Assistant",
                RatingId = "Microsoft Cortana",
                CacheIdOverride = "Microsoft.Windows.Cortana",
                IsValid = true,
                IsProtected = false,
                SystemComponent = false,
                UninstallerKind = UninstallerType.PowerShell,
                InstallLocation = null, // Do not point InstallLocation to C:\Windows\SystemApps to prevent deleting Windows Search
                UninstallString = uninstallCmd,
                QuietUninstallString = uninstallCmd
            };

            if (cortanaDir != null)
            {
                var cortanaExe = Path.Combine(cortanaDir, "SearchUI.exe");
                if (File.Exists(cortanaExe))
                {
                    entry.DisplayIcon = cortanaExe;
                    try
                    {
                        entry.IconBitmap = DrawingTools.ExtractAssociatedIcon(cortanaExe);
                    }
                    catch { }
                }
            }

            return entry;
        }

        public bool IsEnabled() => UninstallToolsGlobalConfig.ScanPreDefined;
        public string DisplayName => Localisation.Progress_AppStores_Templates;
    }
}