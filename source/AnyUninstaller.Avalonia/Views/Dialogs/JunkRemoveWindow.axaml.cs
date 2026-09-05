using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyUninstaller.Avalonia.ViewModels;
using UninstallTools.Junk;
using UninstallTools.Junk.Containers;
using System.Threading.Tasks;

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

            viewModel.PromptLockingProcessesAsync = async (lockingProcs) =>
            {
                var dialog = new ProcessLockDialog(lockingProcs);
                await dialog.ShowDialog(this);
                return dialog.Result;
            };
        }

        private void OnJunkDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            foreach (var item in e.AddedItems.OfType<JunkEntryViewModel>())
            {
                if (!item.IsDeleted)
                    item.IsChecked = true;
            }
        }

        private async void OnCheckLockingClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is JunkRemovalViewModel vm && JunkDataGrid.SelectedItem is JunkEntryViewModel item)
            {
                await vm.CheckLockingProcessesCommand.ExecuteAsync(item);
            }
        }

        private void OnOpenLocationClick(object? sender, RoutedEventArgs e)
        {
            if (JunkDataGrid.SelectedItem is JunkEntryViewModel item)
            {
                if (item.Result is FileSystemJunk fs)
                {
                    var fullPath = fs.Path.FullName;
                    if (Directory.Exists(fullPath))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{fullPath}\"") { UseShellExecute = true });
                    }
                    else if (File.Exists(fullPath))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"") { UseShellExecute = true });
                    }
                }
            }
        }

        private async void OnCopyPathClick(object? sender, RoutedEventArgs e)
        {
            if (JunkDataGrid.SelectedItem is JunkEntryViewModel item && !string.IsNullOrWhiteSpace(item.DisplayName))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(item.DisplayName);
                }
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!ProcessLockHelper.IsShellRunning())
                {
                    ProcessLockHelper.RestartExplorer(force: true);
                }
            });
            Close();
        }
    }
}
