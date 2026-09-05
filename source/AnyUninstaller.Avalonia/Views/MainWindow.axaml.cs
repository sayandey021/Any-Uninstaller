using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AnyUninstaller.Avalonia.Services;
using AnyUninstaller.Avalonia.ViewModels;
using AnyUninstaller.Avalonia.Views.Dialogs;
using Klocman.Tools;
using UninstallTools.Factory;
using UninstallTools.Factory.InfoAdders;
using UninstallTools.Junk;
using UninstallTools.Uninstaller;

namespace AnyUninstaller.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        public MainWindow()
        {
            InitializeComponent();
            RestoreWindowSettings();
            DataContextChanged += OnDataContextChanged;
            ApplicationsDataGrid.AddHandler(PointerPressedEvent, OnDataGridPointerPressed, RoutingStrategies.Tunnel);
            ApplicationsDataGrid.SelectionChanged += OnApplicationsDataGridSelectionChanged;
            Closing += OnWindowClosing;
        }

        private void RestoreWindowSettings()
        {
            if (AppSettingsService.Instance.WindowWidth >= 600 && AppSettingsService.Instance.WindowHeight >= 400)
            {
                Width = AppSettingsService.Instance.WindowWidth;
                Height = AppSettingsService.Instance.WindowHeight;
            }

            if (AppSettingsService.Instance.IsWindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                AppSettingsService.Instance.WindowWidth = Bounds.Width;
                AppSettingsService.Instance.WindowHeight = Bounds.Height;
            }
            AppSettingsService.Instance.IsWindowMaximized = (WindowState == WindowState.Maximized);

            if (ViewModel != null)
            {
                AppSettingsService.Instance.IsToolbarVisible = ViewModel.IsToolbarVisible;
                AppSettingsService.Instance.IsStatusBarVisible = ViewModel.IsStatusBarVisible;
                AppSettingsService.Instance.ShowTreemap = ViewModel.IsTreeMapVisible;
                AppSettingsService.Instance.IsSidebarVisible = ViewModel.Sidebar.IsSidebarVisible;

                AppSettingsService.Instance.FilterShowDesktopApps = ViewModel.Sidebar.ShowDesktopApps;
                AppSettingsService.Instance.FilterShowStoreApps = ViewModel.Sidebar.ShowStoreApps;
                AppSettingsService.Instance.FilterShowGames = ViewModel.Sidebar.ShowGames;
                AppSettingsService.Instance.FilterShowSystemComponents = ViewModel.Sidebar.ShowSystemComponents;
                AppSettingsService.Instance.FilterShowUpdates = ViewModel.Sidebar.ShowUpdates;
                AppSettingsService.Instance.FilterShowWindowsFeatures = ViewModel.Sidebar.ShowWindowsFeatures;
                AppSettingsService.Instance.FilterShowProtected = ViewModel.Sidebar.ShowProtected;
                AppSettingsService.Instance.FilterShowOrphans = ViewModel.Sidebar.ShowOrphans;
                AppSettingsService.Instance.FilterShowInvalid = ViewModel.Sidebar.ShowInvalid;
                AppSettingsService.Instance.FilterShowVerified = ViewModel.Sidebar.ShowVerified;
                AppSettingsService.Instance.FilterShow64Bit = ViewModel.Sidebar.Show64Bit;
                AppSettingsService.Instance.FilterShow32Bit = ViewModel.Sidebar.Show32Bit;
                AppSettingsService.Instance.FilterSelectedSizeIndex = ViewModel.Sidebar.SelectedSizeFilterIndex;
                AppSettingsService.Instance.FilterSelectedDateIndex = ViewModel.Sidebar.SelectedDateFilterIndex;
                AppSettingsService.Instance.FilterShowOnlyQuiet = ViewModel.Sidebar.ShowOnlyQuiet;
                AppSettingsService.Instance.FilterShowOnlyStartup = ViewModel.Sidebar.ShowOnlyStartup;
                AppSettingsService.Instance.FilterShowSigned = ViewModel.Sidebar.ShowSigned;
                AppSettingsService.Instance.FilterShowUnsigned = ViewModel.Sidebar.ShowUnsigned;
            }

            AppSettingsService.Instance.Save();
        }

        private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(ApplicationsDataGrid);
            if (point.Properties.IsRightButtonPressed)
            {
                var visual = e.Source as Visual;
                var row = visual?.FindAncestorOfType<DataGridRow>();
                if (row?.DataContext is ApplicationEntryViewModel item)
                {
                    if (!ApplicationsDataGrid.SelectedItems.Contains(item))
                    {
                        ApplicationsDataGrid.SelectedItem = item;
                        if (ViewModel != null) ViewModel.SelectedItem = item;
                    }
                }
            }
        }

        private bool _isSyncingSelection;

        private void OnApplicationsDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSelection) return;

            try
            {
                _isSyncingSelection = true;

                // When user selects row(s), mark their checkboxes as checked
                foreach (var item in e.AddedItems.OfType<ApplicationEntryViewModel>())
                {
                    item.IsChecked = true;
                }

                // When row(s) are unselected (e.g. clicking another row without Ctrl)
                foreach (var item in e.RemovedItems.OfType<ApplicationEntryViewModel>())
                {
                    if (!ApplicationsDataGrid.SelectedItems.Contains(item))
                    {
                        item.IsChecked = false;
                    }
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private void OnContextMenuOpening(object? sender, CancelEventArgs e)
        {
            if (ApplicationsDataGrid.SelectedItem == null && ApplicationsDataGrid.SelectedItems.Count > 0)
            {
                ApplicationsDataGrid.SelectedItem = ApplicationsDataGrid.SelectedItems[0];
            }
            if (ViewModel != null && ApplicationsDataGrid.SelectedItem is ApplicationEntryViewModel item)
            {
                ViewModel.SelectedItem = item;
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.SelectedItem))
                    {
                        ScrollToSelectedItem(ViewModel.SelectedItem);
                    }
                };
            }
        }

        private void OnTreeMapItemClicked(object? sender, ApplicationEntryViewModel item)
        {
            if (item != null)
            {
                if (ViewModel != null)
                {
                    ViewModel.SelectedItem = item;
                }
                ScrollToSelectedItem(item);
            }
        }

        private void ScrollToSelectedItem(ApplicationEntryViewModel? item = null)
        {
            var target = item ?? ViewModel?.SelectedItem;
            if (target == null) return;

            // Ensure selection in DataGrid
            ApplicationsDataGrid.SelectedItem = target;

            var list = ViewModel?.FilteredUninstallers;
            if (list != null)
            {
                int index = list.IndexOf(target);
                if (index >= 0)
                {
                    ApplicationsDataGrid.SelectedIndex = index;
                    if (!ApplicationsDataGrid.SelectedItems.Contains(target))
                    {
                        ApplicationsDataGrid.SelectedItems.Clear();
                        ApplicationsDataGrid.SelectedItems.Add(target);
                    }

                    CenterItemInView(index, list.Count);

                    Dispatcher.UIThread.Post(() =>
                    {
                        CenterItemInView(index, list.Count);
                    }, DispatcherPriority.Loaded);
                }
            }
        }

        private void CenterItemInView(int index, int totalItems)
        {
            if (totalItems <= 0 || index < 0) return;

            var scrollViewer = ApplicationsDataGrid.FindDescendantOfType<ScrollViewer>();
            if (scrollViewer != null && scrollViewer.Extent.Height > 0 && scrollViewer.Viewport.Height > 0)
            {
                double rowHeight = scrollViewer.Extent.Height / totalItems;
                double targetY = (index * rowHeight) - (scrollViewer.Viewport.Height / 2.0) + (rowHeight / 2.0);
                double maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                double clampedY = Math.Clamp(targetY, 0, maxOffsetY);

                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clampedY);
            }
            else
            {
                if (ViewModel?.FilteredUninstallers != null && index < ViewModel.FilteredUninstallers.Count)
                {
                    ApplicationsDataGrid.ScrollIntoView(ViewModel.FilteredUninstallers[index], null);
                }
            }
        }

        private void OnMenuExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnUninstallClick(object? sender, RoutedEventArgs e)
        {
            await RunUninstallFlowAsync(quiet: false);
        }

        private async void OnQuietUninstallClick(object? sender, RoutedEventArgs e)
        {
            await RunUninstallFlowAsync(quiet: true);
        }

        private async void OnUninstallManuallyClick(object? sender, RoutedEventArgs e)
        {
            await RunManualUninstallFlowAsync();
        }

        private async Task RunManualUninstallFlowAsync()
        {
            if (ViewModel == null) return;

            var targets = ViewModel.GetSelectedOrCurrent();
            if (targets.Count == 0)
            {
                ViewModel.StatusBar.StatusMessage = "Please select at least one application to uninstall manually.";
                return;
            }

            var entryTargets = targets.Select(x => x.Entry).ToList();

            // Check if any of the target applications are actively running before scanning
            var targetPaths = entryTargets
                .SelectMany(t => new[] { t.InstallLocation, t.UninstallerLocation })
                .Where(p => !string.IsNullOrWhiteSpace(p) && (Directory.Exists(p) || File.Exists(p)))
                .Select(p => p!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetPaths.Count > 0)
            {
                var runningProcs = await Task.Run(() => ProcessLockHelper.FindLockingProcesses(targetPaths));
                if (runningProcs.Count > 0)
                {
                    var lockDialog = new ProcessLockDialog(runningProcs);
                    await lockDialog.ShowDialog(this);

                    if (lockDialog.Result == ProcessLockDialogResult.Cancel)
                    {
                        ViewModel.StatusBar.StatusMessage = "Manual uninstall cancelled.";
                        return;
                    }
                    else if (lockDialog.Result == ProcessLockDialogResult.EndProcessesAndDelete)
                    {
                        var pids = lockDialog.SelectedProcesses.Select(x => x.ProcessId).ToList();
                        var toRestart = lockDialog.SelectedProcesses.Where(x => x.ShouldRestart).ToList();
                        ViewModel.StatusBar.StatusMessage = "Closing running application processes...";
                        await Task.Run(() =>
                        {
                            ProcessLockHelper.TerminateProcesses(pids);
                            if (toRestart.Count > 0)
                            {
                                ProcessLockHelper.RestartProcesses(toRestart);
                            }
                        });
                    }
                }
            }

            ViewModel.StatusBar.IsBusy = true;
            ViewModel.StatusBar.ProgressValue = 0;
            ViewModel.StatusBar.ProgressMax = 100;
            ViewModel.StatusBar.StatusMessage = entryTargets.Count == 1
                ? $"Scanning for residual files and registry entries for {entryTargets[0].DisplayNameTrimmed}..."
                : $"Scanning for residual files and registry entries for {entryTargets.Count} selected applications...";

            try
            {
                var progress = new Progress<(int current, int total, string message)>(p =>
                {
                    if (ViewModel != null)
                    {
                        ViewModel.StatusBar.ProgressValue = p.current;
                        ViewModel.StatusBar.ProgressMax = Math.Max(p.total, 1);
                        if (!string.IsNullOrWhiteSpace(p.message))
                        {
                            ViewModel.StatusBar.StatusMessage = p.message;
                        }
                    }
                });

                var allEntries = ViewModel.FilteredUninstallers.Select(x => x.Entry).ToList();
                var junk = await JunkCleaningService.Instance.ScanJunkAsync(entryTargets, allEntries, progress);

                if (junk.Count > 0)
                {
                    var junkVm = new JunkRemovalViewModel(junk);
                    var junkWindow = new JunkRemoveWindow(junkVm);
                    await junkWindow.ShowDialog(this);

                    // Refresh application list after uninstallation/cleanup
                    await ViewModel.LoadApplicationsCommand.ExecuteAsync(null);
                }
                else
                {
                    ViewModel.StatusBar.StatusMessage = entryTargets.Count == 1
                        ? $"No residual files or registry keys found for {entryTargets[0].DisplayNameTrimmed}."
                        : "No residual files or registry keys found for selected applications.";

                    // Refresh application list in case the item was already deleted externally
                    await ViewModel.LoadApplicationsCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                ViewModel.StatusBar.StatusMessage = $"Manual uninstall error: {ex.Message}";
            }
            finally
            {
                ViewModel.StatusBar.IsBusy = false;
            }
        }

        private void OnMsiUninstallClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null || string.IsNullOrWhiteSpace(item.BundleProviderKey)) return;
            Process.Start(new ProcessStartInfo("msiexec.exe", $"/x {item.BundleProviderKey}") { UseShellExecute = true });
        }

        private void OnMsiQuietUninstallClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null || string.IsNullOrWhiteSpace(item.BundleProviderKey)) return;
            Process.Start(new ProcessStartInfo("msiexec.exe", $"/x {item.BundleProviderKey} /qn /norestart") { UseShellExecute = true });
        }

        private void OnMsiModifyClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null || string.IsNullOrWhiteSpace(item.BundleProviderKey)) return;
            Process.Start(new ProcessStartInfo("msiexec.exe", $"/i {item.BundleProviderKey}") { UseShellExecute = true });
        }

        private void OnRunUninstallerClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null || string.IsNullOrWhiteSpace(item.UninstallString)) return;
            try
            {
                var psi = ProcessTools.SeparateArgsFromCommand(item.UninstallString).ToProcessStartInfo();
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Run uninstaller error: {ex.Message}";
            }
        }

        private void OnRunQuietUninstallerClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null || string.IsNullOrWhiteSpace(item.QuietUninstallString)) return;
            try
            {
                var psi = ProcessTools.SeparateArgsFromCommand(item.QuietUninstallString).ToProcessStartInfo();
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Run quiet uninstaller error: {ex.Message}";
            }
        }

        private void OnRunAppExecutableClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(item.InstallLocation) && Directory.Exists(item.InstallLocation))
                {
                    var exes = Directory.GetFiles(item.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly);
                    if (exes.Length > 0)
                    {
                        Process.Start(new ProcessStartInfo(exes[0]) { UseShellExecute = true });
                        return;
                    }
                    Process.Start(new ProcessStartInfo(item.InstallLocation) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Launch error: {ex.Message}";
            }
        }

        private async void OnCopyNameClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                await Clipboard.SetTextAsync(item.DisplayName);
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Copied '{item.DisplayName}' to clipboard";
            }
        }

        private async void OnCopyGuidClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                await Clipboard.SetTextAsync(item.BundleProviderKey);
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Copied GUID / Product Code to clipboard";
            }
        }

        private async void OnCopyRegistryPathClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                await Clipboard.SetTextAsync(item.RegistryPath);
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Copied Registry Path to clipboard";
            }
        }

        private async void OnCopyInstallLocationClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                await Clipboard.SetTextAsync(item.InstallLocation);
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Copied Install Location to clipboard";
            }
        }

        private async void OnCopyUninstallStringClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                await Clipboard.SetTextAsync(item.UninstallString);
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Copied Uninstall String to clipboard";
            }
        }

        private async void OnCopyAllInfoClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && Clipboard != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Application: {item.DisplayName}");
                sb.AppendLine($"Publisher: {item.Publisher}");
                sb.AppendLine($"Version: {item.DisplayVersion}");
                sb.AppendLine($"Install Date: {item.InstallDate:yyyy-MM-dd}");
                sb.AppendLine($"Size: {item.EstimatedSize}");
                sb.AppendLine($"Architecture: {item.Architecture}");
                sb.AppendLine($"Uninstaller Kind: {item.UninstallerKind}");
                sb.AppendLine($"Status: {item.StatusDescription}");
                sb.AppendLine($"Install Location: {item.InstallLocation}");
                sb.AppendLine($"Uninstall String: {item.UninstallString}");
                sb.AppendLine($"Registry Path: {item.RegistryPath}");
                sb.AppendLine($"GUID: {item.BundleProviderKey}");
                await Clipboard.SetTextAsync(sb.ToString());
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Copied all properties to clipboard";
            }
        }

        private async void OnDeleteRegistryEntryClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null) return;

            try
            {
                if (!string.IsNullOrWhiteSpace(item.RegistryPath))
                {
                    try
                    {
                        RegistryTools.RemoveRegistryKey(item.RegistryPath);
                    }
                    catch
                    {
                        // Reg.exe fallback for protected/virtualized 64-bit and 32-bit registry keys
                        var proc = Process.Start(new ProcessStartInfo("reg.exe", $"delete \"{item.RegistryPath}\" /f")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        proc?.WaitForExit(3000);
                        if (proc == null || proc.ExitCode != 0)
                        {
                            // Prompt for elevation on-demand
                            Process.Start(new ProcessStartInfo("reg.exe", $"delete \"{item.RegistryPath}\" /f")
                            {
                                UseShellExecute = true,
                                Verb = "runas",
                                WindowStyle = ProcessWindowStyle.Hidden
                            })?.WaitForExit(5000);
                        }
                    }
                }

                if (ViewModel != null)
                {
                    ViewModel.StatusBar.StatusMessage = $"Deleted registry entry for '{item.DisplayName}'";
                    await ViewModel.LoadApplicationsCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Delete registry error: {ex.Message}";
            }
        }

        private async void OnRenameClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item == null) return;

            var renameDialog = new RenameWindow(item.DisplayName);
            await renameDialog.ShowDialog(this);

            if (renameDialog.Confirmed && !string.IsNullOrWhiteSpace(renameDialog.NewName))
            {
                try
                {
                    if (item.Entry.Rename(renameDialog.NewName))
                    {
                        if (ViewModel != null)
                        {
                            ViewModel.StatusBar.StatusMessage = $"Renamed entry to '{renameDialog.NewName}'";
                            await ViewModel.LoadApplicationsCommand.ExecuteAsync(null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Rename error: {ex.Message}";
                }
            }
        }

        private void OnOpenInstallLocationClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && !string.IsNullOrWhiteSpace(item.InstallLocation) && Directory.Exists(item.InstallLocation))
            {
                Process.Start(new ProcessStartInfo(item.InstallLocation) { UseShellExecute = true });
            }
        }

        private void OnOpenUninstallerLocationClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null)
            {
                if (!string.IsNullOrWhiteSpace(item.UninstallerFullFilename) && File.Exists(item.UninstallerFullFilename))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.UninstallerFullFilename}\"") { UseShellExecute = true });
                }
                else if (!string.IsNullOrWhiteSpace(item.UninstallerLocation) && Directory.Exists(item.UninstallerLocation))
                {
                    Process.Start(new ProcessStartInfo(item.UninstallerLocation) { UseShellExecute = true });
                }
            }
        }

        private void OnOpenInRegeditClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && !string.IsNullOrWhiteSpace(item.RegistryPath))
            {
                try
                {
                    RegistryTools.OpenRegKeyInRegedit(item.RegistryPath);
                }
                catch (Exception ex)
                {
                    if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Regedit open error: {ex.Message}";
                }
            }
        }

        private void OnOpenWebPageClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null && !string.IsNullOrWhiteSpace(item.AboutUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(item.AboutUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    if (ViewModel != null) ViewModel.StatusBar.StatusMessage = $"Web open error: {ex.Message}";
                }
            }
        }

        private void OnSearchGoogleClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null)
            {
                var query = Uri.EscapeDataString(item.DisplayName);
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
            }
        }

        private void OnSearchBingClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null)
            {
                var query = Uri.EscapeDataString(item.DisplayName);
                Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={query}") { UseShellExecute = true });
            }
        }

        private void OnSearchDuckDuckGoClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null)
            {
                var query = Uri.EscapeDataString(item.DisplayName);
                Process.Start(new ProcessStartInfo($"https://duckduckgo.com/?q={query}") { UseShellExecute = true });
            }
        }

        private void OnRateGoodClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Rating saved: 👍 Good / Recommended";
        }

        private void OnRateNeutralClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Rating saved: ⚠️ Neutral";
        }

        private void OnRateBadClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Rating saved: 👎 Bad / Bloatware";
        }

        private async void OnPropertiesClick(object? sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem ?? ViewModel?.GetSelectedOrCurrent().FirstOrDefault();
            if (item != null)
            {
                var propWindow = new PropertiesWindow(item);
                await propWindow.ShowDialog(this);
            }
        }

        private async Task RunUninstallFlowAsync(bool quiet)
        {
            if (ViewModel == null) return;

            var targets = ViewModel.GetSelectedOrCurrent();
            if (targets.Count == 0)
                return;

            // If all selected entries are orphaned, lack a real uninstaller, or are invalid/broken,
            // directly perform the manual uninstall flow so all residual files/registry keys are shown
            // in a checklist and nothing is auto-deleted.
            if (targets.All(x => !x.HasRealUninstaller))
            {
                await RunManualUninstallFlowAsync();
                return;
            }

            var realUninstallerTargets = targets.Where(x => x.HasRealUninstaller).Select(x => x.Entry).ToList();
            var manualTargets = targets.Where(x => !x.HasRealUninstaller).Select(x => x.Entry).ToList();
            var allEntryTargets = targets.Select(x => x.Entry).ToList();

            try
            {
                var task = UninstallerExecutionService.Instance.CreateBulkTask(realUninstallerTargets, quiet);

                var progressVm = new UninstallProgressViewModel(task);
                var progressWindow = new UninstallProgressWindow(progressVm);

                progressVm.UninstallationFinished += async (s, e) =>
                {
                    // Close the progress window first so MainWindow is free to host the next dialog
                    progressWindow.Close();

                    try
                    {
                        // Trigger junk scan after uninstallation finishes if enabled or if there are manual targets
                        if (AppSettingsService.Instance.AutoScanJunkAfterUninstall || manualTargets.Count > 0)
                        {
                            var allEntries = ViewModel.FilteredUninstallers.Select(x => x.Entry).ToList();
                            var junk = await JunkCleaningService.Instance.ScanJunkAsync(allEntryTargets, allEntries);

                            if (junk.Count > 0)
                            {
                                var junkVm = new JunkRemovalViewModel(junk);
                                var junkWindow = new JunkRemoveWindow(junkVm);
                                await junkWindow.ShowDialog(this);
                            }
                        }

                        // Refresh application list
                        await ViewModel.LoadApplicationsCommand.ExecuteAsync(null);

                        // Notify Windows Explorer to refresh navigation pane and icons without restarting explorer.exe
                        WindowsTools.NotifyShellAssociationsChanged();
                    }
                    catch (Exception ex)
                    {
                        ViewModel.StatusBar.StatusMessage = $"Post-uninstall error: {ex.Message}";
                    }
                };

                await progressWindow.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ViewModel.StatusBar.StatusMessage = $"Uninstall task error: {ex.Message}";
            }
        }

        private async void OnCleanJunkClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var targets = ViewModel.GetSelectedOrCurrent().Select(x => x.Entry).ToList();
            if (targets.Count == 0)
            {
                ViewModel.StatusBar.StatusMessage = "Please select an application to scan for leftover junk.";
                return;
            }

            ViewModel.StatusBar.IsBusy = true;
            ViewModel.StatusBar.StatusMessage = targets.Count == 1
                ? $"Scanning for residual junk for {targets[0].DisplayNameTrimmed}..."
                : $"Scanning for residual junk for {targets.Count} selected applications...";

            try
            {
                var allEntries = ViewModel.FilteredUninstallers.Select(x => x.Entry).ToList();
                var junk = await JunkCleaningService.Instance.ScanJunkAsync(targets, allEntries);

                if (junk.Count == 0)
                {
                    ViewModel.StatusBar.StatusMessage = targets.Count == 1
                        ? $"No residual junk found for {targets[0].DisplayNameTrimmed}."
                        : "No residual junk found for selected applications.";
                    return;
                }

                var junkVm = new JunkRemovalViewModel(junk);
                var junkWindow = new JunkRemoveWindow(junkVm);
                await junkWindow.ShowDialog(this);
            }
            finally
            {
                ViewModel.StatusBar.IsBusy = false;
                ViewModel.StatusBar.StatusMessage = "Ready";
            }
        }

        private async void OnDeleteTempFilesClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            try
            {
                var tempCleanerWindow = new TempCleanerWindow();
                await tempCleanerWindow.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ViewModel.StatusBar.StatusMessage = $"Temp cleaner error: {ex.Message}";
            }
        }

        private async void OnTargetApplicationClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var targetWindow = new TargetWindow();
            await targetWindow.ShowDialog(this);

            if (targetWindow.TargetPaths.Count > 0)
            {
                SelectApplicationsFromPaths(targetWindow.TargetPaths);
            }
        }

        private void SelectApplicationsFromPaths(List<string> paths)
        {
            if (ViewModel == null || paths.Count == 0) return;

            var allEntries = ViewModel.AllEntries;
            var matched = new HashSet<ApplicationEntryViewModel>();

            foreach (var rawPath in paths)
            {
                var candidate = FindMatchingApplication(rawPath, allEntries);
                if (candidate != null)
                {
                    matched.Add(candidate);
                }
            }

            // Fallback: If no registered uninstaller entry matched, generate an orphan uninstaller from directory on the fly
            if (matched.Count == 0)
            {
                foreach (var rawPath in paths)
                {
                    var normPath = rawPath.Trim('"', ' ').TrimEnd('\\', '/');
                    var dirPath = Directory.Exists(normPath) ? normPath : Path.GetDirectoryName(normPath);
                    if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath) && !IsGenericSystemDirectory(dirPath))
                    {
                        try
                        {
                            var existing = allEntries.Select(x => x.Entry).ToList();
                            var created = DirectoryFactory.TryCreateFromDirectory(new DirectoryInfo(dirPath), existing).ToList();
                            if (created.Count > 0)
                            {
                                var infoAdder = new InfoAdderManager();
                                foreach (var newEntry in created)
                                {
                                    infoAdder.AddMissingInformation(newEntry);
                                    var newVm = new ApplicationEntryViewModel(newEntry);
                                    ViewModel.AddEntry(newVm);
                                    matched.Add(newVm);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"DirectoryFactory error: {ex.Message}");
                        }
                    }
                }
            }

            if (matched.Count > 0)
            {
                // Ensure all matched items are visible in the DataGrid by updating filters if needed
                EnsureEntriesVisible(matched);

                // Clear previous checkboxes and select matched items
                foreach (var item in ViewModel.FilteredUninstallers)
                {
                    item.IsChecked = matched.Contains(item);
                }

                var first = matched.First();
                ViewModel.SelectedItem = first;
                ScrollToSelectedItem(first);

                var names = string.Join(", ", matched.Select(x => x.DisplayName));
                ViewModel.StatusBar.StatusMessage = $"🎯 Targeted {matched.Count} application(s): {names}";
            }
            else
            {
                var targetStr = string.Join(", ", paths);
                ViewModel.StatusBar.StatusMessage = $"⚠️ Target '{targetStr}' could not be matched to any application or directory.";
            }
        }

        private static bool IsGenericSystemDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            var clean = path.Trim('"', ' ').TrimEnd('\\', '/');
            if (clean.Length <= 3) return true; // e.g. "C:", "C:\"

            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (clean.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                clean.Equals(Path.Combine(winDir, "System32"), StringComparison.OrdinalIgnoreCase) ||
                clean.Equals(Path.Combine(winDir, "SysWOW64"), StringComparison.OrdinalIgnoreCase))
                return true;

            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (clean.Equals(progFiles, StringComparison.OrdinalIgnoreCase)) return true;

            var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(progFilesX86) && clean.Equals(progFilesX86, StringComparison.OrdinalIgnoreCase)) return true;

            var commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            if (!string.IsNullOrEmpty(commonFiles) && clean.Equals(commonFiles, StringComparison.OrdinalIgnoreCase)) return true;

            var commonFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
            if (!string.IsNullOrEmpty(commonFilesX86) && clean.Equals(commonFilesX86, StringComparison.OrdinalIgnoreCase)) return true;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                if (clean.Equals(userProfile, StringComparison.OrdinalIgnoreCase)) return true;
                var usersDir = Path.GetDirectoryName(userProfile);
                if (!string.IsNullOrEmpty(usersDir) && clean.Equals(usersDir, StringComparison.OrdinalIgnoreCase)) return true;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData) && clean.Equals(appData, StringComparison.OrdinalIgnoreCase)) return true;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData) && clean.Equals(localAppData, StringComparison.OrdinalIgnoreCase)) return true;

            var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(progData) && clean.Equals(progData, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private ApplicationEntryViewModel? FindMatchingApplication(string rawPath, IReadOnlyList<ApplicationEntryViewModel> allEntries)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return null;

            var normPath = rawPath.Trim('"', ' ').TrimEnd('\\', '/');
            if (normPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var resolved = WindowsTools.ResolveShortcut(normPath);
                    if (!string.IsNullOrEmpty(resolved)) normPath = resolved.Trim('"', ' ').TrimEnd('\\', '/');
                }
                catch { }
            }

            var dirPath = Directory.Exists(normPath) ? normPath : Path.GetDirectoryName(normPath);
            var fileName = Path.GetFileName(normPath);
            var fileStem = Path.GetFileNameWithoutExtension(normPath);
            var dirName = !string.IsNullOrEmpty(dirPath) ? Path.GetFileName(dirPath.TrimEnd('\\', '/')) : null;

            // Ignore Windows core OS components so they don't falsely match apps misconfigured with C:\Windows install directories
            if (!string.IsNullOrEmpty(fileName))
            {
                if (string.Equals(fileName, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "dwm.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "taskhostw.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "ShellExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "SearchHost.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "svchost.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "csrss.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "services.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "lsass.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "winlogon.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "sihost.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            string? productName = null;
            string? fileDescription = null;

            if (File.Exists(normPath))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(normPath);
                    productName = vi.ProductName?.Trim();
                    fileDescription = vi.FileDescription?.Trim();
                }
                catch { }
            }

            ApplicationEntryViewModel? bestMatch = null;
            int bestScore = 0;

            foreach (var app in allEntries)
            {
                int score = 0;

                // 1. Exact executable / icon / uninstaller full filename match
                if (!string.IsNullOrWhiteSpace(app.DisplayIcon))
                {
                    var cleanIcon = app.DisplayIcon.Trim('"', ' ');
                    var commaIdx = cleanIcon.IndexOf(',');
                    if (commaIdx > 0) cleanIcon = cleanIcon.Substring(0, commaIdx).Trim('"', ' ');

                    if (cleanIcon.Equals(normPath, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 1000);
                    else if (!string.IsNullOrEmpty(dirPath) && !IsGenericSystemDirectory(dirPath) && cleanIcon.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 750);
                }

                if (!string.IsNullOrWhiteSpace(app.UninstallerFullFilename))
                {
                    if (app.UninstallerFullFilename.Equals(normPath, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 1000);
                }

                if (app.SortedExecutables != null && app.SortedExecutables.Any(x => x.Equals(normPath, StringComparison.OrdinalIgnoreCase)))
                {
                    score = Math.Max(score, 950);
                }

                // 2. InstallLocation match (strictly excluding generic system directories like C:\Windows or C:\Program Files)
                if (!string.IsNullOrWhiteSpace(app.InstallLocation))
                {
                    var appLoc = app.InstallLocation.Trim('"', ' ').TrimEnd('\\', '/');
                    if (appLoc.Length >= 3 && !appLoc.EndsWith(':') && !IsGenericSystemDirectory(appLoc))
                    {
                        if (normPath.Equals(appLoc, StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 900);
                        else if (normPath.StartsWith(appLoc + "\\", StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 850);
                        else if (appLoc.Equals(dirPath, StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 850);
                        else if (!string.IsNullOrEmpty(dirPath) && !IsGenericSystemDirectory(dirPath) && dirPath.StartsWith(appLoc + "\\", StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 800);
                        else if (!string.IsNullOrEmpty(dirPath) && !IsGenericSystemDirectory(dirPath) && appLoc.StartsWith(dirPath + "\\", StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 750);
                    }
                }

                // 3. Uninstaller location & UninstallString directory match
                if (!string.IsNullOrWhiteSpace(app.UninstallerLocation))
                {
                    var uninstLoc = app.UninstallerLocation.Trim('"', ' ').TrimEnd('\\', '/');
                    if (uninstLoc.Length >= 3 && !uninstLoc.EndsWith(':') && !IsGenericSystemDirectory(uninstLoc) && !string.IsNullOrEmpty(dirPath) && !IsGenericSystemDirectory(dirPath))
                    {
                        if (uninstLoc.Equals(dirPath, StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 700);
                        else if (dirPath.StartsWith(uninstLoc + "\\", StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 650);
                        else if (uninstLoc.StartsWith(dirPath + "\\", StringComparison.OrdinalIgnoreCase))
                            score = Math.Max(score, 650);
                    }
                }

                if (!string.IsNullOrWhiteSpace(app.UninstallString) && !string.IsNullOrEmpty(dirPath) && dirPath.Length >= 5 && !IsGenericSystemDirectory(dirPath))
                {
                    if (app.UninstallString.Contains(dirPath, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 650);
                }

                // 4. Store App match (RatingId, Comment, InstallLocation)
                if (app.IsStoreApp)
                {
                    if (!string.IsNullOrEmpty(app.RatingId) && normPath.Contains(app.RatingId, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 700);

                    if (!string.IsNullOrEmpty(app.Entry.Comment) && normPath.Contains(app.Entry.Comment, StringComparison.OrdinalIgnoreCase))
                        score = Math.Max(score, 700);
                }

                // 5. Executable FileVersionInfo Product Name match
                if (!string.IsNullOrEmpty(productName) && productName.Length >= 3)
                {
                    if (app.DisplayName.Equals(productName, StringComparison.OrdinalIgnoreCase) ||
                        app.DisplayNameTrimmed.Equals(productName, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 500);
                    }
                    else if (app.DisplayName.Contains(productName, StringComparison.OrdinalIgnoreCase) ||
                             productName.Contains(app.DisplayNameTrimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 450);
                    }
                }

                if (!string.IsNullOrEmpty(fileDescription) && fileDescription.Length >= 3)
                {
                    if (app.DisplayName.Equals(fileDescription, StringComparison.OrdinalIgnoreCase) ||
                        app.DisplayNameTrimmed.Equals(fileDescription, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 400);
                    }
                }

                // 6. Directory Name match (e.g. folder is "Discord" or "7-Zip")
                if (!string.IsNullOrEmpty(dirName) && dirName.Length >= 3)
                {
                    if (app.DisplayNameTrimmed.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                        app.DisplayName.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                        app.RegistryKeyName.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 350);
                    }
                    else if (app.DisplayNameTrimmed.Contains(dirName, StringComparison.OrdinalIgnoreCase) ||
                             dirName.Contains(app.DisplayNameTrimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 300);
                    }
                }

                // 7. Executable stem match (e.g. "chrome", "notepad++", "vlc")
                if (!string.IsNullOrEmpty(fileStem) && fileStem.Length >= 3)
                {
                    if (app.DisplayNameTrimmed.Equals(fileStem, StringComparison.OrdinalIgnoreCase) ||
                        app.RegistryKeyName.Equals(fileStem, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 250);
                    }
                    else if (app.DisplayNameTrimmed.StartsWith(fileStem, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 200);
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = app;
                }
            }

            return bestScore >= 200 ? bestMatch : null;
        }

        private void EnsureEntriesVisible(HashSet<ApplicationEntryViewModel> entries)
        {
            if (ViewModel == null || entries.Count == 0) return;

            bool filterNeedsUpdate = false;

            foreach (var item in entries)
            {
                if (item.IsStoreApp && !ViewModel.Sidebar.ShowStoreApps)
                {
                    ViewModel.Sidebar.ShowStoreApps = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsDesktopApp && !ViewModel.Sidebar.ShowDesktopApps)
                {
                    ViewModel.Sidebar.ShowDesktopApps = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsGame && !ViewModel.Sidebar.ShowGames)
                {
                    ViewModel.Sidebar.ShowGames = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsWindowsFeature && !ViewModel.Sidebar.ShowWindowsFeatures)
                {
                    ViewModel.Sidebar.ShowWindowsFeatures = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsSystemComponent && !ViewModel.Sidebar.ShowSystemComponents)
                {
                    ViewModel.Sidebar.ShowSystemComponents = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsUpdate && !ViewModel.Sidebar.ShowUpdates)
                {
                    ViewModel.Sidebar.ShowUpdates = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsProtected && !ViewModel.Sidebar.ShowProtected)
                {
                    ViewModel.Sidebar.ShowProtected = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsOrphaned && !ViewModel.Sidebar.ShowOrphans)
                {
                    ViewModel.Sidebar.ShowOrphans = true;
                    filterNeedsUpdate = true;
                }
                if (!item.IsValid && !ViewModel.Sidebar.ShowInvalid)
                {
                    ViewModel.Sidebar.ShowInvalid = true;
                    filterNeedsUpdate = true;
                }
                if (item.IsVerified && !ViewModel.Sidebar.ShowVerified)
                {
                    ViewModel.Sidebar.ShowVerified = true;
                    filterNeedsUpdate = true;
                }
                if (item.Is64Bit && !ViewModel.Sidebar.Show64Bit)
                {
                    ViewModel.Sidebar.Show64Bit = true;
                    filterNeedsUpdate = true;
                }
                if (!item.Is64Bit && !ViewModel.Sidebar.Show32Bit)
                {
                    ViewModel.Sidebar.Show32Bit = true;
                    filterNeedsUpdate = true;
                }
            }

            if (ViewModel.Sidebar.SelectedSizeFilterIndex != 0)
            {
                ViewModel.Sidebar.SelectedSizeFilterIndex = 0;
                filterNeedsUpdate = true;
            }

            if (ViewModel.Sidebar.SelectedDateFilterIndex != 0)
            {
                ViewModel.Sidebar.SelectedDateFilterIndex = 0;
                filterNeedsUpdate = true;
            }

            if (!string.IsNullOrEmpty(ViewModel.Sidebar.SearchText))
            {
                ViewModel.Sidebar.SearchText = string.Empty;
                filterNeedsUpdate = true;
            }

            if (filterNeedsUpdate)
            {
                ViewModel.ApplyFiltering();
            }
        }

        private void OnResetSettingsClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ResetToDefaults();
            if (ViewModel != null)
            {
                ViewModel.Sidebar.ResetFiltersCommand.Execute(null);
                ViewModel.StatusBar.StatusMessage = "All application settings and column preferences have been reset to defaults.";
            }
        }

        private async void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(5);
            await settingsWindow.ShowDialog(this);
        }

        private async void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            await settingsWindow.ShowDialog(this);
        }

        private void OnThemeSystemClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ApplyTheme(0);
            AppSettingsService.Instance.Save();
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Theme set to: Use System Theme";
        }

        private void OnThemeLightClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ApplyTheme(1);
            AppSettingsService.Instance.Save();
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Theme set to: Light Mode";
        }

        private void OnThemeDarkClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ApplyTheme(2);
            AppSettingsService.Instance.Save();
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Theme set to: Dark Mode";
        }

        private void OnThemeMidnightClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ApplyTheme(3);
            AppSettingsService.Instance.Save();
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Theme set to: Midnight Blue";
        }

        private void OnThemeOledClick(object? sender, RoutedEventArgs e)
        {
            AppSettingsService.Instance.ApplyTheme(4);
            AppSettingsService.Instance.Save();
            if (ViewModel != null) ViewModel.StatusBar.StatusMessage = "Theme set to: OLED Black";
        }

        private void OnAutoResizeColumnsClick(object? sender, RoutedEventArgs e)
        {
            if (ApplicationsDataGrid == null) return;

            foreach (var column in ApplicationsDataGrid.Columns)
            {
                if (column is DataGridCheckBoxColumn)
                {
                    column.Width = new DataGridLength(44, DataGridLengthUnitType.Pixel);
                }
                else
                {
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
                }
            }

            if (ViewModel != null)
            {
                ViewModel.StatusBar.StatusMessage = "Auto-resized all table columns to fit content.";
            }
        }
    }
}


