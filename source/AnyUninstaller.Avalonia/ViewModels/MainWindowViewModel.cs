using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnyUninstaller.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klocman.IO;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private List<ApplicationEntryViewModel> _allEntries = new();
        public IReadOnlyList<ApplicationEntryViewModel> AllEntries => _allEntries;

        public void AddEntry(ApplicationEntryViewModel entry)
        {
            if (!_allEntries.Contains(entry))
            {
                _allEntries.Add(entry);
                ApplyFiltering();
            }
        }

        [ObservableProperty]
        private ObservableCollection<ApplicationEntryViewModel> _filteredUninstallers = new();

        [ObservableProperty]
        private ApplicationEntryViewModel? _selectedItem;

        [ObservableProperty]
        private FilterSidebarViewModel _sidebar = new();

        [ObservableProperty]
        private StatusBarViewModel _statusBar = new();

        [ObservableProperty]
        private bool _isTreeMapVisible = AppSettingsService.Instance.ShowTreemap;

        [ObservableProperty]
        private bool _isToolbarVisible = AppSettingsService.Instance.IsToolbarVisible;

        [ObservableProperty]
        private bool _isStatusBarVisible = AppSettingsService.Instance.IsStatusBarVisible;

        // Column Visibility
        [ObservableProperty]
        private bool _showColumnCheckboxes = AppSettingsService.Instance.ShowColumnCheckboxes;

        [ObservableProperty]
        private bool _showColumnPublisher = AppSettingsService.Instance.ShowColumnPublisher;

        [ObservableProperty]
        private bool _showColumnVersion = AppSettingsService.Instance.ShowColumnVersion;

        [ObservableProperty]
        private bool _showColumnSize = AppSettingsService.Instance.ShowColumnSize;

        [ObservableProperty]
        private bool _showColumnStatus = AppSettingsService.Instance.ShowColumnStatus;

        [ObservableProperty]
        private bool _showColumnInstallDate = AppSettingsService.Instance.ShowColumnInstallDate;

        [ObservableProperty]
        private bool _showColumnType = AppSettingsService.Instance.ShowColumnType;

        [ObservableProperty]
        private bool _showColumnQuiet = AppSettingsService.Instance.ShowColumnQuiet;

        [ObservableProperty]
        private bool _showColumnLocation = AppSettingsService.Instance.ShowColumnLocation;

        [ObservableProperty]
        private int _selectedThemeIndex = AppSettingsService.Instance.SelectedThemeIndex;

        public bool IsThemeSystem => SelectedThemeIndex == 0;
        public bool IsThemeLight => SelectedThemeIndex == 1;
        public bool IsThemeDark => SelectedThemeIndex == 2;
        public bool IsThemeMidnight => SelectedThemeIndex == 3;
        public bool IsThemeOled => SelectedThemeIndex == 4;

        partial void OnIsTreeMapVisibleChanged(bool value)
        {
            AppSettingsService.Instance.ShowTreemap = value;
            AppSettingsService.Instance.Save();
        }

        partial void OnIsToolbarVisibleChanged(bool value)
        {
            AppSettingsService.Instance.IsToolbarVisible = value;
            AppSettingsService.Instance.Save();
        }

        partial void OnIsStatusBarVisibleChanged(bool value)
        {
            AppSettingsService.Instance.IsStatusBarVisible = value;
            AppSettingsService.Instance.Save();
        }

        private CancellationTokenSource? _scanCts;

        public MainWindowViewModel()
        {
            Sidebar.FilterChanged += (s, e) => ApplyFiltering();
            AppSettingsService.Instance.SettingsChanged += () =>
            {
                IsTreeMapVisible = AppSettingsService.Instance.ShowTreemap;
                IsToolbarVisible = AppSettingsService.Instance.IsToolbarVisible;
                IsStatusBarVisible = AppSettingsService.Instance.IsStatusBarVisible;
                Sidebar.IsSidebarVisible = AppSettingsService.Instance.IsSidebarVisible;
                ShowColumnCheckboxes = AppSettingsService.Instance.ShowColumnCheckboxes;
                ShowColumnPublisher = AppSettingsService.Instance.ShowColumnPublisher;
                ShowColumnVersion = AppSettingsService.Instance.ShowColumnVersion;
                ShowColumnSize = AppSettingsService.Instance.ShowColumnSize;
                ShowColumnStatus = AppSettingsService.Instance.ShowColumnStatus;
                ShowColumnInstallDate = AppSettingsService.Instance.ShowColumnInstallDate;
                ShowColumnType = AppSettingsService.Instance.ShowColumnType;
                ShowColumnQuiet = AppSettingsService.Instance.ShowColumnQuiet;
                ShowColumnLocation = AppSettingsService.Instance.ShowColumnLocation;
                SelectedThemeIndex = AppSettingsService.Instance.SelectedThemeIndex;
                OnPropertyChanged(nameof(IsThemeSystem));
                OnPropertyChanged(nameof(IsThemeLight));
                OnPropertyChanged(nameof(IsThemeDark));
                OnPropertyChanged(nameof(IsThemeMidnight));
                OnPropertyChanged(nameof(IsThemeOled));
            };
            _ = LoadApplicationsAsync();
        }

        [RelayCommand]
        public async Task LoadApplicationsAsync()
        {
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            StatusBar.IsBusy = true;
            StatusBar.StatusMessage = "Scanning for installed applications...";

            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                StatusBar.ProgressValue = p.current;
                StatusBar.ProgressMax = Math.Max(1, p.total);
                StatusBar.StatusMessage = p.message;
            });

            try
            {
                var results = await ScannerService.Instance.ScanApplicationsAsync(progress, _scanCts.Token);
                _allEntries = results.Select(r =>
                {
                    var vm = new ApplicationEntryViewModel(r);
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ApplicationEntryViewModel.IsChecked))
                        {
                            UpdateSelectionStats();
                        }
                    };
                    return vm;
                }).ToList();

                ApplyFiltering();

                StatusBar.StatusMessage = $"Scan completed. Found {_allEntries.Count} applications.";
            }
            catch (OperationCanceledException)
            {
                StatusBar.StatusMessage = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusBar.StatusMessage = $"Scan error: {ex.Message}";
            }
            finally
            {
                StatusBar.IsBusy = false;
            }
        }

        public void ApplyFiltering()
        {
            var query = _allEntries.Where(x =>
            {
                // 1. Search text filter
                if (!string.IsNullOrWhiteSpace(Sidebar.SearchText))
                {
                    var term = Sidebar.SearchText.Trim();
                    bool matchesSearch =
                        x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.InstallLocation.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.DisplayVersion.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.UninstallerKind.Contains(term, StringComparison.OrdinalIgnoreCase);

                    if (!matchesSearch) return false;
                }

                // 2. Quiet-only filter
                if (Sidebar.ShowOnlyQuiet && !x.QuietUninstallPossible)
                    return false;

                // 3. Store Apps exclusion (if user explicitly unchecks Store Apps)
                if (!Sidebar.ShowStoreApps && x.IsStoreApp)
                    return false;

                // 4. Status & Health / Category filter
                // An entry is shown if it matches any checked status/category:
                if (Sidebar.ShowProtected && x.IsProtected && !x.IsUpdate)
                    return true;

                if (Sidebar.ShowOrphans && x.IsOrphaned)
                    return true;

                if (Sidebar.ShowInvalid && !x.IsValid)
                    return true;

                if (Sidebar.ShowVerified && x.IsValid && !x.IsOrphaned && !x.IsProtected && !x.IsSystemComponent && !x.IsUpdate)
                    return true;

                if (Sidebar.ShowSystemComponents && x.IsSystemComponent && !x.IsProtected && !x.IsOrphaned && x.IsValid && !x.IsUpdate)
                    return true;

                if (Sidebar.ShowUpdates && x.IsUpdate)
                    return true;

                return false;
            });

            var list = query.ToList();
            FilteredUninstallers = new ObservableCollection<ApplicationEntryViewModel>(list);

            if (SelectedItem != null && !list.Contains(SelectedItem))
            {
                SelectedItem = null;
            }

            Sidebar.UpdateCounts(_allEntries, list.Count);

            StatusBar.TotalItemsCount = list.Count;
            StatusBar.TotalSize = list.Select(x => x.EstimatedSize)
                .DefaultIfEmpty(FileSize.Empty)
                .Aggregate((s1, s2) => s1 + s2);

            UpdateSelectionStats();
        }

        public bool AreAllSelected => FilteredUninstallers.Count > 0 && FilteredUninstallers.All(x => x.IsChecked);
        public string ToggleSelectAllText => AreAllSelected ? "Select None" : "Select All";
        public string ToggleSelectAllIcon => AreAllSelected ? "✕" : "✓";
        public string ToggleSelectAllTooltip => AreAllSelected ? "Deselect all visible applications" : "Select all visible applications";

        private void UpdateSelectionStats()
        {
            var checkedItems = FilteredUninstallers.Where(x => x.IsChecked).ToList();
            StatusBar.SelectedItemsCount = checkedItems.Count;
            StatusBar.SelectedSize = checkedItems.Select(x => x.EstimatedSize)
                .DefaultIfEmpty(FileSize.Empty)
                .Aggregate((s1, s2) => s1 + s2);

            OnPropertyChanged(nameof(AreAllSelected));
            OnPropertyChanged(nameof(ToggleSelectAllText));
            OnPropertyChanged(nameof(ToggleSelectAllIcon));
            OnPropertyChanged(nameof(ToggleSelectAllTooltip));
        }

        [RelayCommand]
        public void ToggleSelectAll()
        {
            if (FilteredUninstallers.Count == 0) return;

            bool targetState = !AreAllSelected;
            foreach (var item in FilteredUninstallers)
            {
                item.IsChecked = targetState;
            }
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var item in FilteredUninstallers)
                item.IsChecked = true;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var item in FilteredUninstallers)
                item.IsChecked = false;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void InvertSelection()
        {
            foreach (var item in FilteredUninstallers)
                item.IsChecked = !item.IsChecked;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void OpenInstallLocation()
        {
            if (SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.InstallLocation))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{SelectedItem.InstallLocation}\"",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Ignore open failure
                }
            }
        }

        public List<ApplicationEntryViewModel> GetSelectedOrCurrent()
        {
            var checkedItems = FilteredUninstallers.Where(x => x.IsChecked).ToList();
            if (checkedItems.Count > 0) return checkedItems;
            if (SelectedItem != null) return new List<ApplicationEntryViewModel> { SelectedItem };
            return new List<ApplicationEntryViewModel>();
        }
    }
}
