using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using UninstallTools;

namespace AnyUninstaller.Avalonia.Services
{
    public class AppSettingsService
    {
        private static AppSettingsService? _instance;
        public static AppSettingsService Instance => _instance ??= new AppSettingsService();

        // 1. General & Uninstallation Preferences
        public bool ConfirmBeforeUninstall { get; set; } = true;
        public bool PrecacheIcons { get; set; } = true;
        public bool AutoScanJunkAfterUninstall { get; set; } = true;
        public bool DefaultQuietUninstall { get; set; } = false;
        public bool AutoKillStuckProcesses { get; set; } = true;
        public bool CreateRestorePoint { get; set; } = false;

        // 2. Scanner & Detection Settings
        public bool ScanDrives { get; set; } = true;
        public bool AutoDetectCustomProgramFiles { get; set; } = true;
        public bool ScanStoreApps { get; set; } = true;
        public bool ScanSystemComponents { get; set; } = true;
        public bool ScanWindowsUpdates { get; set; } = false;
        public bool ScanProtectedItems { get; set; } = true;

        // 3. Appearance Settings
        public int SelectedThemeIndex { get; set; } = 0; // 0 = Use System Theme, 1 = Light, 2 = Dark, 3 = Midnight, 4 = OLED
        public bool EnableAnimations { get; set; } = true;
        public bool ShowTreemap { get; set; } = true;
        public bool ShowStatusPills { get; set; } = true;
        public bool RoundedCorners { get; set; } = true;

        // 4. Column Visibility Preferences
        public bool ShowColumnCheckboxes { get; set; } = true;
        public bool ShowColumnPublisher { get; set; } = true;
        public bool ShowColumnVersion { get; set; } = true;
        public bool ShowColumnSize { get; set; } = true;
        public bool ShowColumnStatus { get; set; } = true;
        public bool ShowColumnInstallDate { get; set; } = true;
        public bool ShowColumnType { get; set; } = false;
        public bool ShowColumnQuiet { get; set; } = false;
        public bool ShowColumnLocation { get; set; } = true;

        // 5. View Layout Preferences
        public bool IsToolbarVisible { get; set; } = true;
        public bool IsSidebarVisible { get; set; } = true;
        public bool IsStatusBarVisible { get; set; } = true;

        // 6. Sidebar Filter Preferences
        public bool FilterShowDesktopApps { get; set; } = true;
        public bool FilterShowStoreApps { get; set; } = true;
        public bool FilterShowGames { get; set; } = true;
        public bool FilterShowSystemComponents { get; set; } = false;
        public bool FilterShowUpdates { get; set; } = false;
        public bool FilterShowWindowsFeatures { get; set; } = false;
        public bool FilterShowProtected { get; set; } = true;
        public bool FilterShowOrphans { get; set; } = true;
        public bool FilterShowInvalid { get; set; } = true;
        public bool FilterShowVerified { get; set; } = true;
        public bool FilterShow64Bit { get; set; } = true;
        public bool FilterShow32Bit { get; set; } = true;
        public int FilterSelectedSizeIndex { get; set; } = 0; // 0 = Any Size, 1 = > 1 GB, 2 = 100 MB - 1 GB, 3 = < 100 MB, 4 = Unknown
        public int FilterSelectedDateIndex { get; set; } = 0; // 0 = Any Time, 1 = Last 7 Days, 2 = Last 30 Days, 3 = Last 90 Days, 4 = Older than 1 Year, 5 = Unknown
        public bool FilterShowOnlyQuiet { get; set; } = false;
        public bool FilterShowOnlyStartup { get; set; } = false;
        public bool FilterShowSigned { get; set; } = true;
        public bool FilterShowUnsigned { get; set; } = true;

        // 7. Window Session State
        public double WindowWidth { get; set; } = 1240;
        public double WindowHeight { get; set; } = 780;
        public bool IsWindowMaximized { get; set; } = false;

        public event Action? SettingsChanged;

        private static string? _cachedSettingsPath;

