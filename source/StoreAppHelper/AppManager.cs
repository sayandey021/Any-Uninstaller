/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Klocman;
using System.Xml;

namespace StoreAppHelper
{
    public static class AppManager
    {
        private static readonly HashSet<string> EssentialSystemPackages = new(StringComparer.OrdinalIgnoreCase)
        {
            "windows.immersivecontrolpanel",
            "Microsoft.Windows.ShellExperienceHost",
            "Microsoft.Windows.StartMenuExperienceHost",
            "Microsoft.Windows.Search",
            "Microsoft.LockApp",
            "Microsoft.Windows.SecHealthUI",
            "Microsoft.Windows.ContentDeliveryManager",
            "Microsoft.Windows.ParentalControls",
            "Microsoft.AccountsControl",
            "Microsoft.BioEnrollment",
            "Microsoft.CredDialogHost",
            "Microsoft.ECApp",
            "Microsoft.Win32WebViewHost"
        };

        public static void UninstallApp(string fullName)
        {
            Console.WriteLine($"Uninstalling \"{fullName}\"");
            var packageManager = new PackageManager();

            // Extract package name (part before first underscore)
            var sep = fullName.IndexOf('_');
            var packageName = sep > 0 ? fullName.Substring(0, sep) : fullName;
            var familyName = GetPackageFamilyName(fullName, packageName);

            // Step 1: Try WinRT removal
            bool winRtSucceeded = false;
            string lastErrorText = null;
            int lastErrorCode = 0;

            // If elevated, try RemoveForAllUsers first
            bool isElevated = IsCurrentProcessElevated();
            if (isElevated)
            {
                try
                {
                    var opAllUsers = packageManager.RemovePackageAsync(fullName, RemovalOptions.RemoveForAllUsers);
                    var waitAll = new ManualResetEvent(false);
                    opAllUsers.Completed += (_, _) => waitAll.Set();
                    waitAll.WaitOne();

                    if (opAllUsers.Status == AsyncStatus.Completed)
                    {
                        winRtSucceeded = true;
                        TryDeprovisionPackage(packageManager, fullName, packageName, familyName);
                    }
                    else if (opAllUsers.Status == AsyncStatus.Error)
                    {
                        lastErrorText = opAllUsers.GetResults()?.ErrorText;
                        lastErrorCode = opAllUsers.ErrorCode?.HResult ?? 0;
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.WriteExceptionToLog(ex);
                }
            }

            // If not succeeded yet, try standard RemovePackageAsync
            if (!winRtSucceeded)
            {
                try
                {
                    var deploymentOperation = packageManager.RemovePackageAsync(fullName);
                    var opCompletedEvent = new ManualResetEvent(false);
                    deploymentOperation.Completed += (_, _) => opCompletedEvent.Set();
                    opCompletedEvent.WaitOne();

                    if (deploymentOperation.Status == AsyncStatus.Completed)
                    {
                        winRtSucceeded = true;
                        if (isElevated)
                            TryDeprovisionPackage(packageManager, fullName, packageName, familyName);
                    }
                    else if (deploymentOperation.Status == AsyncStatus.Canceled)
                    {
                        Console.WriteLine(@"Uninstallation was cancelled");
                        throw new OperationCanceledException();
                    }
                    else if (deploymentOperation.Status == AsyncStatus.Error)
                    {
                        lastErrorText = deploymentOperation.GetResults()?.ErrorText;
                        lastErrorCode = deploymentOperation.ErrorCode?.HResult ?? 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWriter.WriteExceptionToLog(ex);
                }
            }

            // Check if package is already removed for current user
            if (!IsPackageInstalledForCurrentUser(fullName, packageName))
            {
                // Ensure provisioned package is also de-provisioned and removed for all users
                TryDeprovisionPackage(packageManager, fullName, packageName, familyName);
                if (IsPackageProvisioned(packageName))
                {
                    TryPowerShellFallbackUninstall(fullName, packageName, isElevated);
                }

                Console.WriteLine(@"Uninstallation completed successfully");
                return;
            }

            // Step 2: Resilient PowerShell fallback for system/provisioned apps (Cortana, Snip & Sketch, Maps, Mixed Reality, etc.)
            Console.WriteLine(@"WinRT uninstallation failed or package is provisioned; attempting elevated/PowerShell fallback for " + packageName);
            if (TryPowerShellFallbackUninstall(fullName, packageName, isElevated))
            {
                Console.WriteLine(@"PowerShell fallback uninstallation completed successfully");
                return;
            }

            // If all attempts failed, throw IOException with the captured error info
            Console.WriteLine(@"Error code: 0x{0:X8}", lastErrorCode);
            Console.WriteLine(@"Error text: {0}", lastErrorText);
            throw new IOException(lastErrorText ?? "Failed to uninstall package " + fullName);
        }

        private static bool IsPackageProvisioned(string packageName)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"@(Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like '*{packageName}*' -or $_.PackageName -like '*{packageName}*' }}).Count\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                var outStr = p?.StandardOutput.ReadToEnd()?.Trim();
                p?.WaitForExit(5000);
                return int.TryParse(outStr, out var count) && count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCurrentProcessElevated()
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string GetPackageFamilyName(string fullName, string packageName)
        {
            try
            {
                var parts = fullName.Split('_');
                if (parts.Length >= 5)
                {
                    var publisherId = parts[parts.Length - 1];
                    if (!string.IsNullOrEmpty(publisherId))
                        return $"{packageName}_{publisherId}";
                }
            }
            catch { }
            return string.Empty;
        }

        private static void TryDeprovisionPackage(PackageManager packageManager, string fullName, string packageName, string familyName)
        {
            try
            {
                if (!string.IsNullOrEmpty(familyName))
                {
                    var deprovOp = packageManager.DeprovisionPackageForAllUsersAsync(familyName);
                    var waitDeprov = new ManualResetEvent(false);
                    deprovOp.Completed += (_, _) => waitDeprov.Set();
                    waitDeprov.WaitOne(10000);
                }
            }
            catch
            {
                // Best effort WinRT de-provisioning
            }

            // Also try deprovisioning via PowerShell / DISM
            try
            {
                var script = $"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like '*{packageName}*' -or $_.PackageName -like '*{packageName}*' }} | ForEach-Object {{ " +
                             $"Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -AllUsers -ErrorAction SilentlyContinue; " +
                             $"dism.exe /Online /NoRestart /Remove-ProvisionedAppxPackage /PackageName:$($_.PackageName) 2>$null }}";
                bool elevated = IsCurrentProcessElevated();
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"")
                {
                    UseShellExecute = !elevated,
                    Verb = elevated ? "" : "runas",
                    CreateNoWindow = elevated,
                    WindowStyle = elevated ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(30000);
            }
            catch
            {
                // Best effort
            }
        }

        private static bool TryPowerShellFallbackUninstall(string fullName, string packageName, bool isElevated)
        {
            try
            {
                var script = $"$n = '{packageName}'; $fn = '{fullName}'; " +
                             "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like \"*$n*\" -or $_.PackageName -like \"*$n*\" } | ForEach-Object { " +
                             "    Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -AllUsers -ErrorAction SilentlyContinue; " +
                             "    dism.exe /Online /NoRestart /Remove-ProvisionedAppxPackage /PackageName:$($_.PackageName) 2>$null " +
                             "}; " +
                             "Get-AppxPackage -AllUsers -Name \"*$n*\" | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                             "Get-AppxPackage -Name \"*$n*\" | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                             "if ($fn) { Remove-AppxPackage -Package $fn -AllUsers -ErrorAction SilentlyContinue; Remove-AppxPackage -Package $fn -ErrorAction SilentlyContinue }; " +
                             "if (@(Get-AppxPackage -Name \"*$n*\").Count -eq 0) { exit 0 } else { exit 1 }";

                if (!isElevated)
                {
                    // For provisioned or system apps, standard user cannot remove for all users or deprovision.
                    // Request elevation via UAC to successfully deprovision and remove for all users.
                    var elevatedStartInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"")
                    {
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    try
                    {
                        using var proc = Process.Start(elevatedStartInfo);
                        proc?.WaitForExit(60000);
                    }
                    catch (Win32Exception)
                    {
                        // User declined UAC prompt or MSIX denied elevation; try non-elevated removal for current user
                        var userStartInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Remove-AppxPackage -Package '{fullName}' -ErrorAction SilentlyContinue; Get-AppxPackage -Name '*{packageName}*' | Remove-AppxPackage -ErrorAction SilentlyContinue\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using var proc = Process.Start(userStartInfo);
                        proc?.WaitForExit(30000);
                    }
                }
                else
                {
                    var startInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var proc = Process.Start(startInfo);
                    proc?.WaitForExit(60000);
                }

                // Verify whether the package was successfully removed for the current user
                return !IsPackageInstalledForCurrentUser(fullName, packageName);
            }
            catch (Exception ex)
            {
                LogWriter.WriteExceptionToLog(ex);
                return false;
            }
        }

        private static bool IsPackageInstalledForCurrentUser(string fullName, string packageName)
        {
            try
            {
                var packageManager = new PackageManager();
                var userSecurityId = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
                var userPackages = packageManager.FindPackagesForUserWithPackageTypes(userSecurityId, PackageTypes.Main);
                bool installed = userPackages.Any(p => p.Id.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase) ||
                                                       p.Id.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
                if (installed) return true;
            }
            catch { }

            // Secondary check without explicit SID (queries calling user context)
            try
            {
                var packageManager = new PackageManager();
                var userPackages = packageManager.FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main);
                bool installed = userPackages.Any(p => p.Id.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase) ||
                                                       p.Id.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
                if (installed) return true;
            }
            catch { }

            // Double check via PowerShell
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"@(Get-AppxPackage -Name '*{packageName}*').Count\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                var outStr = p?.StandardOutput.ReadToEnd()?.Trim();
                p?.WaitForExit(5000);
                return int.TryParse(outStr, out var count) && count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static IEnumerable<App> QueryApps()
        {
            var packageManager = new PackageManager();
            var seenFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<Package> packages = null;
            try
            {
                packages = packageManager.FindPackagesWithPackageTypes(PackageTypes.Main);
            }
            catch
            {
                // Fall back to current user query if all-user query is not supported
            }

            if (packages == null)
            {
                var userSecurityId = WindowsIdentity.GetCurrent().User?.Value;
                packages = packageManager.FindPackagesForUserWithPackageTypes(userSecurityId, PackageTypes.Main);
            }

            foreach (var package in packages)
            {
                if (package.Status.Disabled || package.Status.NotAvailable)
                    continue;

                if (!seenFullNames.Add(package.Id.FullName))
                    continue;

                var result = TryCreateAppFromPackage(package);
                if (result != null)
                    yield return result;
            }
        }

        private static App TryCreateAppFromPackage(Package package)
        {
            var manifestContents = TryGetAppManifest(package);
            if (manifestContents == null) return null;
            try
            {
                var installPath = package.InstalledLocation.Path;
                var externalPath = package.EffectiveLocation.Path.Equals(installPath, StringComparison.OrdinalIgnoreCase) ? null : package.EffectiveLocation.Path;

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(manifestContents);
                // namespaces are mandatory, even if there's a default namespace
                var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("ns", xmlDoc.DocumentElement!.NamespaceURI);
                var properties = xmlDoc.DocumentElement.SelectSingleNode("//ns:Properties", nsmgr);

                var displayNameRes = properties!.SelectSingleNode("ns:DisplayName/text()", nsmgr)?.Value;
                var displayNameExtracted = ExtractDisplayName(installPath, package.Id.Name, displayNameRes)
                                           ?? (externalPath != null ? ExtractDisplayName(externalPath, package.Id.Name, displayNameRes) : null);

                var logoPathRes = properties.SelectSingleNode("ns:Logo/text()", nsmgr)?.Value;
                var logoPathExtracted = ExtractDisplayIcon(installPath, logoPathRes)
                                         ?? (externalPath != null ? ExtractDisplayIcon(externalPath, logoPathRes) : null);

                var publisherDisplayNameRes = properties.SelectSingleNode("ns:PublisherDisplayName/text()", nsmgr)?.Value;
                var publisherDisplayNameExtracted = ExtractDisplayName(installPath, package.Id.Name, publisherDisplayNameRes)
                                           ?? (externalPath != null ? ExtractDisplayName(externalPath, package.Id.Name, publisherDisplayNameRes) : null);

                return new App(
                    fullName: package.Id.FullName,
                    displayName: FirstValidName(displayNameExtracted, displayNameRes, package.DisplayName) ?? package.InstalledLocation.DisplayName,
                    publisherDisplayName: FirstValidName(publisherDisplayNameExtracted, package.PublisherDisplayName) ?? "",
                    logo: logoPathExtracted,
                    installedLocation: installPath,
                    isProtected: EssentialSystemPackages.Contains(package.Id.Name));
            }
            catch (SystemException exception)
            {
                LogWriter.WriteExceptionToLog(exception);
                return null;
            }
        }

        private static string FirstValidName(params string[] names)
        {
            return names.FirstOrDefault(s => !string.IsNullOrEmpty(s) && !s.StartsWith("ms-resource:"));
        }

        private static string TryGetAppManifest(Package package)
        {
            try
            {
                var file = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                if (!File.Exists(file)) return null;
                var manifestContents = File.ReadAllText(file);
                return string.IsNullOrWhiteSpace(manifestContents) ? null : manifestContents;
            }
            catch (SystemException exception)
            {
                LogWriter.WriteExceptionToLog(exception);
                return null;
            }
        }

        private static string ExtractDisplayIcon(string appDir, string iconDir)
        {
            var logo = Path.Combine(appDir, iconDir);
            if (File.Exists(logo))
                return logo;

            logo = Path.Combine(appDir, Path.ChangeExtension(logo, "scale-100.png"));
            if (File.Exists(logo))
                return logo;

            var localized = Path.Combine(Path.Combine(appDir, "en-us"), iconDir);
            localized = Path.Combine(appDir, Path.ChangeExtension(localized, "scale-100.png"));
            return File.Exists(localized) ? localized : null;
        }

        /// <summary>
        ///     Grabs display name from resources if necessary.
        /// </summary>
        /// <param name="appDir">package.InstalledLocation.Path</param>
        /// <param name="packageName">Package.Id.Name</param>
        /// <param name="displayName">Application.VisualElements.DisplayName</param>
        private static string ExtractDisplayName(string appDir, string packageName, string displayName)
        {
            if (!Uri.TryCreate(displayName, UriKind.Absolute, out var uri))
                return displayName;

            var priPath = Path.Combine(appDir, "resources.pri");
            var resource = $"ms-resource://{packageName}/resources/{uri.Segments.Last()}";
            var name = NativeMethods.ExtractStringFromPriFile(priPath, resource)?.Trim();
            if (!string.IsNullOrEmpty(name))
                return name;

            var res = string.Concat(uri.Segments.Skip(1));
            resource = $"ms-resource://{packageName}/{res}";
            name = NativeMethods.ExtractStringFromPriFile(priPath, resource)?.Trim();
            if (!string.IsNullOrEmpty(name))
                return name;

            name = NativeMethods.ExtractStringFromPriFile(priPath, displayName)?.Trim();
            if (!string.IsNullOrEmpty(name))
                return name;

            return null;
        }

        private static class NativeMethods
        {
            [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true,
                SetLastError = false, ThrowOnUnmappableChar = true)]
            private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf,
                IntPtr ppvReserved);

            internal static string ExtractStringFromPriFile(string pathToPri, string resourceKey)
            {
                var sWin8ManifestString = $"@{{{pathToPri}? {resourceKey}}}";
                var outBuff = new StringBuilder(1024);
                SHLoadIndirectString(sWin8ManifestString, outBuff, outBuff.Capacity, IntPtr.Zero);
                return outBuff.ToString();
            }
        }
    }
}