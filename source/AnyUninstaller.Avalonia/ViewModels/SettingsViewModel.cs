using System;
using System.Reflection;
using AnyUninstaller.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UninstallTools;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public const int CategoryGeneral = 0;
        public const int CategoryScanner = 1;
        public const int CategoryColumns = 2;
        public const int CategoryAutomation = 3;
        public const int CategoryAppearance = 4;
        public const int CategoryAbout = 5;

        [ObservableProperty]
        private int _selectedCategoryIndex = 0;

        [ObservableProperty]
        private bool _scanDrives = AppSettingsService.Instance.ScanDrives;

        [ObservableProperty]
        private bool _autoDetectCustomProgramFiles = AppSettingsService.Instance.AutoDetectCustomProgramFiles;

        [ObservableProperty]
        private bool _scanStoreApps = AppSettingsService.Instance.ScanStoreApps;

        [ObservableProperty]
        private bool _scanSystemComponents = AppSettingsService.Instance.ScanSystemComponents;

        [ObservableProperty]
        private bool _scanWindowsUpdates = AppSettingsService.Instance.ScanWindowsUpdates;

        [ObservableProperty]
        private bool _scanProtectedItems = AppSettingsService.Instance.ScanProtectedItems;

        private readonly int _originalThemeIndex = AppSettingsService.Instance.SelectedThemeIndex;
        private readonly bool _originalRoundedCorners = AppSettingsService.Instance.RoundedCorners;
        private readonly bool _originalShowTreemap = AppSettingsService.Instance.ShowTreemap;
        private bool _isSaved = false;

        [ObservableProperty]
        private bool _precacheIcons = AppSettingsService.Instance.PrecacheIcons;

        [ObservableProperty]
        private bool _confirmBeforeUninstall = AppSettingsService.Instance.ConfirmBeforeUninstall;

        [ObservableProperty]
        private bool _defaultQuietUninstall = AppSettingsService.Instance.DefaultQuietUninstall;

        [ObservableProperty]
        private bool _autoKillStuckProcesses = AppSettingsService.Instance.AutoKillStuckProcesses;

        [ObservableProperty]
        private bool _autoScanJunkAfterUninstall = AppSettingsService.Instance.AutoScanJunkAfterUninstall;

        [ObservableProperty]
        private bool _createRestorePoint = AppSettingsService.Instance.CreateRestorePoint;

        [ObservableProperty]
        private int _selectedThemeIndex = AppSettingsService.Instance.SelectedThemeIndex;

        [ObservableProperty]
        private bool _enableAnimations = AppSettingsService.Instance.EnableAnimations;

        [ObservableProperty]
        private bool _showTreemap = AppSettingsService.Instance.ShowTreemap;

        [ObservableProperty]
        private bool _showStatusPills = AppSettingsService.Instance.ShowStatusPills;

        [ObservableProperty]
        private bool _roundedCorners = AppSettingsService.Instance.RoundedCorners;

        // Column Visibility Preferences
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

        public string AppVersion => "1.3.4";
        public string DeveloperTitle => "Developed by";
        public string DeveloperName => "Sayan Dey";
        public string LinkedInDisplay => "www.linkedin.com/in/sayan-dey021";
        public string LinkedInUrl => "https://www.linkedin.com/in/sayan-dey021";
        public string GitHubDisplay => "github.com/sayandey021";
        public string GitHubUrl => "https://github.com/sayandey021/Any-Uninstaller";
        public string PrivacyPolicyUrl => "https://github.com/sayandey021/Any-Uninstaller/blob/main/PRIVACY.md";

        public SettingsViewModel()
        {
            _originalThemeIndex = AppSettingsService.Instance.SelectedThemeIndex;
            _originalRoundedCorners = AppSettingsService.Instance.RoundedCorners;
            _originalShowTreemap = AppSettingsService.Instance.ShowTreemap;
        }

        [RelayCommand]
        public void OpenLinkedIn()
        {
            AboutViewModel.OpenUrl(LinkedInUrl);
        }

        [RelayCommand]
        public void OpenGitHub()
        {
            AboutViewModel.OpenUrl(GitHubUrl);
        }

        [RelayCommand]
        public void OpenPrivacyPolicy()
        {
            AboutViewModel.OpenUrl(PrivacyPolicyUrl);
        }

        public event EventHandler? RequestClose;

        partial void OnSelectedThemeIndexChanged(int value)
        {
            AppSettingsService.Instance.ApplyTheme(value);
        }

        partial void OnRoundedCornersChanged(bool value)
        {
            AppSettingsService.Instance.RoundedCorners = value;
            AppSettingsService.Instance.ApplyTheme(SelectedThemeIndex);
        }

        partial void OnShowTreemapChanged(bool value)
        {
            AppSettingsService.Instance.ShowTreemap = value;
        }

        [RelayCommand]
        public void ShowAllColumns()
        {
            ShowColumnCheckboxes = true;
            ShowColumnPublisher = true;
            ShowColumnVersion = true;
            ShowColumnSize = true;
            ShowColumnStatus = true;
            ShowColumnInstallDate = true;
            ShowColumnType = true;
            ShowColumnQuiet = true;
            ShowColumnLocation = true;
        }

        [RelayCommand]
        public void ResetDefaultColumns()
        {
            ShowColumnCheckboxes = true;
            ShowColumnPublisher = true;
            ShowColumnVersion = true;
            ShowColumnSize = true;
            ShowColumnStatus = true;
            ShowColumnInstallDate = true;
            ShowColumnType = false;
            ShowColumnQuiet = false;
            ShowColumnLocation = true;
        }

        public void RevertPreview()
        {
            if (!_isSaved)
            {
                AppSettingsService.Instance.RoundedCorners = _originalRoundedCorners;
                AppSettingsService.Instance.ShowTreemap = _originalShowTreemap;
                AppSettingsService.Instance.ApplyTheme(_originalThemeIndex);
            }
        }

        [RelayCommand]
        private void Save()
        {
            // Apply scanner configuration
            AppSettingsService.Instance.ScanDrives = ScanDrives;
            AppSettingsService.Instance.AutoDetectCustomProgramFiles = AutoDetectCustomProgramFiles;
            AppSettingsService.Instance.ScanStoreApps = ScanStoreApps;
            AppSettingsService.Instance.ScanSystemComponents = ScanSystemComponents;
            AppSettingsService.Instance.ScanWindowsUpdates = ScanWindowsUpdates;
            AppSettingsService.Instance.ScanProtectedItems = ScanProtectedItems;

            UninstallToolsGlobalConfig.ScanDrives = ScanDrives;
            UninstallToolsGlobalConfig.AutoDetectCustomProgramFiles = AutoDetectCustomProgramFiles;
            UninstallToolsGlobalConfig.ScanStoreApps = ScanStoreApps;
            UninstallToolsGlobalConfig.ScanWinUpdates = ScanWindowsUpdates;

            // Apply app preferences
            AppSettingsService.Instance.ConfirmBeforeUninstall = ConfirmBeforeUninstall;
            AppSettingsService.Instance.PrecacheIcons = PrecacheIcons;
            AppSettingsService.Instance.AutoScanJunkAfterUninstall = AutoScanJunkAfterUninstall;
            AppSettingsService.Instance.DefaultQuietUninstall = DefaultQuietUninstall;
            AppSettingsService.Instance.AutoKillStuckProcesses = AutoKillStuckProcesses;
            AppSettingsService.Instance.CreateRestorePoint = CreateRestorePoint;
            AppSettingsService.Instance.EnableAnimations = EnableAnimations;
            AppSettingsService.Instance.ShowTreemap = ShowTreemap;
            AppSettingsService.Instance.ShowStatusPills = ShowStatusPills;
            AppSettingsService.Instance.RoundedCorners = RoundedCorners;

            // Apply column visibility preferences
            AppSettingsService.Instance.ShowColumnCheckboxes = ShowColumnCheckboxes;
            AppSettingsService.Instance.ShowColumnPublisher = ShowColumnPublisher;
            AppSettingsService.Instance.ShowColumnVersion = ShowColumnVersion;
            AppSettingsService.Instance.ShowColumnSize = ShowColumnSize;
            AppSettingsService.Instance.ShowColumnStatus = ShowColumnStatus;
            AppSettingsService.Instance.ShowColumnInstallDate = ShowColumnInstallDate;
            AppSettingsService.Instance.ShowColumnType = ShowColumnType;
            AppSettingsService.Instance.ShowColumnQuiet = ShowColumnQuiet;
            AppSettingsService.Instance.ShowColumnLocation = ShowColumnLocation;

            AppSettingsService.Instance.ApplyTheme(SelectedThemeIndex);

            // Persist all preferences to file
            AppSettingsService.Instance.Save();

            _isSaved = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            RevertPreview();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void OpenWebsite()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