        public static string GetSettingsFilePath()
        {
            if (_cachedSettingsPath != null) return _cachedSettingsPath;

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var dirInfo = new DirectoryInfo(baseDir);
                if (dirInfo.Name.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && dirInfo.Parent != null)
                {
                    baseDir = dirInfo.Parent.FullName;
                }

                var candidate = Path.Combine(baseDir, "AnyUninstaller_Settings.json");
                if (File.Exists(candidate))
                {
                    _cachedSettingsPath = candidate;
                    return candidate;
                }

                // Check for legacy settings file to migrate
                var legacyCandidate = Path.Combine(baseDir, "AnyU_Avalonia_Settings.json");
                if (File.Exists(legacyCandidate))
                {
                    try { File.Copy(legacyCandidate, candidate); } catch { }
                    if (File.Exists(candidate))
                    {
                        _cachedSettingsPath = candidate;
                        return candidate;
                    }
                }

                // Check directory writability for portable mode
                var testFile = Path.Combine(baseDir, $".perm_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                _cachedSettingsPath = candidate;
                return candidate;
            }
            catch
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Any Uninstaller");
                Directory.CreateDirectory(appData);
                var settingsPath = Path.Combine(appData, "AnyUninstaller_Settings.json");

                if (!File.Exists(settingsPath))
                {
                    var oldAppData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Any Uninstaller",
                        "AnyU_Avalonia_Settings.json");
                    if (File.Exists(oldAppData))
                    {
                        try { File.Copy(oldAppData, settingsPath); } catch { }
                    }
                }

