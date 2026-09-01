using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnyUninstaller.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class FilterSidebarViewModel : ViewModelBase
    {
        // 1. Application Type Filters
        [ObservableProperty]
        private bool _showDesktopApps = AppSettingsService.Instance.FilterShowDesktopApps;

        [ObservableProperty]
        private bool _showStoreApps = AppSettingsService.Instance.FilterShowStoreApps;

        [ObservableProperty]
        private bool _showGames = AppSettingsService.Instance.FilterShowGames;

        [ObservableProperty]
        private bool _showSystemComponents = AppSettingsService.Instance.FilterShowSystemComponents;

        [ObservableProperty]
        private bool _showUpdates = AppSettingsService.Instance.FilterShowUpdates;

        [ObservableProperty]
        private bool _showWindowsFeatures = AppSettingsService.Instance.FilterShowWindowsFeatures;

        // 2. Health & Status Filters
        [ObservableProperty]
        private bool _showVerified = AppSettingsService.Instance.FilterShowVerified;

        [ObservableProperty]
        private bool _showProtected = AppSettingsService.Instance.FilterShowProtected;

        [ObservableProperty]
        private bool _showOrphans = AppSettingsService.Instance.FilterShowOrphans;

        [ObservableProperty]
        private bool _showInvalid = AppSettingsService.Instance.FilterShowInvalid;

        // 3. Architecture Filters
        [ObservableProperty]
        private bool _show64Bit = AppSettingsService.Instance.FilterShow64Bit;

        [ObservableProperty]
        private bool _show32Bit = AppSettingsService.Instance.FilterShow32Bit;

        // 4. Size & Installation Date Selectors
        [ObservableProperty]
        private int _selectedSizeFilterIndex = AppSettingsService.Instance.FilterSelectedSizeIndex;

        [ObservableProperty]
        private int _selectedDateFilterIndex = AppSettingsService.Instance.FilterSelectedDateIndex;

        // 5. Capabilities & Security Filters
        [ObservableProperty]
        private bool _showOnlyQuiet = AppSettingsService.Instance.FilterShowOnlyQuiet;

        [ObservableProperty]
        private bool _showOnlyStartup = AppSettingsService.Instance.FilterShowOnlyStartup;

        [ObservableProperty]
        private bool _showSigned = AppSettingsService.Instance.FilterShowSigned;

        [ObservableProperty]
        private bool _showUnsigned = AppSettingsService.Instance.FilterShowUnsigned;

        // 6. Search & Visibility
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isSidebarVisible = AppSettingsService.Instance.IsSidebarVisible;

        // 7. Dynamic Count Badges
        [ObservableProperty]
        private int _countTotal;

        [ObservableProperty]
        private int _filteredCount;

        [ObservableProperty]
        private bool _isFiltered;

        // Type Counts
        [ObservableProperty]
        private int _countDesktopApps;

        [ObservableProperty]
        private int _countStoreApps;

        [ObservableProperty]
        private int _countGames;

        [ObservableProperty]
        private int _countSystemComponents;

        [ObservableProperty]
        private int _countUpdates;

        [ObservableProperty]
        private int _countWindowsFeatures;

        // Health Counts
        [ObservableProperty]
        private int _countVerified;

        [ObservableProperty]
        private int _countProtected;

        [ObservableProperty]
        private int _countOrphans;

        [ObservableProperty]
        private int _countInvalid;

        // Architecture Counts
        [ObservableProperty]
        private int _count64Bit;

        [ObservableProperty]
        private int _count32Bit;

        // Size Counts
        [ObservableProperty]
        private int _countLargeSize;

        [ObservableProperty]
        private int _countMediumSize;

        [ObservableProperty]
        private int _countSmallSize;

        [ObservableProperty]
        private int _countUnknownSize;

        // Date Counts
        [ObservableProperty]
        private int _countRecent7Days;

        [ObservableProperty]
        private int _countRecent30Days;

        [ObservableProperty]
        private int _countRecent90Days;

        [ObservableProperty]
        private int _countOlder1Year;

        // Capability & Security Counts
        [ObservableProperty]
        private int _countQuiet;

        [ObservableProperty]
        private int _countStartup;

        [ObservableProperty]
        private int _countSigned;

        [ObservableProperty]
        private int _countUnsigned;

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

        public event EventHandler? FilterChanged;

        partial void OnIsSidebarVisibleChanged(bool value)
        {
            AppSettingsService.Instance.IsSidebarVisible = value;
            AppSettingsService.Instance.Save();
        }

        partial void OnShowDesktopAppsChanged(bool value) => NotifyFilterChanged();
        partial void OnShowStoreAppsChanged(bool value) => NotifyFilterChanged();
        partial void OnShowGamesChanged(bool value) => NotifyFilterChanged();
        partial void OnShowSystemComponentsChanged(bool value) => NotifyFilterChanged();
        partial void OnShowUpdatesChanged(bool value) => NotifyFilterChanged();
        partial void OnShowWindowsFeaturesChanged(bool value) => NotifyFilterChanged();

        partial void OnShowVerifiedChanged(bool value) => NotifyFilterChanged();
        partial void OnShowProtectedChanged(bool value) => NotifyFilterChanged();
        partial void OnShowOrphansChanged(bool value) => NotifyFilterChanged();
        partial void OnShowInvalidChanged(bool value) => NotifyFilterChanged();

        partial void OnShow64BitChanged(bool value) => NotifyFilterChanged();
        partial void OnShow32BitChanged(bool value) => NotifyFilterChanged();

        partial void OnSelectedSizeFilterIndexChanged(int value) => NotifyFilterChanged();
        partial void OnSelectedDateFilterIndexChanged(int value) => NotifyFilterChanged();

        partial void OnShowOnlyQuietChanged(bool value) => NotifyFilterChanged();
        partial void OnShowOnlyStartupChanged(bool value) => NotifyFilterChanged();
        partial void OnShowSignedChanged(bool value) => NotifyFilterChanged();
        partial void OnShowUnsignedChanged(bool value) => NotifyFilterChanged();

        private System.Threading.CancellationTokenSource? _searchDebounceCts;

        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasSearchText));
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            // Debounce rapid typing (150ms) to keep UI responsive
            Task.Delay(150, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(NotifyFilterChanged);
                }
            }, TaskScheduler.Default);
        }

        private void NotifyFilterChanged()
        {
            IsFiltered = !ShowDesktopApps || !ShowStoreApps || !ShowGames ||
                         ShowSystemComponents || ShowUpdates || ShowWindowsFeatures ||
                         !ShowProtected || !ShowOrphans || !ShowInvalid || !ShowVerified ||
                         !Show64Bit || !Show32Bit ||
                         SelectedSizeFilterIndex != 0 || SelectedDateFilterIndex != 0 ||
                         ShowOnlyQuiet || ShowOnlyStartup || !ShowSigned || !ShowUnsigned ||
                         !string.IsNullOrWhiteSpace(SearchText);

            AppSettingsService.Instance.FilterShowDesktopApps = ShowDesktopApps;
            AppSettingsService.Instance.FilterShowStoreApps = ShowStoreApps;
            AppSettingsService.Instance.FilterShowGames = ShowGames;
            AppSettingsService.Instance.FilterShowSystemComponents = ShowSystemComponents;
            AppSettingsService.Instance.FilterShowUpdates = ShowUpdates;
            AppSettingsService.Instance.FilterShowWindowsFeatures = ShowWindowsFeatures;
            AppSettingsService.Instance.FilterShowProtected = ShowProtected;
            AppSettingsService.Instance.FilterShowOrphans = ShowOrphans;
            AppSettingsService.Instance.FilterShowInvalid = ShowInvalid;
            AppSettingsService.Instance.FilterShowVerified = ShowVerified;
            AppSettingsService.Instance.FilterShow64Bit = Show64Bit;
            AppSettingsService.Instance.FilterShow32Bit = Show32Bit;
            AppSettingsService.Instance.FilterSelectedSizeIndex = SelectedSizeFilterIndex;
            AppSettingsService.Instance.FilterSelectedDateIndex = SelectedDateFilterIndex;
            AppSettingsService.Instance.FilterShowOnlyQuiet = ShowOnlyQuiet;
            AppSettingsService.Instance.FilterShowOnlyStartup = ShowOnlyStartup;
            AppSettingsService.Instance.FilterShowSigned = ShowSigned;
            AppSettingsService.Instance.FilterShowUnsigned = ShowUnsigned;

            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateCounts(IReadOnlyList<ApplicationEntryViewModel> allEntries, int filtered)
        {
            CountTotal = allEntries.Count;
            FilteredCount = filtered;

            int desktop = 0, store = 0, games = 0, system = 0, updates = 0, winFeatures = 0;
            int verified = 0, protectedItems = 0, orphans = 0, invalid = 0;
            int bit64 = 0, bit32 = 0;
            int large = 0, medium = 0, small = 0, unknownSize = 0;
            int recent7 = 0, recent30 = 0, recent90 = 0, older1Year = 0;
            int quiet = 0, startup = 0, signed = 0, unsigned = 0;

            const long oneGbInKb = 1024 * 1024;
            const long hundredMbInKb = 100 * 1024;

            for (int i = 0; i < allEntries.Count; i++)
            {
                var x = allEntries[i];

                if (x.IsDesktopApp) desktop++;
                if (x.IsStoreApp) store++;
                if (x.IsGame) games++;
                if (x.IsSystemComponent) system++;
                if (x.IsUpdate) updates++;
                if (x.IsWindowsFeature) winFeatures++;

                if (x.IsVerified) verified++;
                if (x.IsProtected) protectedItems++;
                if (x.IsOrphaned) orphans++;
                if (!x.IsValid) invalid++;

                if (x.Is64Bit) bit64++;
                else bit32++;

                long sizeKb = x.EstimatedSizeKb;
                if (sizeKb >= oneGbInKb) large++;
                else if (sizeKb >= hundredMbInKb) medium++;
                else if (sizeKb > 0) small++;
                else unknownSize++;

                if (x.HasInstallDate)
                {
                    double age = x.InstallAgeDays;
                    if (age >= 0 && age <= 7) recent7++;
                    if (age >= 0 && age <= 30) recent30++;
                    if (age >= 0 && age <= 90) recent90++;
                    if (age > 365) older1Year++;
                }

                if (x.QuietUninstallPossible) quiet++;
                if (x.HasStartupEntries) startup++;
                if (x.IsSigned) signed++;
                else unsigned++;
            }

            CountDesktopApps = desktop;
            CountStoreApps = store;
            CountGames = games;
            CountSystemComponents = system;
            CountUpdates = updates;
            CountWindowsFeatures = winFeatures;

            CountVerified = verified;
            CountProtected = protectedItems;
            CountOrphans = orphans;
            CountInvalid = invalid;

            Count64Bit = bit64;
            Count32Bit = bit32;

            CountLargeSize = large;
            CountMediumSize = medium;
            CountSmallSize = small;
            CountUnknownSize = unknownSize;

            CountRecent7Days = recent7;
            CountRecent30Days = recent30;
            CountRecent90Days = recent90;
            CountOlder1Year = older1Year;

            CountQuiet = quiet;
            CountStartup = startup;
            CountSigned = signed;
            CountUnsigned = unsigned;
        }

        [RelayCommand]
        public void ClearSearch()
        {
            _searchDebounceCts?.Cancel();
            SearchText = string.Empty;
            NotifyFilterChanged();
        }

        [RelayCommand]
        public void ToggleSidebar()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        // 8. Quick Preset Commands
        [RelayCommand]
        public void PresetDefault()
        {
            ShowDesktopApps = true;
            ShowStoreApps = true;
            ShowGames = true;
            ShowSystemComponents = false;
            ShowUpdates = false;
            ShowWindowsFeatures = false;

            ShowVerified = true;
            ShowProtected = true;
            ShowOrphans = true;
            ShowInvalid = true;

            Show64Bit = true;
            Show32Bit = true;

            SelectedSizeFilterIndex = 0;
            SelectedDateFilterIndex = 0;

            ShowOnlyQuiet = false;
            ShowOnlyStartup = false;
            ShowSigned = true;
            ShowUnsigned = true;

            SearchText = string.Empty;
        }

        [RelayCommand]
        public void PresetAll()
        {
            ShowDesktopApps = true;
            ShowStoreApps = true;
            ShowGames = true;
            ShowSystemComponents = true;
            ShowUpdates = true;
            ShowWindowsFeatures = true;

            ShowVerified = true;
            ShowProtected = true;
            ShowOrphans = true;
            ShowInvalid = true;

            Show64Bit = true;
            Show32Bit = true;

            SelectedSizeFilterIndex = 0;
            SelectedDateFilterIndex = 0;

            ShowOnlyQuiet = false;
            ShowOnlyStartup = false;
            ShowSigned = true;
            ShowUnsigned = true;
        }

        [RelayCommand]
        public void PresetIssuesOnly()
        {
            PresetDefault();
            ShowVerified = false;
            ShowProtected = false;
            ShowOrphans = true;
            ShowInvalid = true;
        }

        [RelayCommand]
        public void PresetQuietOnly()
        {
            PresetDefault();
            ShowOnlyQuiet = true;
        }

        [RelayCommand]
        public void PresetStartupOnly()
        {
            PresetDefault();
            ShowOnlyStartup = true;
        }

        [RelayCommand]
        public void PresetLargeOnly()
        {
            PresetDefault();
            SelectedSizeFilterIndex = 1; // > 1 GB
        }

        [RelayCommand]
        public void PresetRecentOnly()
        {
            PresetDefault();
            SelectedDateFilterIndex = 2; // Last 30 Days
        }

        [RelayCommand]
        public void PresetUnsignedOnly()
        {
            PresetDefault();
            ShowSigned = false;
            ShowUnsigned = true;
        }

        [RelayCommand]
        public void PresetGamesOnly()
        {
            PresetDefault();
            ShowDesktopApps = false;
            ShowStoreApps = false;
            ShowGames = true;
        }

        [RelayCommand]
        public void ResetFilters()
        {
            PresetDefault();
        }
    }
}
