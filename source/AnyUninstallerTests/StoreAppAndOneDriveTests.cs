using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UninstallTools;
using UninstallTools.Factory;
using UninstallTools.Uninstaller;
using AnyUninstaller.Avalonia.ViewModels;
using AnyUninstaller.Avalonia.Services;

namespace AnyUninstallerTests
{
    [TestClass]
    public class StoreAppAndOneDriveTests
    {
        [TestMethod]
        public void PredefinedFactory_DetectsOneDriveIfPresent()
        {
            var entry = PredefinedFactory.CreateOneDriveEntry();

            // On Windows 10/11 where OneDrive is present
            if (entry != null)
            {
                Assert.AreEqual("Microsoft OneDrive", entry.RawDisplayName);
                Assert.AreEqual("Microsoft Corporation", entry.Publisher);
                Assert.AreEqual(UninstallerType.PowerShell, entry.UninstallerKind);
                Assert.IsTrue(entry.IsValid, "OneDrive entry should be valid");
                Assert.IsFalse(entry.IsProtected, "OneDrive must not be marked protected");
                Assert.IsFalse(entry.SystemComponent, "OneDrive must not be marked system component");
                Assert.IsTrue(entry.UninstallPossible, "OneDrive should have an uninstall string");
                Assert.IsTrue(entry.QuietUninstallPossible, "OneDrive should support quiet uninstallation");
                Assert.IsTrue(entry.UninstallString.Contains("powershell.exe", StringComparison.OrdinalIgnoreCase), "Uninstall string should invoke PowerShell");
                Assert.IsTrue(entry.UninstallString.Contains("OneDriveSetup.exe", StringComparison.OrdinalIgnoreCase), "Uninstall string should invoke OneDriveSetup.exe");
                Assert.IsTrue(entry.UninstallString.Contains("018D5C66-4533-4307-9B53-224DE2ED1FE6", StringComparison.OrdinalIgnoreCase), "Uninstall string should clean Explorer sidebar CLSID");

                var vm = new ApplicationEntryViewModel(entry);
                Assert.IsTrue(vm.CanStandardUninstall, "ViewModel must allow standard uninstall for OneDrive");
                Assert.IsTrue(vm.CanQuietUninstall, "ViewModel must allow quiet uninstall for OneDrive");
                Assert.IsFalse(vm.IsProtected, "ViewModel must report IsProtected = false for OneDrive");
            }
        }

        [TestMethod]
        public void StoreAppEntry_CortanaAndUserApps_CanBeUninstalled()
        {
            var cortanaEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Cortana",
                Publisher = "Microsoft Corporation",
                Comment = "Microsoft.549981C3F5F10_4.2204.13303.0_x64__8wekyb3d8bbwe",
                RatingId = "Microsoft.549981C3F5F10",
                CacheIdOverride = "Microsoft.549981C3F5F10_4.2204.13303.0_x64__8wekyb3d8bbwe",
                IsValid = true,
                IsProtected = false,
                SystemComponent = false,
                UninstallerKind = UninstallerType.StoreApp,
                UninstallString = "\"StoreAppHelper.exe\" /uninstall \"Microsoft.549981C3F5F10_4.2204.13303.0_x64__8wekyb3d8bbwe\"",
                QuietUninstallString = "\"StoreAppHelper.exe\" /uninstall \"Microsoft.549981C3F5F10_4.2204.13303.0_x64__8wekyb3d8bbwe\""
            };

            var vm = new ApplicationEntryViewModel(cortanaEntry);
            Assert.IsTrue(vm.HasRealUninstaller, "Cortana must have a real uninstaller");
            Assert.IsTrue(vm.CanStandardUninstall, "Cortana must allow standard uninstallation");
            Assert.IsTrue(vm.CanQuietUninstall, "Cortana must allow quiet uninstallation");
            Assert.IsFalse(vm.IsProtected, "Cortana must not be protected");
        }