                _cachedSettingsPath = settingsPath;
                return _cachedSettingsPath;
            }
        }

        public void Load()
        {
            try
            {
                var path = GetSettingsFilePath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettingsService>(json);
                if (loaded != null)
                {
                    CopyFrom(loaded);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AnyU.Avalonia] Error loading settings: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                var path = GetSettingsFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(this, options);

                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AnyU.Avalonia] Error saving settings: {ex.Message}");
            }
        }

        public void CopyFrom(AppSettingsService source)
        {
            ConfirmBeforeUninstall = source.ConfirmBeforeUninstall;
            PrecacheIcons = source.PrecacheIcons;
            AutoScanJunkAfterUninstall = source.AutoScanJunkAfterUninstall;
            DefaultQuietUninstall = source.DefaultQuietUninstall;
            AutoKillStuckProcesses = source.AutoKillStuckProcesses;
            CreateRestorePoint = source.CreateRestorePoint;

            ScanDrives = source.ScanDrives;
            AutoDetectCustomProgramFiles = source.AutoDetectCustomProgramFiles;
            ScanStoreApps = source.ScanStoreApps;
            ScanSystemComponents = source.ScanSystemComponents;
            ScanWindowsUpdates = source.ScanWindowsUpdates;
            ScanProtectedItems = source.ScanProtectedItems;

            SelectedThemeIndex = source.SelectedThemeIndex;
            EnableAnimations = source.EnableAnimations;
            ShowTreemap = source.ShowTreemap;
            ShowStatusPills = source.ShowStatusPills;
            RoundedCorners = source.RoundedCorners;

            ShowColumnCheckboxes = source.ShowColumnCheckboxes;
            ShowColumnPublisher = source.ShowColumnPublisher;
            ShowColumnVersion = source.ShowColumnVersion;
            ShowColumnSize = source.ShowColumnSize;
            ShowColumnStatus = source.ShowColumnStatus;
            ShowColumnInstallDate = source.ShowColumnInstallDate;
            ShowColumnType = source.ShowColumnType;
            ShowColumnQuiet = source.ShowColumnQuiet;
            ShowColumnLocation = source.ShowColumnLocation;

            IsToolbarVisible = source.IsToolbarVisible;
            IsSidebarVisible = source.IsSidebarVisible;
            IsStatusBarVisible = source.IsStatusBarVisible;

            FilterShowDesktopApps = source.FilterShowDesktopApps;
            FilterShowStoreApps = source.FilterShowStoreApps;
            FilterShowGames = source.FilterShowGames;
            FilterShowSystemComponents = source.FilterShowSystemComponents;
            FilterShowUpdates = source.FilterShowUpdates;
            FilterShowWindowsFeatures = source.FilterShowWindowsFeatures;
            FilterShowProtected = source.FilterShowProtected;
            FilterShowOrphans = source.FilterShowOrphans;
            FilterShowInvalid = source.FilterShowInvalid;
            FilterShowVerified = source.FilterShowVerified;
            FilterShow64Bit = source.FilterShow64Bit;
            FilterShow32Bit = source.FilterShow32Bit;
            FilterSelectedSizeIndex = source.FilterSelectedSizeIndex;
            FilterSelectedDateIndex = source.FilterSelectedDateIndex;
            FilterShowOnlyQuiet = source.FilterShowOnlyQuiet;
            FilterShowOnlyStartup = source.FilterShowOnlyStartup;
            FilterShowSigned = source.FilterShowSigned;
            FilterShowUnsigned = source.FilterShowUnsigned;

            WindowWidth = source.WindowWidth;
            WindowHeight = source.WindowHeight;
            IsWindowMaximized = source.IsWindowMaximized;
        }

        public void ResetToDefaults()
        {
            ConfirmBeforeUninstall = true;
            PrecacheIcons = true;
            AutoScanJunkAfterUninstall = true;
            DefaultQuietUninstall = false;
            AutoKillStuckProcesses = true;
            CreateRestorePoint = false;

            ScanDrives = true;
            AutoDetectCustomProgramFiles = true;
            ScanStoreApps = true;
            ScanSystemComponents = true;
            ScanWindowsUpdates = false;
            ScanProtectedItems = true;

            SelectedThemeIndex = 0;
            EnableAnimations = true;
            ShowTreemap = true;
            ShowStatusPills = true;
            RoundedCorners = true;

            ShowColumnCheckboxes = true;
            ShowColumnPublisher = true;
            ShowColumnVersion = true;
            ShowColumnSize = true;
            ShowColumnStatus = true;
            ShowColumnInstallDate = true;
            ShowColumnType = true;
            ShowColumnQuiet = true;
            ShowColumnLocation = true;

            IsToolbarVisible = true;
            IsSidebarVisible = true;
            IsStatusBarVisible = true;

            FilterShowDesktopApps = true;
            FilterShowStoreApps = true;
            FilterShowGames = true;
            FilterShowSystemComponents = false;
            FilterShowUpdates = false;
            FilterShowWindowsFeatures = false;
            FilterShowProtected = true;
            FilterShowOrphans = true;
            FilterShowInvalid = true;
            FilterShowVerified = true;
            FilterShow64Bit = true;
            FilterShow32Bit = true;
            FilterSelectedSizeIndex = 0;
            FilterSelectedDateIndex = 0;
            FilterShowOnlyQuiet = false;
            FilterShowOnlyStartup = false;
            FilterShowSigned = true;
            FilterShowUnsigned = true;

            WindowWidth = 1240;
            WindowHeight = 780;
            IsWindowMaximized = false;

            ApplyTheme(0);
            Save();
        }

        private bool _platformSettingsHooked;

        private void EnsurePlatformSettingsHooked()
        {
            if (!_platformSettingsHooked && Application.Current?.PlatformSettings != null)
            {
                _platformSettingsHooked = true;
                Application.Current.PlatformSettings.ColorValuesChanged += (s, e) =>
                {
                    if (SelectedThemeIndex == 0)
                    {
                        ApplyTheme(0);
                    }
                };
            }
        }

        private static bool IsSystemThemeDark()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    return Application.Current.PlatformSettings.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                {
                    return val == 0;
                }
            }
            catch { }

            return true;
        }

        public void ApplyTheme(int themeIndex)
        {
            SelectedThemeIndex = themeIndex;
            if (Application.Current == null) return;

            int effectiveTheme = themeIndex;
            if (themeIndex == 0) // Use System Theme
            {
                EnsurePlatformSettingsHooked();
                effectiveTheme = IsSystemThemeDark() ? 2 : 1; // 2 = Dark Mode, 1 = Light Mode
            }

            Color appBg;
            Color cardBg;
            Color cardBorder;
            Color cardInnerBg;
            Color cardInnerBorder;
            Color toolbarBtnBg;
            Color toolbarBtnBorder;
            Color primaryAccent;
            Color chipBg;
            Color textPrimary;
            Color textSecondary;
            Color gridLines;
            Color rowHover;
            Color rowSelected;
            Color scrollThumb;

            switch (effectiveTheme)
            {
                case 1: // Light Mode
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                    appBg = Color.Parse("#f6f8fa");
                    cardBg = Color.Parse("#ffffff");
                    cardBorder = Color.Parse("#d0d7de");
                    cardInnerBg = Color.Parse("#f6f8fa");
                    cardInnerBorder = Color.Parse("#e1e4e8");
                    toolbarBtnBg = Color.Parse("#ffffff");
                    toolbarBtnBorder = Color.Parse("#d0d7de");
                    primaryAccent = Color.Parse("#0969da");
                    chipBg = Color.Parse("#eaeef2");
                    textPrimary = Color.Parse("#1f2328");
                    textSecondary = Color.Parse("#57606a");
                    gridLines = Color.Parse("#e1e4e8");
                    rowHover = Color.Parse("#0969da18");
                    rowSelected = Color.Parse("#0969da30");
                    scrollThumb = Color.Parse("#8c959f");
                    break;

                case 3: // Midnight Blue
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    appBg = Color.Parse("#070d19");
                    cardBg = Color.Parse("#0f172a");
                    cardBorder = Color.Parse("#1e293b");
                    cardInnerBg = Color.Parse("#070d19");
                    cardInnerBorder = Color.Parse("#1e293b");
                    toolbarBtnBg = Color.Parse("#1e293b");
                    toolbarBtnBorder = Color.Parse("#334155");
                    primaryAccent = Color.Parse("#38bdf8");
                    chipBg = Color.Parse("#1e293b");
                    textPrimary = Color.Parse("#f8fafc");
                    textSecondary = Color.Parse("#94a3b8");
                    gridLines = Color.Parse("#1e293b");
                    rowHover = Color.Parse("#38bdf820");
                    rowSelected = Color.Parse("#38bdf840");
                    scrollThumb = Color.Parse("#475569");
                    break;

                case 4: // OLED Black
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    appBg = Color.Parse("#000000");
                    cardBg = Color.Parse("#111111");
                    cardBorder = Color.Parse("#242424");
                    cardInnerBg = Color.Parse("#000000");
                    cardInnerBorder = Color.Parse("#242424");
                    toolbarBtnBg = Color.Parse("#181818");
                    toolbarBtnBorder = Color.Parse("#2e2e2e");
                    primaryAccent = Color.Parse("#00f2fe");
                    chipBg = Color.Parse("#181818");
                    textPrimary = Color.Parse("#ffffff");
                    textSecondary = Color.Parse("#888888");
                    gridLines = Color.Parse("#222222");
                    rowHover = Color.Parse("#00f2fe20");
                    rowSelected = Color.Parse("#00f2fe40");
                    scrollThumb = Color.Parse("#444444");
                    break;

                case 2: // Dark Mode (Default)
                default:
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    appBg = Color.Parse("#0d1117");
                    cardBg = Color.Parse("#161b22");
                    cardBorder = Color.Parse("#30363d");
                    cardInnerBg = Color.Parse("#0d1117");
                    cardInnerBorder = Color.Parse("#21262d");
                    toolbarBtnBg = Color.Parse("#21262d");
                    toolbarBtnBorder = Color.Parse("#30363d");
                    primaryAccent = Color.Parse("#58a6ff");
                    chipBg = Color.Parse("#21262d");
                    textPrimary = Color.Parse("#e6edf3");
                    textSecondary = Color.Parse("#8b949e");
                    gridLines = Color.Parse("#21262d");
                    rowHover = Color.Parse("#1f293788");
                    rowSelected = Color.Parse("#1d4ed844");
                    scrollThumb = Color.Parse("#555d68");
                    break;
            }

            Application.Current.Resources["AppBgBrush"] = new SolidColorBrush(appBg);
            Application.Current.Resources["CardBgBrush"] = new SolidColorBrush(cardBg);
            Application.Current.Resources["CardBorderBrush"] = new SolidColorBrush(cardBorder);
            Application.Current.Resources["CardInnerBgBrush"] = new SolidColorBrush(cardInnerBg);
            Application.Current.Resources["CardInnerBorderBrush"] = new SolidColorBrush(cardInnerBorder);
            Application.Current.Resources["ToolbarBtnBgBrush"] = new SolidColorBrush(toolbarBtnBg);
            Application.Current.Resources["ToolbarBtnBorderBrush"] = new SolidColorBrush(toolbarBtnBorder);
            Application.Current.Resources["PrimaryAccentBrush"] = new SolidColorBrush(primaryAccent);
            Application.Current.Resources["ChipBgBrush"] = new SolidColorBrush(chipBg);
            Application.Current.Resources["TextPrimaryBrush"] = new SolidColorBrush(textPrimary);
            Application.Current.Resources["TextSecondaryBrush"] = new SolidColorBrush(textSecondary);
            Application.Current.Resources["GridLinesBrush"] = new SolidColorBrush(gridLines);
            Application.Current.Resources["RowHoverBrush"] = new SolidColorBrush(rowHover);
            Application.Current.Resources["RowSelectedBrush"] = new SolidColorBrush(rowSelected);
            Application.Current.Resources["ScrollThumbBrush"] = new SolidColorBrush(scrollThumb);
            Application.Current.Resources["CardCornerRadius"] = RoundedCorners ? new CornerRadius(12) : new CornerRadius(0);
            Application.Current.Resources["CardInnerCornerRadius"] = RoundedCorners ? new CornerRadius(8) : new CornerRadius(0);

            SettingsChanged?.Invoke();
        }
    }
}
