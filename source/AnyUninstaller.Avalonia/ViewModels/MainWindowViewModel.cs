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

        partial void OnSelectedItemChanged(ApplicationEntryViewModel? value)
        {
            OnPropertyChanged(nameof(HasSelectedApplications));
        }

        public bool IsNotLoading => !StatusBar.IsBusy;
        public bool CanSelect => !StatusBar.IsBusy && FilteredUninstallers.Count > 0;
        public bool HasSelectedApplications => !StatusBar.IsBusy && ((FilteredUninstallers.Count > 0 && FilteredUninstallers.Any(x => x.IsChecked)) || SelectedItem != null);

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
            StatusBar.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StatusBarViewModel.IsBusy))
                {
                    OnPropertyChanged(nameof(IsNotLoading));
                    OnPropertyChanged(nameof(CanSelect));
                    OnPropertyChanged(nameof(HasSelectedApplications));
                }
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

                // Build ViewModels on worker thread
                var vms = await Task.Run(() =>
                {
                    return results.Select(r => new ApplicationEntryViewModel(r)).ToList();
                }, _scanCts.Token);

                foreach (var vm in vms)
                {
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ApplicationEntryViewModel.IsChecked))
                        {
                            UpdateSelectionStats();
                        }
                    };
                }

                _allEntries = vms;
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
            var searchText = Sidebar.SearchText?.Trim();
            string[]? searchTerms = !string.IsNullOrWhiteSpace(searchText)
                ? searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                : null;

            bool showDesktop = Sidebar.ShowDesktopApps;
            bool showStore = Sidebar.ShowStoreApps;
            bool showGames = Sidebar.ShowGames;
            bool showSystem = Sidebar.ShowSystemComponents;
            bool showUpdates = Sidebar.ShowUpdates;
            bool showFeatures = Sidebar.ShowWindowsFeatures;

            bool showVerified = Sidebar.ShowVerified;
            bool showProtected = Sidebar.ShowProtected;
            bool showOrphans = Sidebar.ShowOrphans;
            bool showInvalid = Sidebar.ShowInvalid;

            bool show64 = Sidebar.Show64Bit;
            bool show32 = Sidebar.Show32Bit;

            int sizeIndex = Sidebar.SelectedSizeFilterIndex;
            int dateIndex = Sidebar.SelectedDateFilterIndex;

            bool onlyQuiet = Sidebar.ShowOnlyQuiet;
            bool onlyStartup = Sidebar.ShowOnlyStartup;
            bool showSigned = Sidebar.ShowSigned;
            bool showUnsigned = Sidebar.ShowUnsigned;

            const long oneGbInKb = 1024 * 1024;
            const long hundredMbInKb = 100 * 1024;

            var list = new List<ApplicationEntryViewModel>(_allEntries.Count);
            var totalSize = FileSize.Empty;

            for (int i = 0; i < _allEntries.Count; i++)
            {
                var x = _allEntries[i];

                // 1. Search text filter
                if (searchTerms != null)
                {
                    bool matchesAll = true;
                    for (int t = 0; t < searchTerms.Length; t++)
                    {
                        var term = searchTerms[t];
                        bool termMatches =
                            (!string.IsNullOrEmpty(x.DisplayName) && x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.Publisher) && x.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.InstallLocation) && x.InstallLocation.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.DisplayVersion) && x.DisplayVersion.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.UninstallerKind) && x.UninstallerKind.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.Comment) && x.Comment.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.Architecture) && x.Architecture.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.StatusDescription) && x.StatusDescription.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(x.CertificateIssuer) && x.CertificateIssuer.Contains(term, StringComparison.OrdinalIgnoreCase));

                        if (!termMatches)
                        {
                            matchesAll = false;
                            break;
                        }
                    }
                    if (!matchesAll) continue;
                }

                // 2. Application Type Filter
                if (x.IsSystemComponent && !showSystem) continue;
                if (x.IsUpdate && !showUpdates) continue;
                if (x.IsWindowsFeature && !showFeatures) continue;
                if (x.IsStoreApp && !showStore) continue;
                if (x.IsGame && !showGames) continue;
                if (x.IsDesktopApp && !showDesktop) continue;

                // 3. Status & Health Filter
                if (x.IsOrphaned && !showOrphans) continue;
                if (!x.IsValid && !showInvalid) continue;
                if (x.IsProtected && !showProtected) continue;
                if (x.IsVerified && !showVerified) continue;

                // 4. Architecture Filter
                if (x.Is64Bit && !show64) continue;
                if (!x.Is64Bit && !show32) continue;

                // 5. Size Range Filter
                if (sizeIndex != 0)
                {
                    long sizeKb = x.EstimatedSizeKb;
                    if (sizeIndex == 1 && sizeKb < oneGbInKb) continue;
                    if (sizeIndex == 2 && (sizeKb < hundredMbInKb || sizeKb >= oneGbInKb)) continue;
                    if (sizeIndex == 3 && (sizeKb <= 0 || sizeKb >= hundredMbInKb)) continue;
                    if (sizeIndex == 4 && sizeKb > 0) continue;
                }

                // 6. Installation Age Filter
                if (dateIndex != 0)
                {
                    if (dateIndex == 5)
                    {
                        if (x.HasInstallDate) continue;
                    }
                    else
                    {
                        if (!x.HasInstallDate) continue;
                        double age = x.InstallAgeDays;
                        if (age < 0) continue;
                        if (dateIndex == 1 && age > 7) continue;
                        if (dateIndex == 2 && age > 30) continue;
                        if (dateIndex == 3 && age > 90) continue;
                        if (dateIndex == 4 && age <= 365) continue;
                    }
                }

                // 7. Capabilities & Security Filters
                if (onlyQuiet && !x.QuietUninstallPossible) continue;
                if (onlyStartup && !x.HasStartupEntries) continue;
                if (!showSigned && x.IsSigned) continue;
                if (!showUnsigned && !x.IsSigned) continue;

                list.Add(x);
                totalSize += x.EstimatedSize;
            }

            if (!string.IsNullOrEmpty(_currentSortMemberPath))
            {
                list = ApplySortOrdering(list, _currentSortMemberPath, _isSortAscending).ToList();
            }

            FilteredUninstallers = new ObservableCollection<ApplicationEntryViewModel>(list);

            if (SelectedItem != null && !list.Contains(SelectedItem))
            {
                SelectedItem = null;
            }

            Sidebar.UpdateCounts(_allEntries, list.Count);

            StatusBar.TotalItemsCount = list.Count;
            StatusBar.TotalSize = totalSize;

            UpdateSelectionStats();
        }

        private string? _currentSortMemberPath;
        private bool _isSortAscending = true;

        public void SortFiltered(string? sortMemberPath, bool ascending)
        {
            _currentSortMemberPath = sortMemberPath;
            _isSortAscending = ascending;

            if (string.IsNullOrEmpty(sortMemberPath) || FilteredUninstallers.Count == 0) return;

            var sorted = ApplySortOrdering(FilteredUninstallers, sortMemberPath, ascending);
            FilteredUninstallers = new ObservableCollection<ApplicationEntryViewModel>(sorted);
        }

        private static IEnumerable<ApplicationEntryViewModel> ApplySortOrdering(IEnumerable<ApplicationEntryViewModel> items, string sortMemberPath, bool ascending)
        {
            return sortMemberPath switch
            {
                nameof(ApplicationEntryViewModel.DisplayName) => ascending 
                    ? items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.DisplayName, StringComparer.OrdinalIgnoreCase),
                nameof(ApplicationEntryViewModel.Publisher) => ascending 
                    ? items.OrderBy(x => x.Publisher, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.Publisher, StringComparer.OrdinalIgnoreCase),
                nameof(ApplicationEntryViewModel.DisplayVersion) => ascending 
                    ? items.OrderBy(x => x.DisplayVersion, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.DisplayVersion, StringComparer.OrdinalIgnoreCase),
                nameof(ApplicationEntryViewModel.EstimatedSizeKb) or nameof(ApplicationEntryViewModel.EstimatedSize) => ascending 
                    ? items.OrderBy(x => x.EstimatedSizeKb) 
                    : items.OrderByDescending(x => x.EstimatedSizeKb),
                nameof(ApplicationEntryViewModel.StatusDescription) => ascending 
                    ? items.OrderBy(x => x.StatusDescription, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.StatusDescription, StringComparer.OrdinalIgnoreCase),
                nameof(ApplicationEntryViewModel.InstallDate) => ascending 
                    ? items.OrderBy(x => x.InstallDate) 
                    : items.OrderByDescending(x => x.InstallDate),
                nameof(ApplicationEntryViewModel.UninstallerKind) => ascending 
                    ? items.OrderBy(x => x.UninstallerKind, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.UninstallerKind, StringComparer.OrdinalIgnoreCase),
                nameof(ApplicationEntryViewModel.QuietUninstallPossible) => ascending 
                    ? items.OrderBy(x => x.QuietUninstallPossible) 
                    : items.OrderByDescending(x => x.QuietUninstallPossible),
                nameof(ApplicationEntryViewModel.InstallLocation) => ascending 
                    ? items.OrderBy(x => x.InstallLocation, StringComparer.OrdinalIgnoreCase) 
                    : items.OrderByDescending(x => x.InstallLocation, StringComparer.OrdinalIgnoreCase),
                _ => items
            };
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
            OnPropertyChanged(nameof(CanSelect));
            OnPropertyChanged(nameof(HasSelectedApplications));
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
