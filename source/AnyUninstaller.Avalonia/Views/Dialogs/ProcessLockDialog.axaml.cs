using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UninstallTools.Junk;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public enum ProcessLockDialogResult
    {
        Cancel,
        EndProcessesAndDelete,
        SkipLockedItems
    }

    public partial class ProcessLockDialog : Window
    {
        public ObservableCollection<LockingProcessInfo> Processes { get; } = new();
        public ProcessLockDialogResult Result { get; private set; } = ProcessLockDialogResult.Cancel;

        public List<LockingProcessInfo> SelectedProcesses => Processes.Where(x => x.IsSelected).ToList();

        public ProcessLockDialog()
        {
            InitializeComponent();
        }

        public ProcessLockDialog(IEnumerable<LockingProcessInfo> processes) : this()
        {
            foreach (var p in processes)
            {
                p.IsSelected = true;
                Processes.Add(p);
            }

            ProcessDataGrid.ItemsSource = Processes;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int procCount = Processes.Count;
            int pathsCount = Processes.Select(x => x.LockedPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            SummaryTextBlock.Text = procCount == 1
                ? $"1 running application is locking {pathsCount} item(s)"
                : $"{procCount} running applications are locking {pathsCount} item(s)";
        }

        private void OnEndProcessesClick(object? sender, RoutedEventArgs e)
        {
            Result = ProcessLockDialogResult.EndProcessesAndDelete;
            Close();
        }

        private void OnSkipClick(object? sender, RoutedEventArgs e)
        {
            Result = ProcessLockDialogResult.SkipLockedItems;
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Result = ProcessLockDialogResult.Cancel;
            Close();
        }
    }
}
