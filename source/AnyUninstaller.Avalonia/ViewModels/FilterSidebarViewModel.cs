using System;
using System.Collections.Generic;
using System.Linq;
using AnyUninstaller.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class FilterSidebarViewModel : ViewModelBase
    {
        // 1. Filter Properties
        [ObservableProperty]
        private bool _showStoreApps = AppSettingsService.Instance.FilterShowStoreApps;

        [ObservableProperty]
        private bool _showSystemComponents = AppSettingsService.Instance.FilterShowSystemComponents;

        [ObservableProperty]
        private bool _showProtected = AppSettingsService.Instance.FilterShowProtected;

        [ObservableProperty]
        private bool _showUpdates = AppSettingsService.Instance.FilterShowUpdates;

        [ObservableProperty]
        private bool _showOrphans = AppSettingsService.Instance.FilterShowOrphans;

        [ObservableProperty]
        private bool _showInvalid = AppSettingsService.Instance.FilterShowInvalid;

        [ObservableProperty]
        private bool _showVerified = AppSettingsService.Instance.FilterShowVerified;

        [ObservableProperty]
        private bool _showOnlyQuiet = AppSettingsService.Instance.FilterShowOnlyQuiet;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isSidebarVisible = AppSettingsService.Instance.IsSidebarVisible;

        // 2. Dynamic Count Badges
        [ObservableProperty]
        private int _countTotal;

        [ObservableProperty]
        private int _countStoreApps;

        [ObservableProperty]
        private int _countSystemComponents;

        [ObservableProperty]
        private int _countProtected;

        [ObservableProperty]
        private int _countUpdates;

        [ObservableProperty]
        private int _countOrphans;

        [ObservableProperty]
        private int _countInvalid;

        [ObservableProperty]
        private int _countVerified;

        [ObservableProperty]
        private int _countQuiet;

        [ObservableProperty]
        private int _filteredCount;

        [ObservableProperty]
        private bool _isFiltered;

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

        public event EventHandler? FilterChanged;

        partial void OnIsSidebarVisibleChanged(bool value)
        {
            AppSettingsService.Instance.IsSidebarVisible = value;
            AppSettingsService.Instance.Save();
        }

        partial void OnShowStoreAppsChanged(bool value) => NotifyFilterChanged();
        partial void OnShowSystemComponentsChanged(bool value) => NotifyFilterChanged();
        partial void OnShowProtectedChanged(bool value) => NotifyFilterChanged();
        partial void OnShowUpdatesChanged(bool value) => NotifyFilterChanged();
        partial void OnShowOrphansChanged(bool value) => NotifyFilterChanged();
        partial void OnShowInvalidChanged(bool value) => NotifyFilterChanged();
        partial void OnShowVerifiedChanged(bool value) => NotifyFilterChanged();
        partial void OnShowOnlyQuietChanged(bool value) => NotifyFilterChanged();
        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasSearchText));
            NotifyFilterChanged();
        }

        private void NotifyFilterChanged()
        {
            IsFiltered = !ShowStoreApps || ShowSystemComponents || !ShowProtected ||
                         ShowUpdates || !ShowOrphans || !ShowInvalid || !ShowVerified ||
                         ShowOnlyQuiet || !string.IsNullOrWhiteSpace(SearchText);

            AppSettingsService.Instance.FilterShowStoreApps = ShowStoreApps;
            AppSettingsService.Instance.FilterShowSystemComponents = ShowSystemComponents;
            AppSettingsService.Instance.FilterShowProtected = ShowProtected;
            AppSettingsService.Instance.FilterShowUpdates = ShowUpdates;
            AppSettingsService.Instance.FilterShowOrphans = ShowOrphans;
            AppSettingsService.Instance.FilterShowInvalid = ShowInvalid;
            AppSettingsService.Instance.FilterShowVerified = ShowVerified;
            AppSettingsService.Instance.FilterShowOnlyQuiet = ShowOnlyQuiet;

            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateCounts(IReadOnlyList<ApplicationEntryViewModel> allEntries, int filtered)
        {
            CountTotal = allEntries.Count;
            FilteredCount = filtered;
            CountStoreApps = allEntries.Count(x => x.IsStoreApp);
            CountSystemComponents = allEntries.Count(x => x.IsSystemComponent && !x.IsProtected && !x.IsOrphaned && x.IsValid && !x.IsUpdate);
            CountProtected = allEntries.Count(x => x.IsProtected);
            CountUpdates = allEntries.Count(x => x.IsUpdate && !x.IsProtected && !x.IsOrphaned && x.IsValid);
            CountOrphans = allEntries.Count(x => x.IsOrphaned);
            CountInvalid = allEntries.Count(x => !x.IsValid);
            CountVerified = allEntries.Count(x => x.IsValid && !x.IsOrphaned && !x.IsProtected && !x.IsSystemComponent && !x.IsUpdate);
            CountQuiet = allEntries.Count(x => x.QuietUninstallPossible);
        }

        // 3. Quick Preset Commands
        [RelayCommand]
        public void PresetDefault()
        {
            ShowStoreApps = true;
            ShowSystemComponents = false;
            ShowProtected = true;
            ShowUpdates = false;
            ShowOrphans = true;
            ShowInvalid = true;
            ShowVerified = true;
            ShowOnlyQuiet = false;
            SearchText = string.Empty;
        }

        [RelayCommand]
        public void PresetAll()
        {
            ShowStoreApps = true;
            ShowSystemComponents = true;
            ShowProtected = true;
            ShowUpdates = true;
            ShowOrphans = true;
            ShowInvalid = true;
            ShowVerified = true;
            ShowOnlyQuiet = false;
        }

        [RelayCommand]
        public void PresetIssuesOnly()
        {
            ShowStoreApps = true;
            ShowSystemComponents = false;
            ShowProtected = false;
            ShowUpdates = false;
            ShowOrphans = true;
            ShowInvalid = true;
            ShowVerified = false;
            ShowOnlyQuiet = false;
        }

        [RelayCommand]
        public void PresetQuietOnly()
        {
            ShowOnlyQuiet = true;
        }

        [RelayCommand]
        public void ResetFilters()
        {
            PresetDefault();
        }

        [RelayCommand]
        public void ClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}
