using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Klocman.Tools;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class TargetWindow : Window
    {
        public List<string> TargetPaths { get; } = new();

        private DispatcherTimer? _trackingTimer;
        private uint _lastTargetPid;
        private string? _lastTargetTitle;
        private string? _lastTargetProcName;
        private bool _isTracking;
        private IntPtr _crossCursor = IntPtr.Zero;

        private const int VK_LBUTTON = 0x01;
        private const int VK_ESCAPE = 0x1B;
        private const int IDC_ARROW = 32512;
        private const int IDC_CROSS = 32515;
        private const uint GA_ROOT = 2;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public TargetWindow()
        {
            InitializeComponent();
            _crossCursor = LoadCursor(IntPtr.Zero, IDC_CROSS);
        }

        private void OnCrosshairPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                e.Pointer.Capture(CrosshairTargetArea);
                StartWindowTracking();
            }
        }

        private void OnCrosshairPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isTracking && _crossCursor != IntPtr.Zero)
            {
                SetCursor(_crossCursor);
            }
        }

        private void OnCrosshairPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isTracking)
            {
                e.Pointer.Capture(null);
                FinishTracking();
            }
        }

        private void StartWindowTracking()
        {
            _isTracking = true;
            _lastTargetPid = 0;
            _lastTargetTitle = null;
            _lastTargetProcName = null;

            // Keep TargetWindow on top and slightly transparent so the desktop is visible
            Topmost = true;
            Opacity = 0.90;

            // Capture mouse at Win32 level so crosshair cursor and mouse events work across all windows
            try
            {
                var platformHandle = this.TryGetPlatformHandle();
                if (platformHandle != null && platformHandle.Handle != IntPtr.Zero)
                {
                    SetCapture(platformHandle.Handle);
                }
            }
            catch { }

            if (_crossCursor != IntPtr.Zero)
            {
                SetCursor(_crossCursor);
            }

            if (TargetStateText != null)
            {
                TargetStateText.Text = "Aiming...";
                TargetStateText.Foreground = Brushes.Orange;
            }

            if (TargetWindowTitleText != null)
                TargetWindowTitleText.Text = "Hover over any application window...";

            if (TargetProcessText != null)
                TargetProcessText.Text = "Release mouse over window to select";

            StatusTextBlock.Text = "🎯 Dragging target... Release over target window (ESC to cancel)";

            _trackingTimer?.Stop();
            _trackingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            _trackingTimer.Tick += OnTrackingTimerTick;
            _trackingTimer.Start();
        }

        private string? _lastExplorerFolder;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        private static string? TryGetExplorerWindowFolderPath(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return null;

                dynamic? windows = shell.Windows();
                if (windows == null) return null;

                foreach (dynamic win in windows)
                {
                    try
                    {
                        long winHwnd = (long)win.HWND;
                        if (winHwnd == (long)hWnd || IsChild(new IntPtr(winHwnd), hWnd))
                        {
                            string? url = win.LocationURL;
                            if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.IsFile)
                            {
                                var localPath = uri.LocalPath;
                                if (Directory.Exists(localPath))
                                    return localPath;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private void OnTrackingTimerTick(object? sender, EventArgs e)
        {
            if (!_isTracking) return;

            // 1. Force the Win32 cursor to the crosshair target icon while dragging
            if (_crossCursor != IntPtr.Zero)
            {
                SetCursor(_crossCursor);
            }

            // 2. Check for ESC key to cancel
            if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                CancelTracking();
                return;
            }

            // 3. Track window directly under the cursor
            if (GetCursorPos(out var pt))
            {
                var hWnd = WindowFromPoint(pt);
                if (hWnd != IntPtr.Zero)
                {
                    var rootHWnd = GetAncestor(hWnd, GA_ROOT);
                    var targetHWnd = rootHWnd != IntPtr.Zero ? rootHWnd : hWnd;

                    GetWindowThreadProcessId(targetHWnd, out var pid);
                    uint currentPid = (uint)Process.GetCurrentProcess().Id;

                    if (pid > 0 && pid != currentPid)
                    {
                        _lastTargetPid = pid;

                        var titleBuilder = new StringBuilder(256);
                        GetWindowText(targetHWnd, titleBuilder, titleBuilder.Capacity);
                        _lastTargetTitle = titleBuilder.ToString();

                        try
                        {
                            using var p = Process.GetProcessById((int)pid);
                            _lastTargetProcName = p.ProcessName;
                        }
                        catch
                        {
                            _lastTargetProcName = $"PID {pid}";
                        }

                        // Special handling for Windows Explorer windows: detect the open directory folder!
                        if (string.Equals(_lastTargetProcName, "explorer", StringComparison.OrdinalIgnoreCase))
                        {
                            var openFolder = TryGetExplorerWindowFolderPath(targetHWnd) ?? TryGetExplorerWindowFolderPath(hWnd);
                            if (!string.IsNullOrEmpty(openFolder))
                            {
                                _lastExplorerFolder = openFolder;
                                if (TargetWindowTitleText != null)
                                    TargetWindowTitleText.Text = $"Folder: {Path.GetFileName(openFolder.TrimEnd('\\', '/'))}";
                                if (TargetProcessText != null)
                                    TargetProcessText.Text = openFolder;
                            }
                            else
                            {
                                _lastExplorerFolder = null;
                                if (TargetWindowTitleText != null)
                                    TargetWindowTitleText.Text = "Windows Explorer / Desktop";
                                if (TargetProcessText != null)
                                    TargetProcessText.Text = "System shell component (cannot be uninstalled)";
                            }
                        }
                        else
                        {
                            _lastExplorerFolder = null;
                            if (TargetWindowTitleText != null)
                            {
                                TargetWindowTitleText.Text = !string.IsNullOrWhiteSpace(_lastTargetTitle) 
                                    ? _lastTargetTitle 
                                    : $"({_lastTargetProcName})";
                            }

                            if (TargetProcessText != null)
                            {
                                TargetProcessText.Text = $"{_lastTargetProcName}.exe (PID {pid})";
                            }
                        }
                    }
                    else if (pid == currentPid)
                    {
                        if (TargetWindowTitleText != null)
                            TargetWindowTitleText.Text = "(Any Uninstaller — drag outside)";

                        if (TargetProcessText != null)
                            TargetProcessText.Text = "Aim at external application window";
                    }
                }
            }

            // 4. Check if the mouse button was released anywhere on screen
            short keyState = GetAsyncKeyState(VK_LBUTTON);
            bool isDown = (keyState & 0x8000) != 0;

            if (!isDown)
            {
                FinishTracking();
            }
        }

        private void FinishTracking()
        {
            _isTracking = false;
            _trackingTimer?.Stop();
            _trackingTimer = null;

            try
            {
                ReleaseCapture();
                SetCursor(LoadCursor(IntPtr.Zero, IDC_ARROW));
            }
            catch { }

            Topmost = false;
            Opacity = 1.0;

            if (TargetStateText != null)
            {
                TargetStateText.Text = "Ready";
                TargetStateText.Foreground = Brush.Parse("#3fb950");
            }

            if (_lastTargetPid > 0)
            {
                // If targeting Windows Explorer: check if a valid folder was targeted
                if (string.Equals(_lastTargetProcName, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(_lastExplorerFolder) && Directory.Exists(_lastExplorerFolder))
                    {
                        TargetPaths.Add(_lastExplorerFolder);
                        Close();
                        return;
                    }
                    else
                    {
                        StatusTextBlock.Text = "Windows Explorer / Desktop is a Windows component and cannot be uninstalled.";
                        return;
                    }
                }

                var exePath = GetProcessExecutablePath(_lastTargetPid);
                if (!string.IsNullOrEmpty(exePath) && (File.Exists(exePath) || Directory.Exists(exePath)))
                {
                    TargetPaths.Add(exePath);
                    Close();
                    return;
                }
                else
                {
                    StatusTextBlock.Text = $"Selected {_lastTargetProcName} (PID {_lastTargetPid}), but could not read executable path.";
                }
            }
            else
            {
                if (TargetWindowTitleText != null)
                    TargetWindowTitleText.Text = "Click & drag ⌖ icon over any window";

                if (TargetProcessText != null)
                    TargetProcessText.Text = "Release mouse over target to select";

                StatusTextBlock.Text = "No target window selected.";
            }
        }

        private void CancelTracking()
        {
            _isTracking = false;
            _trackingTimer?.Stop();
            _trackingTimer = null;

            try
            {
                ReleaseCapture();
                SetCursor(LoadCursor(IntPtr.Zero, IDC_ARROW));
            }
            catch { }

            Topmost = false;
            Opacity = 1.0;

            if (TargetStateText != null)
            {
                TargetStateText.Text = "Ready";
                TargetStateText.Foreground = Brush.Parse("#3fb950");
            }

            if (TargetWindowTitleText != null)
                TargetWindowTitleText.Text = "Click & drag ⌖ icon over any window";

            if (TargetProcessText != null)
                TargetProcessText.Text = "Release mouse over target to select";

            StatusTextBlock.Text = "Targeting cancelled.";
        }

        private static string? GetProcessExecutablePath(uint pid)
        {
            if (pid == 0) return null;

            // Strategy 1: QueryFullProcessImageName with PROCESS_QUERY_LIMITED_INFORMATION
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                    {
                        var path = sb.ToString();
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }

            // Strategy 2: QueryFullProcessImageName with PROCESS_QUERY_INFORMATION (0x0400)
            hProc = OpenProcess(0x0400, false, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                    {
                        var path = sb.ToString();
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }

            // Strategy 3: Fallback to Process.MainModule
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                var fn = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(fn)) return fn;
            }
            catch { }

            // Strategy 4: WMI Query fallback for 32/64-bit processes
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {pid}");
                foreach (var obj in searcher.Get())
                {
                    var p = obj["ExecutablePath"]?.ToString();
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }
            catch { }

            return null;
        }

        private async void OnSelectDirectoryClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Application Installation Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var folderPath = folders[0].Path.LocalPath;
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        TargetPaths.Add(folderPath);
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error selecting directory: {ex.Message}";
            }
        }

        private async void OnSelectFileClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Application File or Shortcut",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Applications & Shortcuts (*.exe, *.lnk)")
                        {
                            Patterns = new[] { "*.exe", "*.lnk", "*.bat", "*.cmd" }
                        },
                        new FilePickerFileType("All Files (*.*)")
                        {
                            Patterns = new[] { "*.*" }
                        }
                    }
                });

                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var resolved = WindowsTools.ResolveShortcut(filePath);
                                if (!string.IsNullOrEmpty(resolved) && (File.Exists(resolved) || Directory.Exists(resolved)))
                                {
                                    filePath = resolved;
                                }
                            }
                            catch { }
                        }

                        TargetPaths.Add(filePath);
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error selecting file: {ex.Message}";
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _trackingTimer?.Stop();
            _trackingTimer = null;
            try
            {
                ReleaseCapture();
                SetCursor(LoadCursor(IntPtr.Zero, IDC_ARROW));
            }
            catch { }
            TargetPaths.Clear();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _trackingTimer?.Stop();
            _trackingTimer = null;
            try
            {
                ReleaseCapture();
                SetCursor(LoadCursor(IntPtr.Zero, IDC_ARROW));
            }
            catch { }
            base.OnClosed(e);
        }
    }
}
