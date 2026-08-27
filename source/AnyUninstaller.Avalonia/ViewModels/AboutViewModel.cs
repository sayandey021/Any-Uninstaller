using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class AboutViewModel : ViewModelBase
    {
        public string AppName => "Any Uninstaller";
        public string Version => "1.2.0";
        public string VersionDisplay => "v1.2.0";
        public string Description => "Clean, fast, and effortless batch application uninstaller.";
        public string DeveloperTitle => "Developed by";
        public string DeveloperName => "Sayan Dey";
        public string LinkedInDisplay => "www.linkedin.com/in/sayan-dey021";
        public string LinkedInUrl => "https://www.linkedin.com/in/sayan-dey021";
        public string GitHubDisplay => "github.com/sayandey021";
        public string GitHubUrl => "https://github.com/sayandey021";

        [RelayCommand]
        public void OpenLinkedIn()
        {
            OpenUrl(LinkedInUrl);
        }

        [RelayCommand]
        public void OpenGitHub()
        {
            OpenUrl(GitHubUrl);
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
