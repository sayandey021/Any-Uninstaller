using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klocman.Tools;
using UninstallTools.Junk;
using UninstallTools.Junk.Confidence;
using UninstallTools.Junk.Containers;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class JunkEntryViewModel : ObservableObject
    {
        public IJunkResult Result { get; }

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _isDeleted;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _status = "Pending";

        public ConfidenceLevel ConfidenceLevel => Result.Confidence?.GetConfidence() ?? ConfidenceLevel.Unknown;
        public bool IsConfident => ConfidenceLevel >= ConfidenceLevel.Good;

        public JunkEntryViewModel(IJunkResult result)
        {
            Result = result;
            // Select only the most confident leftovers by default (Good and VeryGood)
            _isChecked = IsConfident;
        }

        public string DisplayName => Result.GetDisplayName() ?? string.Empty;
        public string ApplicationName => Result.Application?.DisplayName ?? "Unknown";
        public string Confidence => Result.Confidence?.GetConfidence().ToString() ?? "Unknown";
        public string JunkType => Result.GetType().Name.Replace("Junk", "");

        public string ConfidenceBrush => ConfidenceLevel switch
        {
            ConfidenceLevel.VeryGood => "#3fb950",
            ConfidenceLevel.Good => "#58a6ff",
            ConfidenceLevel.Questionable => "#d29922",
            ConfidenceLevel.Bad => "#f85149",
            _ => "#8b949e"
        };
    }

    public partial class JunkRemovalViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<JunkEntryViewModel> _junkItems = new();

        [ObservableProperty]
        private string _statusMessage = "Select junk items to remove";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private bool _isCompleted;

        public bool IsActiveView => !IsCompleted;

        partial void OnIsCompletedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsActiveView));
        }

        [ObservableProperty]
        private int _lastDeletedCount;

        [ObservableProperty]
        private int _remainingUndeletedCount;

        [ObservableProperty]
        private int _remainingUnselectedCount;

        public int RemainingTotalCount => JunkItems.Count;
        public bool HasRemainingItems => RemainingTotalCount > 0;
        public string ShowRemainingButtonText => HasRemainingItems
            ? $"Show Undeleted & Unselected Items ({RemainingTotalCount})"
            : "No Remaining Items (0)";

        public string CompletionHeadlineText => RemainingTotalCount == 0
            ? "All Leftover Junk Cleaned!"
            : "Junk Cleanup Completed!";

        public string CompletionHighlightText => RemainingTotalCount == 0
            ? $"Removed all {LastDeletedCount} leftover item(s) • 100% Clean"
            : $"Successfully removed {LastDeletedCount} leftover item(s)";

        public string CompletionDetailsText
        {
            get
            {
                if (RemainingTotalCount == 0)
                {
                    return $"Spotless! Successfully removed all {LastDeletedCount} residual file(s), folder(s), and registry entry(ies). The system is now completely clean.";
                }
                else if (RemainingUndeletedCount > 0 && RemainingUnselectedCount > 0)
                {
                    return $"Removed {LastDeletedCount} leftover item(s). {RemainingTotalCount} item(s) remain ({RemainingUndeletedCount} in-use/failed, {RemainingUnselectedCount} unselected).";
                }
                else if (RemainingUndeletedCount > 0)
                {
                    return $"Removed {LastDeletedCount} leftover item(s). {RemainingUndeletedCount} item(s) could not be removed because they are currently locked or in use.";
                }
                else
                {
                    return $"Removed {LastDeletedCount} leftover item(s). {RemainingUnselectedCount} unselected item(s) were left intact.";
                }
            }
        }

        public string SelectionSummaryText => $"Selected: {SelectedCount} / {TotalCount} item(s)";
        public string DeleteButtonText => SelectedCount > 0 ? $"Delete Selected Junk ({SelectedCount})" : "Delete Selected Junk";
        public bool CanDelete => SelectedCount > 0 && !IsBusy;
        public bool AreAllSelected => TotalCount > 0 && SelectedCount == TotalCount;
        public string ToggleSelectAllText => AreAllSelected ? "Select None" : "Select All";
        public string ToggleSelectAllIcon => AreAllSelected ? "✕" : "✓";
        public string ToggleSelectAllTooltip => AreAllSelected ? "Deselect all items" : "Select all items";

        [RelayCommand]
        public void ShowRemainingItems()
        {
            IsCompleted = false;
        }

        [RelayCommand]
        public void ShowCompletionSummary()
        {
            IsCompleted = true;
        }

        public JunkRemovalViewModel(IEnumerable<IJunkResult> items)
        {
            foreach (var item in items)
            {
                var vm = new JunkEntryViewModel(item);
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(JunkEntryViewModel.IsChecked))
                    {
                        UpdateSelectionStats();
                    }
                };
                JunkItems.Add(vm);
            }
            UpdateSelectionStats();

            int confidentCount = JunkItems.Count(x => x.IsConfident);
            if (confidentCount > 0 && confidentCount < TotalCount)
            {
                StatusMessage = $"Selected {confidentCount} high-confidence item(s) by default ({TotalCount - confidentCount} questionable unselected).";
            }
            else if (confidentCount == TotalCount && TotalCount > 0)
            {
                StatusMessage = $"Selected all {TotalCount} high-confidence leftover item(s).";
            }
            else
            {
                StatusMessage = "Review and select items to remove.";
            }
        }

        public void UpdateSelectionStats()
        {
            TotalCount = JunkItems.Count;
            SelectedCount = JunkItems.Count(x => x.IsChecked && !x.IsDeleted);
            OnPropertyChanged(nameof(SelectionSummaryText));
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(AreAllSelected));
            OnPropertyChanged(nameof(ToggleSelectAllText));
            OnPropertyChanged(nameof(ToggleSelectAllIcon));
            OnPropertyChanged(nameof(ToggleSelectAllTooltip));
        }

        [RelayCommand]
        public void SelectConfidentOnly()
        {
            foreach (var item in JunkItems)
            {
                if (!item.IsDeleted)
                    item.IsChecked = item.IsConfident;
            }
            UpdateSelectionStats();
            StatusMessage = $"Selected {SelectedCount} high-confidence item(s).";
        }

        [RelayCommand]
        public void ToggleSelectAll()
        {
            bool targetState = !AreAllSelected;
            foreach (var item in JunkItems)
            {
                if (!item.IsDeleted)
                    item.IsChecked = targetState;
            }
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var item in JunkItems)
                item.IsChecked = true;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var item in JunkItems)
                item.IsChecked = false;
            UpdateSelectionStats();
        }

        public Func<List<LockingProcessInfo>, Task<AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult>>? PromptLockingProcessesAsync { get; set; }

        [RelayCommand]
        public async Task CheckLockingProcessesAsync(JunkEntryViewModel? entry)
        {
            if (entry == null || entry.Result is not FileSystemJunk fs)
            {
                StatusMessage = "Selected item is not a file or directory.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Checking for applications locking {entry.DisplayName}...";

            var path = fs.Path.FullName;
            var procs = await Task.Run(() => ProcessLockHelper.FindLockingProcesses(new[] { path }));
            IsBusy = false;

            if (procs.Count == 0)
            {
                StatusMessage = $"No running applications are locking {entry.DisplayName}.";
                return;
            }

            if (PromptLockingProcessesAsync != null)
            {
                var dialogResult = await PromptLockingProcessesAsync(procs);
                if (dialogResult == AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult.EndProcessesAndDelete)
                {
                    IsBusy = true;
                    StatusMessage = "Closing locking applications...";
                    var toKill = procs.Where(x => x.IsSelected).ToList();
                    var pids = toKill.Select(x => x.ProcessId).ToList();
                    var toRestart = toKill.Where(x => x.ShouldRestart).ToList();
                    await Task.Run(() => ProcessLockHelper.TerminateProcesses(pids));

                    StatusMessage = $"Deleting {entry.DisplayName}...";
                    bool deleted = false;
                    string? err = null;
                    try
                    {
                        await Task.Run(() =>
                        {
                            entry.Result.Delete();
                            deleted = true;
                        });
                    }
                    catch (Exception ex)
                    {
                        err = ex.Message;
                    }
                    finally
                    {
                        if (toRestart.Count > 0 || !ProcessLockHelper.IsShellRunning())
                        {
                            await Task.Run(() => ProcessLockHelper.RestartProcesses(toRestart));
                        }
                    }

                    if (deleted)
                    {
                        WindowsTools.NotifyShellAssociationsChanged();
                        entry.IsDeleted = true;
                        entry.HasError = false;
                        entry.ErrorMessage = null;
                        entry.Status = "Deleted";
                        JunkItems.Remove(entry);
                        UpdateSelectionStats();
                        StatusMessage = $"Successfully deleted {entry.DisplayName}.";
                    }
                    else
                    {
                        entry.HasError = true;
                        entry.ErrorMessage = err;
                        entry.Status = err ?? "Delete failed";
                        StatusMessage = $"Could not delete {entry.DisplayName}: {err}";
                    }
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        public async Task DeleteSelectedJunkAsync()
        {
            var selected = JunkItems.Where(x => x.IsChecked && !x.IsDeleted).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "No items selected for deletion.";
                return;
            }

            // Phase 1: Proactively check if any selected files or folders are currently locked by running processes
            var allProcsToRestart = new List<LockingProcessInfo>();
            var fsItems = selected.Where(x => x.Result is FileSystemJunk).ToList();
            if (fsItems.Count > 0 && PromptLockingProcessesAsync != null)
            {
                IsBusy = true;
                StatusMessage = "Checking for in-use files and folders...";

                var pathsToCheck = fsItems.Select(x => ((FileSystemJunk)x.Result).Path.FullName).ToList();
                var lockingProcesses = await Task.Run(() => ProcessLockHelper.FindLockingProcesses(pathsToCheck));

                if (lockingProcesses.Count > 0)
                {
                    IsBusy = false;
                    var dialogResult = await PromptLockingProcessesAsync(lockingProcesses);

                    if (dialogResult == AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult.Cancel)
                    {
                        StatusMessage = "Deletion cancelled.";
                        return;
                    }
                    else if (dialogResult == AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult.SkipLockedItems)
                    {
                        var lockedPaths = lockingProcesses.Select(p => p.LockedPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var item in fsItems)
                        {
                            var fsPath = ((FileSystemJunk)item.Result).Path.FullName;
                            if (lockedPaths.Contains(fsPath))
                            {
                                item.IsChecked = false;
                            }
                        }
                        UpdateSelectionStats();
                        selected = JunkItems.Where(x => x.IsChecked && !x.IsDeleted).ToList();
                        if (selected.Count == 0)
                        {
                            StatusMessage = "All locked items were skipped. No remaining items to delete.";
                            return;
                        }
                    }
                    else if (dialogResult == AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult.EndProcessesAndDelete)
                    {
                        IsBusy = true;
                        StatusMessage = "Closing locking applications...";
                        var toKill = lockingProcesses.Where(x => x.IsSelected).ToList();
                        var pidsToKill = toKill.Select(x => x.ProcessId).ToList();
                        allProcsToRestart.AddRange(toKill.Where(x => x.ShouldRestart));
                        await Task.Run(() => ProcessLockHelper.TerminateProcesses(pidsToKill));
                    }
                }
            }

            IsBusy = true;
            StatusMessage = $"Deleting {selected.Count} leftover item(s)...";

            var successfullyDeleted = new List<JunkEntryViewModel>();
            var errors = new List<string>();

            await Task.Run(() =>
            {
                var junkResults = selected.Select(x => x.Result).ToList();
                var result = JunkManager.DeleteJunkBatch(junkResults, (current, total, msg) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusMessage = $"Cleaning item {current} of {total}...";
                    });
                });

                foreach (var item in selected)
                {
                    if (result.SuccessfullyDeleted.Contains(item.Result))
                    {
                        item.IsDeleted = true;
                        item.HasError = false;
                        item.ErrorMessage = null;
                        item.Status = "Deleted";
                        successfullyDeleted.Add(item);
                    }
                    else if (result.FailedItems.TryGetValue(item.Result, out var errorMsg))
                    {
                        item.HasError = true;
                        item.ErrorMessage = errorMsg;
                        item.Status = errorMsg;
                        errors.Add($"{item.DisplayName}: {errorMsg}");
                    }
                }
            });

            // Phase 2: Post-deletion fallback for any folders/files that still failed because they are in-use or locked
            var failedLockedItems = selected
                .Where(x => x.HasError && !x.IsDeleted && x.Result is FileSystemJunk &&
                            (x.ErrorMessage?.Contains("In use", StringComparison.OrdinalIgnoreCase) == true ||
                             x.ErrorMessage?.Contains("locked", StringComparison.OrdinalIgnoreCase) == true ||
                             x.ErrorMessage?.Contains("access", StringComparison.OrdinalIgnoreCase) == true))
                .ToList();

            if (failedLockedItems.Count > 0 && PromptLockingProcessesAsync != null)
            {
                var failedPaths = failedLockedItems.Select(x => ((FileSystemJunk)x.Result).Path.FullName).ToList();
                var postLockingProcesses = await Task.Run(() => ProcessLockHelper.FindLockingProcesses(failedPaths));

                if (postLockingProcesses.Count > 0)
                {
                    IsBusy = false;
                    var retryResult = await PromptLockingProcessesAsync(postLockingProcesses);

                    if (retryResult == AnyUninstaller.Avalonia.Views.Dialogs.ProcessLockDialogResult.EndProcessesAndDelete)
                    {
                        IsBusy = true;
                        StatusMessage = "Closing locking applications and retrying deletion...";
                        var retryToKill = postLockingProcesses.Where(x => x.IsSelected).ToList();
                        var retryPids = retryToKill.Select(x => x.ProcessId).ToList();
                        allProcsToRestart.AddRange(retryToKill.Where(x => x.ShouldRestart));
                        await Task.Run(() => ProcessLockHelper.TerminateProcesses(retryPids));

                        await Task.Run(() =>
                        {
                            var retryResults = failedLockedItems.Select(x => x.Result).ToList();
                            var retryBatchResult = JunkManager.DeleteJunkBatch(retryResults);

                            foreach (var item in failedLockedItems)
                            {
                                if (retryBatchResult.SuccessfullyDeleted.Contains(item.Result))
                                {
                                    item.IsDeleted = true;
                                    item.HasError = false;
                                    item.ErrorMessage = null;
                                    item.Status = "Deleted";
                                    successfullyDeleted.Add(item);
                                    errors.RemoveAll(err => err.StartsWith(item.DisplayName + ":", StringComparison.OrdinalIgnoreCase));
                                }
                                else if (retryBatchResult.FailedItems.TryGetValue(item.Result, out var newError))
                                {
                                    item.ErrorMessage = newError;
                                    item.Status = newError;
                                }
                            }
                        });
                    }
                }
            }

            // Revive and restart terminated applications (such as Windows Explorer and external apps)
            if (allProcsToRestart.Count > 0 || !ProcessLockHelper.IsShellRunning())
            {
                StatusMessage = "Restarting applications...";
                await Task.Run(() => ProcessLockHelper.RestartProcesses(allProcsToRestart));
            }

            // Remove successfully deleted items from list on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in successfullyDeleted)
                {
                    JunkItems.Remove(item);
                }
                UpdateSelectionStats();
            });

            if (successfullyDeleted.Count > 0)
            {
                WindowsTools.NotifyShellAssociationsChanged();
            }

            if (errors.Count > 0)
            {
                if (successfullyDeleted.Count > 0)
                {
                    StatusMessage = $"Deleted {successfullyDeleted.Count} item(s). Failed {errors.Count}: {errors[0]}";
                }
                else
                {
                    StatusMessage = $"Failed to delete {errors.Count} item(s): {errors[0]}";
                }
            }
            else
            {
                StatusMessage = $"Successfully deleted {successfullyDeleted.Count} item(s).";
            }

            LastDeletedCount = successfullyDeleted.Count;
            RemainingUndeletedCount = JunkItems.Count(x => x.HasError || !string.IsNullOrEmpty(x.ErrorMessage));
            RemainingUnselectedCount = Math.Max(0, JunkItems.Count - RemainingUndeletedCount);

            OnPropertyChanged(nameof(RemainingTotalCount));
            OnPropertyChanged(nameof(HasRemainingItems));
            OnPropertyChanged(nameof(ShowRemainingButtonText));
            OnPropertyChanged(nameof(CompletionHeadlineText));
            OnPropertyChanged(nameof(CompletionHighlightText));
            OnPropertyChanged(nameof(CompletionDetailsText));
            IsCompleted = true;

            IsBusy = false;
        }

        [RelayCommand]
        public async Task RestartExplorerAsync()
        {
            StatusMessage = "Restarting Windows Explorer...";
            bool success = await Task.Run(() => ProcessLockHelper.RestartExplorer(force: true));
            StatusMessage = success ? "Windows Explorer restarted successfully." : "Windows Explorer restart requested.";
        }
    }
}
