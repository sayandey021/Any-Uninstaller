using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class JunkRemoveWindow : Window
    {
        public JunkRemoveWindow()
        {
            InitializeComponent();
        }

        public JunkRemoveWindow(JunkRemovalViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
