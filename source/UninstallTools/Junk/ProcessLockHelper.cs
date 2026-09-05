using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace UninstallTools.Junk
{
    public class LockingProcessInfo : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private bool _shouldRestart = true;
        private string? _executablePath;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ApplicationDescription { get; set; } = string.Empty;

        public string? ExecutablePath
        {
            get => _executablePath ?? (IsExplorer ? GetDefaultExplorerPath() : null);
            set
            {
                _executablePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRestart));
                OnPropertyChanged(nameof(RestartStatusText));
            }
        }

        public string? ServiceName { get; set; }
        public string LockedPath { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShouldRestart
        {
            get => _shouldRestart;
            set
            {
                if (_shouldRestart != value)
                {
                    _shouldRestart = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RestartStatusText));
                }
            }
        }

        public bool IsUninstallTarget { get; set; } = false;

        public bool IsExplorer
        {
            get
            {
                if (ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                    ProcessName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                    ProcessName.IndexOf("explorer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ApplicationDescription.IndexOf("Windows Explorer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(_executablePath))
                {
                    try
                    {
                        var fn = Path.GetFileName(_executablePath);
                        if (fn.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
                }

                return false;
            }
        }

        private static string GetDefaultExplorerPath()
        {
            try
            {
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var p = Path.Combine(winDir, "explorer.exe");
                return File.Exists(p) ? p : "explorer.exe";
            }
            catch
            {
                return "explorer.exe";
            }
        }

        public bool CanRestart
        {
            get
            {
                if (IsExplorer) return true;
                if (!string.IsNullOrWhiteSpace(ServiceName)) return true;
                if (IsUninstallTarget) return false;
                if (!string.IsNullOrWhiteSpace(ExecutablePath))
                {
                    try { return File.Exists(ExecutablePath); }
                    catch { return false; }
                }
                return false;
            }
        }

        public string RestartStatusText
        {
            get
            {
                if (IsExplorer) return "Will restart Explorer";
                if (!string.IsNullOrWhiteSpace(ServiceName)) return "Will restart service";
                if (IsUninstallTarget) return "Close only (Uninstall target)";
                if (CanRestart) return ShouldRestart ? "Will restart" : "Do not restart";
                return "Close only";
            }
        }

        public string DisplayTitle => !string.IsNullOrWhiteSpace(ApplicationDescription)
            ? ApplicationDescription
            : (!string.IsNullOrWhiteSpace(ProcessName) ? ProcessName : $"PID: {ProcessId}");

        public string ProcessDetails => !string.IsNullOrWhiteSpace(ProcessName)
            ? $"{ProcessName} (PID: {ProcessId})"
            : $"PID: {ProcessId}";
    }

    /// <summary>
    /// Helper to detect and terminate processes locking specific directories or files using
    /// the Windows Restart Manager API and running process path analysis.
    /// </summary>
    public static class ProcessLockHelper
    {
        private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "system", "system idle process", "smss", "csrss", "wininit", "services", "lsass",
            "svchost", "fontdrvhost", "winlogon", "dwm", "registry", "memory compression",
            "explorer"
        };

        #region Win32 & Restart Manager Interop

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string strServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out IntPtr pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            IntPtr pSessionHandle,
            uint nFiles,
            string[]? rgsFilenames,
            uint nApplications,
            [In] RM_UNIQUE_PROCESS[]? rgApplications,
            uint nServices,
            string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
            IntPtr pSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
            out uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(IntPtr pSessionHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            int dwFlags,
            [Out] StringBuilder lpExeName,
            ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            int dwDesiredAccess,
            bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        private const int ERROR_MORE_DATA = 234;
        private const int ERROR_SUCCESS = 0;

        #endregion

        /// <summary>
        /// Attempts to get the full executable path of a process using QueryFullProcessImageName,
        /// which succeeds with PROCESS_QUERY_LIMITED_INFORMATION even when Process.MainModule throws Access Denied.
        /// </summary>
        public static string? TryGetProcessExecutablePath(int processId)
        {
            if (processId <= 4) return null;

            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            return sb.ToString();
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Accurately determines if the interactive Windows Shell (taskbar / desktop) is running and responsive.
        /// </summary>
        public static bool IsShellRunning()
        {
            try
            {
                // Check 1: Native Shell window handles (desktop and taskbar)
                IntPtr shellWnd = GetShellWindow();
                if (shellWnd != IntPtr.Zero)
                    return true;

                IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
                if (trayWnd != IntPtr.Zero)
                    return true;

                // Check 2: Running and responding explorer process in the current interactive session
                int currentSession = -1;
                try { currentSession = Process.GetCurrentProcess().SessionId; } catch { }

                var explorerProcs = Process.GetProcessesByName("explorer");
                try
                {
                    foreach (var proc in explorerProcs)
                    {
                        try
                        {
                            if ((currentSession == -1 || proc.SessionId == currentSession) && proc.Responding)
                            {
                                return true;
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    foreach (var proc in explorerProcs)
                    {
                        proc.Dispose();
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds all running processes that are either running an executable from within the specified paths,
        /// or hold an open file handle locking any file within the specified paths.
        /// </summary>
        public static List<LockingProcessInfo> FindLockingProcesses(IEnumerable<string> paths)
        {
            var results = new Dictionary<string, LockingProcessInfo>(StringComparer.OrdinalIgnoreCase);
            var currentPid = Process.GetCurrentProcess().Id;

            var validPaths = paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p =>
                {
                    try { return Path.GetFullPath(p.Trim().TrimEnd('\\', '/')); }
                    catch { return p.Trim(); }
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validPaths.Count == 0)
                return new List<LockingProcessInfo>();

            // Phase 1: Check running process executable locations against directory paths
            try
            {
                var runningProcesses = Process.GetProcesses();
                foreach (var proc in runningProcesses)
                {
                    using (proc)
                    {
                        if (proc.Id <= 4 || proc.Id == currentPid)
                            continue;

                        string? exePath = null;
                        try
                        {
                            exePath = proc.MainModule?.FileName;
                        }
                        catch
                        {
                            exePath = TryGetProcessExecutablePath(proc.Id);
                        }

                        if (string.IsNullOrEmpty(exePath))
                            continue;

                        string normExe;
                        try { normExe = Path.GetFullPath(exePath); }
                        catch { normExe = exePath; }

                        foreach (var targetPath in validPaths)
                        {
                            bool isInside = false;
                            if (Directory.Exists(targetPath))
                            {
                                if (normExe.StartsWith(targetPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(normExe, targetPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    isInside = true;
                                }
                            }
                            else if (File.Exists(targetPath))
                            {
                                if (string.Equals(normExe, targetPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    isInside = true;
                                }
                            }

                            if (isInside)
                            {
                                string key = $"{proc.Id}_{targetPath}";
                                if (!results.ContainsKey(key))
                                {
                                    string appDesc = string.Empty;
                                    try { appDesc = proc.MainModule?.FileVersionInfo.FileDescription ?? string.Empty; } catch { }

                                    results[key] = new LockingProcessInfo
                                    {
                                        ProcessId = proc.Id,
                                        ProcessName = proc.ProcessName,
                                        ApplicationDescription = !string.IsNullOrWhiteSpace(appDesc) ? appDesc : proc.ProcessName,
                                        ExecutablePath = normExe,
                                        LockedPath = targetPath,
                                        IsSelected = true,
                                        IsUninstallTarget = true,
                                        ShouldRestart = false
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Phase 2: Use Windows Restart Manager API on files and directory contents
            foreach (var targetPath in validPaths)
            {
                var filesToTest = new List<string>();

                if (File.Exists(targetPath))
                {
                    filesToTest.Add(targetPath);
                }
                else if (Directory.Exists(targetPath))
                {
                    try
                    {
                        // Enumerate existing files in the directory (limit to top 150 to keep it extremely fast)
                        var files = Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories)
                            .Take(150);
                        filesToTest.AddRange(files);
                    }
                    catch { }
                }

                if (filesToTest.Count == 0)
                    continue;

                QueryRestartManager(filesToTest, targetPath, currentPid, results);
            }

            // Filter out system critical processes
            return results.Values
                .Where(x => !IsCriticalProcess(x.ProcessName, x.ProcessId))
                .OrderBy(x => x.DisplayTitle)
                .ToList();
        }

        private static void QueryRestartManager(
            List<string> files,
            string originalTargetPath,
            int currentPid,
            Dictionary<string, LockingProcessInfo> results)
        {
            // Register in chunks of 64 files to avoid any API limits
            const int chunkSize = 64;
            for (int i = 0; i < files.Count; i += chunkSize)
            {
                var chunk = files.Skip(i).Take(chunkSize).ToArray();
                if (chunk.Length == 0) continue;

                string sessionKey = Guid.NewGuid().ToString();
                int res = RmStartSession(out IntPtr sessionHandle, 0, sessionKey);
                if (res != ERROR_SUCCESS)
                    continue;

                try
                {
                    res = RmRegisterResources(sessionHandle, (uint)chunk.Length, chunk, 0, null, 0, null);
                    if (res != ERROR_SUCCESS)
                        continue;

                    uint needed = 0;
                    uint count = 0;
                    uint reasons = 0;

                    res = RmGetList(sessionHandle, out needed, ref count, null, out reasons);
                    if (res == ERROR_MORE_DATA && needed > 0)
                    {
                        var processInfo = new RM_PROCESS_INFO[needed];
                        count = needed;
                        res = RmGetList(sessionHandle, out needed, ref count, processInfo, out reasons);

                        if (res == ERROR_SUCCESS)
                        {
                            for (int pIdx = 0; pIdx < count; pIdx++)
                            {
                                var info = processInfo[pIdx];
                                int pid = info.Process.dwProcessId;

                                if (pid <= 4 || pid == currentPid)
                                    continue;

                                string key = $"{pid}_{originalTargetPath}";
                                if (results.ContainsKey(key))
                                    continue;

                                string procName = info.strAppName;
                                string? exePath = null;
                                string appDesc = string.Empty;

                                try
                                {
                                    using var proc = Process.GetProcessById(pid);
                                    procName = proc.ProcessName;
                                    try
                                    {
                                        exePath = proc.MainModule?.FileName;
                                        appDesc = proc.MainModule?.FileVersionInfo.FileDescription ?? string.Empty;
                                    }
                                    catch
                                    {
                                        exePath = TryGetProcessExecutablePath(pid);
                                    }
                                }
                                catch
                                {
                                    exePath = TryGetProcessExecutablePath(pid);
                                }

                                if (string.IsNullOrWhiteSpace(appDesc) && !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                                {
                                    try
                                    {
                                        var fvi = FileVersionInfo.GetVersionInfo(exePath);
                                        appDesc = fvi.FileDescription ?? string.Empty;
                                    }
                                    catch { }
                                }

                                if (string.IsNullOrWhiteSpace(appDesc) && !string.IsNullOrWhiteSpace(info.strAppName))
                                {
                                    appDesc = info.strAppName;
                                }

                                bool isExplorer = procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                                                  procName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                                                  procName.IndexOf("explorer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  (!string.IsNullOrWhiteSpace(info.strAppName) && info.strAppName.IndexOf("Windows Explorer", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                                  (!string.IsNullOrEmpty(exePath) && Path.GetFileName(exePath).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase));

                                if (isExplorer)
                                {
                                    // Never treat Windows Explorer as a locking process to terminate during uninstalls.
                                    // Locked files will be handled gracefully via reboot scheduling without closing the shell.
                                    continue;
                                }

                                string svcName = info.strServiceShortName;
                                bool isInsideTarget = false;
                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    isInsideTarget = IsPathInsideOrEqual(exePath, originalTargetPath);
                                }

                                results[key] = new LockingProcessInfo
                                {
                                    ProcessId = pid,
                                    ProcessName = procName,
                                    ApplicationDescription = !string.IsNullOrWhiteSpace(appDesc) ? appDesc : procName,
                                    ExecutablePath = exePath,
                                    ServiceName = !string.IsNullOrWhiteSpace(svcName) ? svcName : null,
                                    LockedPath = originalTargetPath,
                                    IsSelected = true,
                                    IsUninstallTarget = isInsideTarget,
                                    ShouldRestart = !isInsideTarget && (!string.IsNullOrWhiteSpace(exePath) || !string.IsNullOrWhiteSpace(svcName))
                                };
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    RmEndSession(sessionHandle);
                }
            }
        }

        public static bool IsCriticalProcess(string processName, int processId)
        {
            if (processId <= 4 || processId == Process.GetCurrentProcess().Id)
                return true;

            if (string.IsNullOrWhiteSpace(processName))
            {
                try
                {
                    using var proc = Process.GetProcessById(processId);
                    processName = proc.ProcessName;
                }
                catch
                {
                    return false;
                }
            }

            var trimmed = processName.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = Path.GetFileNameWithoutExtension(trimmed);

            return CriticalProcessNames.Contains(trimmed);
        }

        /// <summary>
        /// Terminates a process by PID with process tree kill and elevated taskkill fallback.
        /// </summary>
        public static bool TerminateProcess(int processId, int timeoutMs = 3000)
        {
            if (IsCriticalProcess(string.Empty, processId))
                return false;

            try
            {
                using var proc = Process.GetProcessById(processId);
                if (IsCriticalProcess(proc.ProcessName, processId))
                    return false;
                try
                {
                    proc.Kill(entireProcessTree: true);
                    return proc.WaitForExit(timeoutMs);
                }
                catch (Win32Exception)
                {
                    // Access denied - fallback to taskkill
                    return RunTaskKill(processId, timeoutMs, elevated: true);
                }
            }
            catch (ArgumentException)
            {
                // Process has already terminated
                return true;
            }
            catch
            {
                return RunTaskKill(processId, timeoutMs, elevated: false);
            }
        }

        /// <summary>
        /// Terminates a collection of processes by PID and waits briefly for handles to release.
        /// </summary>
        public static int TerminateProcesses(IEnumerable<int> processIds, int timeoutMs = 3000)
        {
            int terminated = 0;
            var distinctPids = processIds.Distinct().ToList();

            foreach (var pid in distinctPids)
            {
                if (TerminateProcess(pid, timeoutMs))
                {
                    terminated++;
                }
            }

            if (terminated > 0)
            {
                // Give Windows a brief moment to close all file handles after processes terminate
                Thread.Sleep(600);
            }

            return terminated;
        }

        private static bool RunTaskKill(int processId, int timeoutMs, bool elevated)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /PID {processId}",
                    CreateNoWindow = true,
                    UseShellExecute = elevated,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (elevated)
                {
                    psi.Verb = "runas";
                }

                using var killProc = Process.Start(psi);
                killProc?.WaitForExit(timeoutMs);

                // Verify if process has exited
                try
                {
                    using var verifyProc = Process.GetProcessById(processId);
                    return verifyProc.HasExited;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restarts processes that were terminated to release file locks.
        /// Specifically ensures Windows Explorer is restarted if it was stopped,
        /// starts any stopped Windows services, and restarts external applications.
        /// </summary>
        public static int RestartProcesses(IEnumerable<LockingProcessInfo> processes)
        {
            int restarted = 0;

            var toRestart = processes
                .Where(p => p.ShouldRestart && p.CanRestart)
                .ToList();

            // 1. Revive Windows Explorer first if requested or if shell window is currently down
            bool explorerRequested = toRestart.Any(p => p.IsExplorer);
            if (explorerRequested || !IsShellRunning())
            {
                if (RestartExplorer(force: explorerRequested))
                {
                    if (explorerRequested)
                        restarted++;
                }
            }

            // 2. Restart services and external applications
            foreach (var proc in toRestart)
            {
                if (proc.IsExplorer)
                {
                    // Already handled above
                    continue;
                }

                // Windows Service
                if (!string.IsNullOrWhiteSpace(proc.ServiceName))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "net.exe",
                            Arguments = $"start \"{proc.ServiceName}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(3000);
                        restarted++;
                    }
                    catch { }
                    continue;
                }

                // Regular application
                if (!string.IsNullOrWhiteSpace(proc.ExecutablePath))
                {
                    try
                    {
                        if (File.Exists(proc.ExecutablePath))
                        {
                            bool appStarted = false;
                            string workingDir = Path.GetDirectoryName(proc.ExecutablePath) ?? string.Empty;

                            // Attempt 1: UseShellExecute = true
                            try
                            {
                                var psi = new ProcessStartInfo
                                {
                                    FileName = proc.ExecutablePath,
                                    UseShellExecute = true,
                                    WorkingDirectory = workingDir
                                };
                                Process.Start(psi);
                                appStarted = true;
                            }
                            catch { }

                            // Attempt 2: UseShellExecute = false (CreateProcess fallback)
                            if (!appStarted)
                            {
                                try
                                {
                                    var psi = new ProcessStartInfo
                                    {
                                        FileName = proc.ExecutablePath,
                                        UseShellExecute = false,
                                        WorkingDirectory = workingDir
                                    };
                                    Process.Start(psi);
                                    appStarted = true;
                                }
                                catch { }
                            }

                            if (appStarted)
                                restarted++;
                        }
                    }
                    catch { }
                }
            }

            // Safety fallback: ensure Windows Explorer taskbar/desktop shell is active
            try
            {
                if (!IsShellRunning())
                {
                    RestartExplorer(force: false);
                }
            }
            catch { }

            return restarted;
        }

        /// <summary>
        /// Ensures Windows Explorer and the Windows Shell (taskbar / desktop) is revived.
        /// </summary>
        /// <param name="force">If true, clears hung/zombie explorer processes and ensures a fresh shell launch.</param>
        public static bool RestartExplorer(bool force = false)
        {
            try
            {
                // If not forced and shell is already healthy and responsive, no need to relaunch (avoids opening an unwanted folder window)
                if (!force && IsShellRunning())
                {
                    return true;
                }

                // If forced or if existing explorer processes are hung, clean up lingering instances in current session
                if (force)
                {
                    try
                    {
                        int currentSession = -1;
                        try { currentSession = Process.GetCurrentProcess().SessionId; } catch { }

                        var existingExplorers = Process.GetProcessesByName("explorer");
                        foreach (var p in existingExplorers)
                        {
                            try
                            {
                                if (currentSession == -1 || p.SessionId == currentSession)
                                {
                                    p.Kill();
                                    p.WaitForExit(1000);
                                }
                            }
                            catch { }
                            finally
                            {
                                p.Dispose();
                            }
                        }
                    }
                    catch { }

                    // Brief wait for Windows to clean up shell desktop/tray resources
                    Thread.Sleep(400);
                }
                else
                {
                    // Brief wait in case explorer process was just told to exit
                    for (int i = 0; i < 5; i++)
                    {
                        if (IsShellRunning())
                        {
                            return true;
                        }
                        Thread.Sleep(100);
                    }
                }

                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string explorerPath = Path.Combine(winDir, "explorer.exe");
                if (!File.Exists(explorerPath)) explorerPath = "explorer.exe";

                // Tier 1: Start explorer directly with UseShellExecute = true and Windows working directory
                bool started = false;
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = explorerPath,
                        WorkingDirectory = winDir,
                        UseShellExecute = true
                    };
                    using var p = Process.Start(psi);
                    started = true;
                }
                catch { }

                // Check if shell came back up
                if (started)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(150);
                        if (IsShellRunning()) return true;
                    }
                }

                // Tier 2: CreateProcess directly (UseShellExecute = false)
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = explorerPath,
                        WorkingDirectory = winDir,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    started = true;
                }
                catch { }

                if (started)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(150);
                        if (IsShellRunning()) return true;
                    }
                }

                // Tier 3: Via cmd.exe start
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start \"\" \"{explorerPath}\"",
                        WorkingDirectory = winDir,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var p = Process.Start(psi);
                }
                catch { }

                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(150);
                    if (IsShellRunning()) return true;
                }

                // Tier 4: Via PowerShell Start-Process (decouples elevated parent context)
                if (!IsShellRunning())
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Start-Process explorer.exe\"",
                            WorkingDirectory = winDir,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var p = Process.Start(psi);
                    }
                    catch { }

                    for (int i = 0; i < 12; i++)
                    {
                        Thread.Sleep(150);
                        if (IsShellRunning()) return true;
                    }
                }

                return IsShellRunning();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if a file path is located inside or equals a directory path.
        /// </summary>
        public static bool IsPathInsideOrEqual(string childPath, string parentPath)
        {
            try
            {
                string normChild = Path.GetFullPath(childPath).TrimEnd('\\', '/');
                string normParent = Path.GetFullPath(parentPath).TrimEnd('\\', '/');

                if (string.Equals(normChild, normParent, StringComparison.OrdinalIgnoreCase))
                    return true;

                return normChild.StartsWith(normParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       normChild.StartsWith(normParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
