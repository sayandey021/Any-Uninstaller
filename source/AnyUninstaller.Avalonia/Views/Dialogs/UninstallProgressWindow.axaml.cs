using Avalonia.Controls;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class UninstallProgressWindow : Window
    {
        public UninstallProgressWindow()
        {
            InitializeComponent();
        }

        public UninstallProgressWindow(UninstallProgressViewModel viewModel) : this()
        {
            DataContext = viewModel;
            Loaded += (s, e) => viewModel.Start();
        }
    }
}
