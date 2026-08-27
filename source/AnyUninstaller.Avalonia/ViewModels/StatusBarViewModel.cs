using CommunityToolkit.Mvvm.ComponentModel;
using Klocman.IO;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class StatusBarViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int _totalItemsCount;

        [ObservableProperty]
        private int _selectedItemsCount;

        [ObservableProperty]
        private FileSize _totalSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _selectedSize = FileSize.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private int _progressMax = 100;
    }
}
