using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstallerTests
{
    [TestClass]
    public class MainWindowEmptyStateTests
    {
        [TestMethod]
        public void MainWindowViewModel_EmptyStateInitialAndProperties()
        {
            var vm = new MainWindowViewModel();
            vm.StatusBar.IsBusy = false;

            // When no items in FilteredUninstallers and not busy
            Assert.IsTrue(vm.HasNoFilteredResults);
            Assert.IsFalse(vm.HasFilteredResults);
            Assert.IsFalse(vm.IsTreeMapVisibleAndHasItems);
            Assert.AreEqual("No applications found", vm.EmptyStateHeadline);
            Assert.IsTrue(vm.EmptyStateMessage.Contains("No installed applications were detected"));

            // When user types a search query that yields no results
            vm.Sidebar.SearchText = "film";
            Assert.IsTrue(vm.HasSearchText);
            Assert.AreEqual("No matching applications found", vm.EmptyStateHeadline);
            Assert.IsTrue(vm.EmptyStateMessage.Contains("film"));

            // Clear search command resets search text
            vm.ClearSearchCommand.Execute(null);
            Assert.IsFalse(vm.HasSearchText);
            Assert.AreEqual(string.Empty, vm.Sidebar.SearchText);
        }

        [TestMethod]
        public void MainWindowViewModel_ResetFiltersCommand_ClearsFiltersAndSearch()
        {
            var vm = new MainWindowViewModel();
            vm.Sidebar.SearchText = "test_query";
            vm.Sidebar.ShowDesktopApps = false;
            vm.Sidebar.ShowStoreApps = false;

            vm.ResetAllFiltersCommand.Execute(null);

            Assert.AreEqual(string.Empty, vm.Sidebar.SearchText);
            Assert.IsTrue(vm.Sidebar.ShowDesktopApps);
            Assert.IsTrue(vm.Sidebar.ShowStoreApps);
        }
    }
}
