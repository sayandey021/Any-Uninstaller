using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnyUninstaller.Avalonia.ViewModels;
using UninstallTools;
using UninstallTools.Junk;
using UninstallTools.Junk.Confidence;
using UninstallTools.Junk.Containers;

namespace AnyUninstallerTests
{
    [TestClass]
    public class CleanCompletionViewModelTests
    {
        private class DummyJunkResult : IJunkResult
        {
            public ApplicationUninstallerEntry? Application => null;
            public ConfidenceCollection Confidence { get; } = new ConfidenceCollection();
            public IJunkCreator? Source => null;

            public void Backup(string backupDirectory) { }
            public void Delete() { }
            public string GetDisplayName() => "Dummy Temp File";
            public void Open() { }
            public string ToLongString() => "Dummy Temp File";
        }

        [TestMethod]
        public void JunkRemovalViewModel_CompletionPropertiesAndToggles()
        {
            var dummy1 = new DummyJunkResult();
            var dummy2 = new DummyJunkResult();
            dummy1.Confidence.Add(5); // VeryGood (>= 5)
            dummy2.Confidence.Add(1); // Questionable (1)

            var vm = new JunkRemovalViewModel(new[] { dummy1, dummy2 });

            Assert.IsFalse(vm.IsCompleted);
            Assert.IsTrue(vm.IsActiveView);
            Assert.AreEqual(2, vm.RemainingTotalCount);
            Assert.IsTrue(vm.HasRemainingItems);
            Assert.IsTrue(vm.ShowRemainingButtonText.Contains("2"));

            // Toggle summary command
            vm.ShowCompletionSummaryCommand.Execute(null);
            Assert.IsTrue(vm.IsCompleted);
            Assert.IsFalse(vm.IsActiveView);

            // Toggle back to remaining items
            vm.ShowRemainingItemsCommand.Execute(null);
            Assert.IsFalse(vm.IsCompleted);
            Assert.IsTrue(vm.IsActiveView);
        }

        [TestMethod]
        public void JunkRemovalViewModel_CompletionTextFormatting()
        {
            var vm = new JunkRemovalViewModel(new List<IJunkResult>());

            vm.LastDeletedCount = 5;
            // 0 items remaining
            Assert.IsTrue(vm.CompletionHeadlineText.Contains("All Leftover Junk Cleaned"));
            Assert.IsTrue(vm.CompletionHighlightText.Contains("5"));

            // When items remain in list
            var dummy = new DummyJunkResult();
            var vmWithRemaining = new JunkRemovalViewModel(new[] { dummy });
            vmWithRemaining.LastDeletedCount = 5;
            vmWithRemaining.RemainingUndeletedCount = 1;
            vmWithRemaining.RemainingUnselectedCount = 0;

            Assert.IsTrue(vmWithRemaining.CompletionHeadlineText.Contains("Junk Cleanup Completed"));
            Assert.IsTrue(vmWithRemaining.CompletionHighlightText.Contains("5"));
            Assert.IsTrue(vmWithRemaining.CompletionDetailsText.Contains("5 leftover item(s)"));
            Assert.IsTrue(vmWithRemaining.CompletionDetailsText.Contains("locked or in use"));
        }

        [TestMethod]
        public void TempCleanerViewModel_CompletionPropertiesAndToggles()
        {
            var vm = new TempCleanerViewModel();

            Assert.IsFalse(vm.IsCompleted);
            Assert.IsTrue(vm.IsActiveView);

            // Simulate completion
            vm.LastDeletedCount = 12;
            vm.LastFreedSizeFormatted = "350 MB";
            vm.IsCompleted = true;
            Assert.IsFalse(vm.IsActiveView);

            Assert.IsTrue(vm.CompletionHeadlineText.Length > 0);
            Assert.IsTrue(vm.CompletionHighlightText.Contains("350 MB"));
            Assert.IsTrue(vm.CompletionHighlightText.Contains("12"));

            // Test toggling
            vm.ShowRemainingItemsCommand.Execute(null);
            Assert.IsFalse(vm.IsCompleted);
            Assert.IsTrue(vm.IsActiveView);

            vm.ShowCompletionSummaryCommand.Execute(null);
            Assert.IsTrue(vm.IsCompleted);
            Assert.IsFalse(vm.IsActiveView);
        }
    }
}
