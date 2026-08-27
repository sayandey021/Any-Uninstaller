using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutViewModel();
        }

        private void OnLinkedInClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AboutViewModel vm)
            {
                AboutViewModel.OpenUrl(vm.LinkedInUrl);
            }
        }

        private void OnGitHubClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AboutViewModel vm)
            {
                AboutViewModel.OpenUrl(vm.GitHubUrl);
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
