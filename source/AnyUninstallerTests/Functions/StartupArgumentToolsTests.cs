using System;
using System.IO;
using AnyUninstaller.Functions.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnyUninstallerTests.Functions
{
    [TestClass]
    public class StartupArgumentToolsTests
    {
        [TestMethod]
        public void GetStartupUninstallListPath_ReturnsNullWhenThereAreNoArguments()
        {
            var result = StartupArgumentTools.GetStartupUninstallListPath(Array.Empty<string>());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetStartupUninstallListPath_ReturnsNullForNonAnyulArgument()
        {
            var result = StartupArgumentTools.GetStartupUninstallListPath(new[] { "AnyUninstaller.exe", "/setup" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetStartupUninstallListPath_ReturnsNullForMissingAnyulFile()
        {
            var result = StartupArgumentTools.GetStartupUninstallListPath(new[] { "AnyUninstaller.exe", @"C:\missing\Default.anyul" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetStartupUninstallListPath_ReturnsExistingAnyulPath()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".anyul");

            try
            {
                File.WriteAllText(tempPath, "<UninstallList />");

                var result = StartupArgumentTools.GetStartupUninstallListPath(new[] { "AnyUninstaller.exe", tempPath });

                Assert.AreEqual(tempPath, result);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
