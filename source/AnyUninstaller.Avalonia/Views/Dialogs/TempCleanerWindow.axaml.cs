using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class TempCleanerWindow : Window
    {
        public TempCleanerWindow()
        {
            InitializeComponent();
            DataContext = new TempCleanerViewModel();
        }

        public TempCleanerWindow(TempCleanerViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                if (!UninstallTools.Junk.ProcessLockHelper.IsShellRunning())
                {
                    UninstallTools.Junk.ProcessLockHelper.RestartExplorer(force: true);
                }
            });
            Close();
        }
    }
}
