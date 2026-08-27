using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UninstallTools.Uninstaller;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class UninstallTaskItemViewModel : ObservableObject
    {
        public BulkUninstallEntry Entry { get; }

        [ObservableProperty]
        private string _status = "Pending";

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _isFailed;

        public UninstallTaskItemViewModel(BulkUninstallEntry entry)
        {
            Entry = entry;
        }

        public string DisplayName => Entry.UninstallerEntry.DisplayName;
        public string UninstallerKind => Entry.UninstallerEntry.UninstallerKind.ToString();
        public bool IsQuiet => Entry.IsSilentPossible;
    }

    public partial class UninstallProgressViewModel : ViewModelBase
    {
        private readonly BulkUninstallTask _task;

        [ObservableProperty]
        private ObservableCollection<UninstallTaskItemViewModel> _items = new();

        [ObservableProperty]
        private int _completedCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private string _currentStatus = "Initializing...";

        [ObservableProperty]
        private bool _isFinished;

        public event EventHandler? UninstallationFinished;

        public UninstallProgressViewModel(BulkUninstallTask task)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));

            foreach (var target in _task.AllUninstallersList)
            {
                Items.Add(new UninstallTaskItemViewModel(target));
            }

            TotalCount = Items.Count;

            _task.OnStatusChanged += OnTaskStatusChanged;
        }

        public void Start()
        {
            _task.Start();
        }

        private bool _isFinishedInvoked;

        private void OnTaskStatusChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var item in Items)
                {
                    item.Status = item.Entry.CurrentStatus.ToString();
                    if (item.Entry.CurrentStatus == UninstallStatus.Completed)
                    {
                        item.IsCompleted = true;
                    }
                    else if (item.Entry.CurrentStatus == UninstallStatus.Failed || item.Entry.CurrentStatus == UninstallStatus.Invalid)
                    {
                        item.IsFailed = true;
                    }
                }

                CompletedCount = Items.Count(x => x.IsCompleted || x.IsFailed);
                CurrentStatus = $"Uninstalled {CompletedCount} of {TotalCount}";

                if (_task.Finished && !_isFinishedInvoked)
                {
                    _isFinishedInvoked = true;
                    IsFinished = true;
                    CurrentStatus = "Uninstallation batch finished.";
                    UninstallationFinished?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        [RelayCommand]
        public void Abort()
        {
            _task.Aborted = true;
            CurrentStatus = "Aborted by user.";
            IsFinished = true;
        }
    }
}
