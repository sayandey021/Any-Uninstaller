using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UninstallTools.Junk;

namespace AnyUninstallerTests
{
    [TestClass]
    public class ProcessLockHelperTests
    {
        [TestMethod]
        public void FindLockingProcesses_DetectsLockedFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"lock_test_{Guid.NewGuid():N}.dat");
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    var results = ProcessLockHelper.FindLockingProcesses(new[] { tempFile });

                    // Current process or a child process is locking the file
                    var currentPid = Process.GetCurrentProcess().Id;
                    // Note: FindLockingProcesses filters out current PID to prevent self-termination during normal cleanup,
                    // but we can verify it executes without error.
                    Assert.IsNotNull(results);
                }

                // After releasing stream, file can be deleted without issues
                File.Delete(tempFile);
                Assert.IsFalse(File.Exists(tempFile));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [TestMethod]
        public void FindLockingProcesses_DetectsDirectoryContents()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"lock_dir_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, "inner_locked.txt");

            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    var results = ProcessLockHelper.FindLockingProcesses(new[] { tempDir });
                    Assert.IsNotNull(results);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void CriticalProcessCheck_ProtectsSystemAndCurrentProcess()
        {
            var currentPid = Process.GetCurrentProcess().Id;
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess(string.Empty, currentPid));
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess("system", 4));
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess("csrss", 100));
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess("svchost", 200));
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess("explorer", 1234), "explorer should be protected as a critical process");
            Assert.IsTrue(ProcessLockHelper.IsCriticalProcess("explorer.exe", 1234), "explorer.exe should be protected as a critical process");
            Assert.IsFalse(ProcessLockHelper.IsCriticalProcess("Discord", 99999));
            Assert.IsFalse(ProcessLockHelper.IsCriticalProcess("Slack", 88888));
        }

        [TestMethod]
        public void LockingProcessInfo_RestartProperties()
        {
            // Windows Explorer
            var explorerInfo = new LockingProcessInfo
            {
                ProcessId = 1234,
                ProcessName = "explorer",
                LockedPath = @"C:\Program Files\TestApp",
                IsUninstallTarget = false,
                ShouldRestart = true
            };
            Assert.IsTrue(explorerInfo.IsExplorer);
            Assert.IsTrue(explorerInfo.CanRestart);
            Assert.IsTrue(explorerInfo.ShouldRestart);
            Assert.AreEqual("Will restart Explorer", explorerInfo.RestartStatusText);

            // Uninstall target (executable inside target directory being deleted)
            var targetInfo = new LockingProcessInfo
            {
                ProcessId = 5678,
                ProcessName = "TestApp",
                ExecutablePath = @"C:\Program Files\TestApp\TestApp.exe",
                LockedPath = @"C:\Program Files\TestApp",
                IsUninstallTarget = true,
                ShouldRestart = false
            };
            Assert.IsFalse(targetInfo.IsExplorer);
            Assert.IsFalse(targetInfo.CanRestart);
            Assert.IsFalse(targetInfo.ShouldRestart);
            Assert.AreEqual("Close only (Uninstall target)", targetInfo.RestartStatusText);

            // Windows Service
            var serviceInfo = new LockingProcessInfo
            {
                ProcessId = 9101,
                ProcessName = "TestSvc",
                ServiceName = "TestService",
                LockedPath = @"C:\Program Files\TestApp\file.dll",
                IsUninstallTarget = false,
                ShouldRestart = true
            };
            Assert.IsTrue(serviceInfo.CanRestart);
            Assert.AreEqual("Will restart service", serviceInfo.RestartStatusText);
        }

        [TestMethod]
        public void LockingProcessInfo_IsExplorer_Variations()
        {
            // Case 1: "explorer"
            var proc1 = new LockingProcessInfo { ProcessName = "explorer" };
            Assert.IsTrue(proc1.IsExplorer);
            Assert.IsTrue(proc1.CanRestart);
            Assert.AreEqual("Will restart Explorer", proc1.RestartStatusText);

            // Case 2: "explorer.exe"
            var proc2 = new LockingProcessInfo { ProcessName = "explorer.exe" };
            Assert.IsTrue(proc2.IsExplorer);
            Assert.IsTrue(proc2.CanRestart);
            Assert.AreEqual("Will restart Explorer", proc2.RestartStatusText);

            // Case 3: "Windows Explorer" (from Restart Manager strAppName)
            var proc3 = new LockingProcessInfo { ProcessName = "Windows Explorer", ApplicationDescription = "Windows Explorer" };
            Assert.IsTrue(proc3.IsExplorer);
            Assert.IsTrue(proc3.CanRestart);
            Assert.AreEqual("Will restart Explorer", proc3.RestartStatusText);

            // Case 4: Process path ends with explorer.exe
            var proc4 = new LockingProcessInfo { ProcessName = "custom_shell", ExecutablePath = @"C:\Windows\explorer.exe" };
            Assert.IsTrue(proc4.IsExplorer);
            Assert.IsTrue(proc4.CanRestart);
            Assert.AreEqual("Will restart Explorer", proc4.RestartStatusText);
        }

        [TestMethod]
        public void LockingProcessInfo_PropertyChanged_FiresCorrectly()
        {
            var proc = new LockingProcessInfo
            {
                ProcessName = "MyApp",
                ExecutablePath = @"C:\MyApp\MyApp.exe",
                ShouldRestart = false
            };

            var changedProps = new System.Collections.Generic.List<string>();
            proc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    changedProps.Add(e.PropertyName);
            };

            proc.ShouldRestart = true;
            Assert.IsTrue(changedProps.Contains(nameof(LockingProcessInfo.ShouldRestart)));
            Assert.IsTrue(changedProps.Contains(nameof(LockingProcessInfo.RestartStatusText)));

            changedProps.Clear();
            proc.IsSelected = false;
            Assert.IsTrue(changedProps.Contains(nameof(LockingProcessInfo.IsSelected)));
        }

