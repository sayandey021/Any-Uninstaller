using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AnyUninstaller.Avalonia.Services;
using AnyUninstaller.Avalonia.ViewModels;
using AnyUninstaller.Avalonia.Views;

namespace AnyUninstaller.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            Console.WriteLine("[AnyU.Avalonia] App.Initialize: Loading XAML...");
            AvaloniaXamlLoader.Load(this);
            Console.WriteLine("[AnyU.Avalonia] App.Initialize: XAML Loaded.");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Console.WriteLine("[AnyU.Avalonia] OnFrameworkInitializationCompleted: Starting...");

            // Load persisted settings from previous session and apply theme
            AppSettingsService.Instance.Load();
            AppSettingsService.Instance.ApplyTheme(AppSettingsService.Instance.SelectedThemeIndex);

            // Synchronize scanner settings with global config
            UninstallTools.UninstallToolsGlobalConfig.ScanDrives = AppSettingsService.Instance.ScanDrives;
            UninstallTools.UninstallToolsGlobalConfig.AutoDetectCustomProgramFiles = AppSettingsService.Instance.AutoDetectCustomProgramFiles;
            UninstallTools.UninstallToolsGlobalConfig.ScanStoreApps = AppSettingsService.Instance.ScanStoreApps;
            UninstallTools.UninstallToolsGlobalConfig.ScanWinUpdates = AppSettingsService.Instance.ScanWindowsUpdates;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (Array.Exists(desktop.Args ?? Array.Empty<string>(), a => string.Equals(a, "--target-preview", StringComparison.OrdinalIgnoreCase)))
                {
                    desktop.MainWindow = new Views.Dialogs.TargetWindow();
                }
                else
                {
                    Console.WriteLine("[AnyU.Avalonia] Creating MainWindow & MainWindowViewModel...");
                    var mainWindow = new MainWindow();
                    var vm = new MainWindowViewModel();
                    mainWindow.DataContext = vm;
                    desktop.MainWindow = mainWindow;
                    Console.WriteLine("[AnyU.Avalonia] MainWindow assigned to desktop lifetime.");
                }
            }

            base.OnFrameworkInitializationCompleted();
            Console.WriteLine("[AnyU.Avalonia] OnFrameworkInitializationCompleted: Finished.");
        }
    }
}
