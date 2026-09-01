using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        public string SelectionSummaryText => $"Selected: {SelectedCount} / {TotalCount} item(s)";
        public string DeleteButtonText => SelectedCount > 0 ? $"🗑️ Delete Selected Junk ({SelectedCount})" : "🗑️ Delete Selected Junk";
        public bool CanDelete => SelectedCount > 0 && !IsBusy;
        public bool AreAllSelected => TotalCount > 0 && SelectedCount == TotalCount;
        public string ToggleSelectAllText => AreAllSelected ? "Select None" : "Select All";
        public string ToggleSelectAllIcon => AreAllSelected ? "✕" : "✓";
        public string ToggleSelectAllTooltip => AreAllSelected ? "Deselect all items" : "Select all items";

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

        [RelayCommand]
        public async Task DeleteSelectedJunkAsync()
        {
            var selected = JunkItems.Where(x => x.IsChecked && !x.IsDeleted).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "No items selected for deletion.";
                return;
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

            // Remove successfully deleted items from list on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in successfullyDeleted)
                {
                    JunkItems.Remove(item);
                }
                UpdateSelectionStats();
            });

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

            IsBusy = false;
        }
    }
}