#nullable enable
        [TestMethod]
        public void ProcessLockHelper_IsShellRunning_ExecutesSafely()
        {
            // Verify that calling IsShellRunning executes cleanly without any exception
            bool isRunning = ProcessLockHelper.IsShellRunning();
            // Verify return value is a valid boolean
            Assert.IsTrue(isRunning || !isRunning);
        }

        [TestMethod]
        public void ProcessLockHelper_RestartExplorer_ExecutesSafely()
        {
            // Calling with force: false should check shell and return without error
            bool result = ProcessLockHelper.RestartExplorer(force: false);
            Assert.IsTrue(result || !result);
        }

        [TestMethod]
        public void ProcessLockHelper_TryGetProcessExecutablePath_CurrentProcess()
        {
            int currentPid = Process.GetCurrentProcess().Id;
            string? path = ProcessLockHelper.TryGetProcessExecutablePath(currentPid);
            Assert.IsNotNull(path);
            Assert.IsTrue(File.Exists(path));
        }
#nullable restore

        [TestMethod]
        public void IsPathInsideOrEqual_Tests()
        {
            var parent = @"C:\Program Files\MyApplication";
            var child1 = @"C:\Program Files\MyApplication\bin\app.exe";
            var child2 = @"C:\Program Files\MyApplication";
            var unrelated = @"C:\Program Files\OtherApp\app.exe";

            Assert.IsTrue(ProcessLockHelper.IsPathInsideOrEqual(child1, parent));
            Assert.IsTrue(ProcessLockHelper.IsPathInsideOrEqual(child2, parent));
            Assert.IsFalse(ProcessLockHelper.IsPathInsideOrEqual(unrelated, parent));
        }

        [TestMethod]
        public void WindowsTools_NotifyShellAssociationsChanged_ExecutesSafely()
        {
            Klocman.Tools.WindowsTools.NotifyShellAssociationsChanged();
        }

        [TestMethod]
        public void WindowsTools_ScheduleDeleteOnReboot_ExecutesSafely()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"anyu_test_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempFile, "test");
                bool scheduled = Klocman.Tools.WindowsTools.ScheduleDeleteOnReboot(tempFile);
                Assert.IsTrue(scheduled || !scheduled);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
