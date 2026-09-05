using Microsoft.VisualStudio.TestTools.UnitTesting;
using UninstallTools;

namespace AnyUninstallerTests
{
    [TestClass]
    public class ApplicationUninstallerEntryTests
    {
        [DataTestMethod]
        [DataRow("Tweak-TestEntry", true)]
        [DataRow("tweak-TestEntry", true)]
        [DataRow("Steam App 42", false)]
        [DataRow(null, false)]
        public void IsScriptTweak_MatchesCurrentTweakIdFormat(string ratingId, bool expected)
        {
            var entry = new ApplicationUninstallerEntry
            {
                RatingId = ratingId
            };

            Assert.AreEqual(expected, entry.IsScriptTweak);
        }

        [TestMethod]
        public void OrphanedEntry_HasNoRealUninstaller_CanOnlyManualUninstall()
        {
            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Test Orphan",
                IsOrphaned = true,
                InstallLocation = @"C:\Program Files\TestOrphan",
                UninstallString = @"cmd.exe /C del /S C:\Program Files\TestOrphan"
            };

            var vm = new AnyUninstaller.Avalonia.ViewModels.ApplicationEntryViewModel(entry);

            Assert.IsFalse(vm.HasRealUninstaller, "Orphaned entry should not report having a real uninstaller");
            Assert.IsFalse(vm.CanStandardUninstall, "Orphaned entry must not allow standard uninstallation");
            Assert.IsFalse(vm.CanQuietUninstall, "Orphaned entry must not allow quiet uninstallation");
            Assert.IsTrue(vm.CanManualUninstall, "Orphaned entry must allow manual uninstallation");
        }

        [TestMethod]
        public void SimpleDeleteEntry_HasNoRealUninstaller_CanOnlyManualUninstall()
        {
            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Unity Editor",
                UninstallerKind = UninstallerType.SimpleDelete,
                InstallLocation = @"C:\Program Files\Unity\Editor",
                UninstallString = @"UniversalUninstaller.exe C:\Program Files\Unity\Editor"
            };

            var vm = new AnyUninstaller.Avalonia.ViewModels.ApplicationEntryViewModel(entry);

            Assert.IsFalse(vm.HasRealUninstaller, "SimpleDelete entry should not report having a real uninstaller");
            Assert.IsFalse(vm.CanStandardUninstall, "SimpleDelete entry must not allow standard uninstallation");
            Assert.IsFalse(vm.CanQuietUninstall, "SimpleDelete entry must not allow quiet uninstallation");
            Assert.IsTrue(vm.CanManualUninstall, "SimpleDelete entry must allow manual uninstallation");
        }

        [TestMethod]
        public void ValidEntry_HasRealUninstaller_AllowsStandardUninstall()
        {
            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Valid Application",
                UninstallerKind = UninstallerType.InnoSetup,
                IsValid = true,
                UninstallString = @"C:\Program Files\App\unins000.exe",
                QuietUninstallString = @"C:\Program Files\App\unins000.exe /SILENT"
            };

            var vm = new AnyUninstaller.Avalonia.ViewModels.ApplicationEntryViewModel(entry);

            Assert.IsTrue(vm.HasRealUninstaller, "Valid entry should report having a real uninstaller");
            Assert.IsTrue(vm.CanStandardUninstall, "Valid entry must allow standard uninstallation");
            Assert.IsTrue(vm.CanQuietUninstall, "Valid entry must allow quiet uninstallation");
            Assert.IsTrue(vm.CanManualUninstall, "Valid entry must allow manual uninstallation");
        }

        [TestMethod]
        public void InvalidOrBrokenEntry_HasNoRealUninstaller_CanOnlyManualUninstall()
        {
            var entry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Broken Application",
                UninstallerKind = UninstallerType.InnoSetup,
                IsValid = false, // Broken / missing uninstaller file
                UninstallString = @"C:\Program Files\DeadApp\unins000.exe"
            };

            var vm = new AnyUninstaller.Avalonia.ViewModels.ApplicationEntryViewModel(entry);

            Assert.IsFalse(vm.HasRealUninstaller, "Invalid/broken entry should not report having a real uninstaller");
            Assert.IsFalse(vm.CanStandardUninstall, "Invalid/broken entry must not allow standard uninstallation");
            Assert.IsFalse(vm.CanQuietUninstall, "Invalid/broken entry must not allow quiet uninstallation");
            Assert.IsTrue(vm.CanManualUninstall, "Invalid/broken entry must allow manual uninstallation");
        }
    }
}