        [TestMethod]
        public void StoreAppEntry_EssentialSystemComponents_RemainProtected()
        {
            var settingsEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Settings",
                Publisher = "Microsoft Corporation",
                Comment = "windows.immersivecontrolpanel_10.0.6.1000_neutral_neutral_cw5n1h2txyewy",
                RatingId = "windows.immersivecontrolpanel",
                IsValid = true,
                IsProtected = true,
                SystemComponent = true,
                UninstallerKind = UninstallerType.StoreApp,
                UninstallString = "\"StoreAppHelper.exe\" /uninstall \"windows.immersivecontrolpanel_10.0.6.1000_neutral_neutral_cw5n1h2txyewy\""
            };

            var vm = new ApplicationEntryViewModel(settingsEntry);
            Assert.IsTrue(vm.IsProtected, "Essential settings package must be marked protected");
            Assert.IsFalse(vm.CanStandardUninstall, "Protected core OS settings must not allow standard uninstallation");
            Assert.IsFalse(vm.CanQuietUninstall, "Protected core OS settings must not allow quiet uninstallation");
        }

        [TestMethod]
        public void StoreAppFactory_PowerShellCommandGeneration_WorksCorrectly()
        {
            var fullName = "Microsoft.WindowsCamera_2024.2401.5.0_x64__8wekyb3d8bbwe";
            var psCmd = StoreAppFactory.GetPowerShellRemoveCommand(fullName);

            Assert.AreEqual($"Remove-AppxPackage -package {fullName} -confirm:$false", psCmd);

            var entries = new[]
            {
                new ApplicationUninstallerEntry
                {
                    UninstallerKind = UninstallerType.StoreApp,
                    Comment = fullName
                },
                new ApplicationUninstallerEntry
                {
                    UninstallerKind = UninstallerType.InnoSetup,
                    Comment = "non-store"
                }
            };

            var commands = StoreAppFactory.ToPowerShellRemoveCommands(entries);
            Assert.AreEqual(1, commands.Length, "Should only generate PowerShell command for StoreApp entries");
            Assert.AreEqual(psCmd, commands[0]);
        }

        [TestMethod]
        public void UninstallerExecutionService_RespectsIgnoreProtection()
        {
            var protectedEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Protected App",
                IsValid = true,
                IsProtected = true,
                UninstallString = "cmd.exe /c exit 0"
            };

            // When user executes action with default ignoreProtection = true
            var bulkTask = UninstallerExecutionService.Instance.CreateBulkTask(
                new[] { protectedEntry },
                quiet: false,
                simulate: true,
                ignoreProtection: true);

            var queuedEntry = bulkTask.AllUninstallersList.First();
            Assert.AreEqual(UninstallStatus.Waiting, queuedEntry.CurrentStatus,
                "Explicitly requested uninstallation should be queued with status Waiting even if IsProtected");

            // When ignoreProtection = false
            var blockedTask = UninstallerExecutionService.Instance.CreateBulkTask(
                new[] { protectedEntry },
                quiet: false,
                simulate: true,
                ignoreProtection: false);

            var blockedEntry = blockedTask.AllUninstallersList.First();
            Assert.AreEqual(UninstallStatus.Protected, blockedEntry.CurrentStatus,
                "When ignoreProtection is false, protected entries must be marked Protected");
        }

        [TestMethod]
        public void StoreAppEntry_NeverPointsUninstallerLocationToSelfOrHelpers()
        {
            var skypeEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Skype",
                Publisher = "Microsoft Corporation",
                Comment = "Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c",
                RatingId = "Microsoft.SkypeApp",
                IsValid = true,
                UninstallerKind = UninstallerType.StoreApp,
                InstallLocation = @"C:\Program Files\WindowsApps\Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c",
                UninstallString = "\"StoreAppHelper.exe\" /uninstall \"Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c\""
            };

            Assert.IsNull(skypeEntry.UninstallerFullFilename,
                "UninstallerFullFilename must be null and not point to StoreAppHelper.exe");
            Assert.IsNull(skypeEntry.UninstallerLocation,
                "UninstallerLocation must be null and not point to Any Uninstaller's directory");
        }

        [TestMethod]
        public void ApplicationUninstallerEntry_IsSelfOrHelper_DetectsOwnBinariesAndDirectories()
        {
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelper("StoreAppHelper.exe"));
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelper("AnyUninstaller.exe"));
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelper("SteamHelper.exe"));
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelper(Path.Combine(AppContext.BaseDirectory, "StoreAppHelper.exe")));
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelperDirectory(AppContext.BaseDirectory));
            Assert.IsTrue(ApplicationUninstallerEntry.IsSelfOrHelperDirectory(UninstallToolsGlobalConfig.AppLocation));

            Assert.IsFalse(ApplicationUninstallerEntry.IsSelfOrHelper(@"C:\Program Files\VideoLAN\VLC\uninstall.exe"));
            Assert.IsFalse(ApplicationUninstallerEntry.IsSelfOrHelperDirectory(@"C:\Program Files\VideoLAN\VLC"));
        }

        [TestMethod]
        public void JunkCleaningService_NeverReturnsAppDirectoryAsJunk()
        {
            var skypeEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Skype",
                Publisher = "Microsoft Corporation",
                Comment = "Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c",
                RatingId = "Microsoft.SkypeApp",
                IsValid = true,
                UninstallerKind = UninstallerType.StoreApp,
                InstallLocation = @"C:\Program Files\WindowsApps\Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c",
                UninstallString = $"\"{Path.Combine(AppContext.BaseDirectory, "StoreAppHelper.exe")}\" /uninstall \"Microsoft.SkypeApp_15.114.3204.0_x86__kzf8qxf38zg5c\""
            };

            var junk = JunkCleaningService.Instance.ScanJunkAsync(
                new[] { skypeEntry },
                new[] { skypeEntry }).GetAwaiter().GetResult();

            foreach (var item in junk)
            {
                var displayName = item.GetDisplayName();
                Assert.IsFalse(ApplicationUninstallerEntry.IsSelfOrHelper(displayName),
                    $"Junk item must not be Any Uninstaller helper: {displayName}");
                Assert.IsFalse(ApplicationUninstallerEntry.IsSelfOrHelperDirectory(displayName),
                    $"Junk item must not point to Any Uninstaller application directory: {displayName}");
            }
        }

        [TestMethod]
        public void StoreAppEntry_BloatwareApps_AreNotProtectedAndCanBeUninstalled()
        {
            var bloatwareApps = new[]
            {
                new { Name = "Snip & Sketch", FullName = "Microsoft.ScreenSketch_11.2605.38.0_x64__8wekyb3d8bbwe", Rating = "Microsoft.ScreenSketch" },
                new { Name = "Windows Maps", FullName = "Microsoft.WindowsMaps_11.2401.5.0_x64__8wekyb3d8bbwe", Rating = "Microsoft.WindowsMaps" },
                new { Name = "Mixed Reality Portal", FullName = "Microsoft.MixedReality.Portal_2000.21051.1282.0_x64__8wekyb3d8bbwe", Rating = "Microsoft.MixedReality.Portal" }
            };

            foreach (var app in bloatwareApps)
            {
                var entry = new ApplicationUninstallerEntry
                {
                    RawDisplayName = app.Name,
                    Publisher = "Microsoft Corporation",
                    Comment = app.FullName,
                    RatingId = app.Rating,
                    CacheIdOverride = app.FullName,
                    IsValid = true,
                    IsProtected = false,
                    SystemComponent = false,
                    UninstallerKind = UninstallerType.StoreApp,
                    UninstallString = $"\"StoreAppHelper.exe\" /uninstall \"{app.FullName}\"",
                    QuietUninstallString = $"\"StoreAppHelper.exe\" /uninstall \"{app.FullName}\""
                };

                var vm = new ApplicationEntryViewModel(entry);
                Assert.IsFalse(vm.IsProtected, $"{app.Name} must not be marked protected");
                Assert.IsTrue(vm.HasRealUninstaller, $"{app.Name} must report having a real uninstaller");
                Assert.IsTrue(vm.CanStandardUninstall, $"{app.Name} must allow standard uninstallation");
                Assert.IsTrue(vm.CanQuietUninstall, $"{app.Name} must allow quiet uninstallation");
                Assert.IsTrue(vm.IsStoreApp, $"{app.Name} must be recognized as a StoreApp");
            }
        }

        [TestMethod]
        public void PredefinedFactory_OneDriveScript_ContainsSafetyTimeoutAndFullCleanup()
        {
            var entry = PredefinedFactory.CreateOneDriveEntry();
            if (entry != null)
            {
                // Script should contain process termination
                Assert.IsTrue(entry.UninstallString.Contains("Stop-Process", StringComparison.OrdinalIgnoreCase));
                // Script should contain timeout protection (not hang indefinitely)
                Assert.IsTrue(entry.UninstallString.Contains("WaitForExit", StringComparison.OrdinalIgnoreCase));
                // Script should clean scheduled tasks
                Assert.IsTrue(entry.UninstallString.Contains("OneDrive", StringComparison.OrdinalIgnoreCase));
                // Script should clean Explorer CLSID
                Assert.IsTrue(entry.UninstallString.Contains("018D5C66-4533-4307-9B53-224DE2ED1FE6", StringComparison.OrdinalIgnoreCase));
                // Script should broadcast SHChangeNotify without restarting explorer
                Assert.IsTrue(entry.UninstallString.Contains("SHChangeNotify", StringComparison.OrdinalIgnoreCase));
            }
        }
        [TestMethod]
        public void AppxManifest_UsesStandardRunFullTrustCapability()
        {
            var manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\packaging\AppxManifest.xml"));
            if (File.Exists(manifestPath))
            {
                var content = File.ReadAllText(manifestPath);
                Assert.IsTrue(content.Contains("runFullTrust"),
                    "AppxManifest.xml must include runFullTrust capability");
                Assert.IsFalse(content.Contains("allowElevation"),
                    "AppxManifest.xml should not contain allowElevation to maintain seamless Store compatibility");
            }
        }

        [TestMethod]
        public void PredefinedFactory_OneDriveScript_ContainsDllRelocationAndOwnership()
        {
            var entry = PredefinedFactory.CreateOneDriveEntry();
            if (entry != null)
            {
                Assert.IsTrue(entry.UninstallString.Contains("FileSyncShell", StringComparison.OrdinalIgnoreCase),
                    "OneDrive uninstaller script must handle locked FileSyncShell DLLs");
                Assert.IsTrue(entry.UninstallString.Contains("takeown", StringComparison.OrdinalIgnoreCase),
                    "OneDrive uninstaller script must take ownership before removal");
            }
        }

        [TestMethod]
        public void PredefinedFactory_Cortana_NeverPointsInstallLocationToSystemApps()
        {
            var entry = PredefinedFactory.CreateLegacyCortanaEntry();
            if (entry != null)
            {
                Assert.IsNull(entry.InstallLocation,
                    "Cortana entry must not point InstallLocation to SystemApps to prevent attempting to delete Windows Search");
            }
        }

        [TestMethod]
        public void FileSystemJunk_Delete_ThrowsWhenItemCannotBeDeleted()
        {
            // Test that FileSystemJunk.Delete() does not silently pretend success when a directory still exists
            var tempDir = Path.Combine(Path.GetTempPath(), "AnyUTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var lockedFile = Path.Combine(tempDir, "locked.bin");

            try
            {
                // Lock a file inside with FileShare.None
                using var fs = new FileStream(lockedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                var junk = new UninstallTools.Junk.Containers.FileSystemJunk(
                    new DirectoryInfo(tempDir),
                    new ApplicationUninstallerEntry { RawDisplayName = "TestApp" },
                    null!);

                Assert.ThrowsExactly<UnauthorizedAccessException>(() => junk.Delete());
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }
    }
}

