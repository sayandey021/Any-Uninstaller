using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnyUninstaller.Avalonia.Services;
using Klocman.IO;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class TempItemViewModel : ObservableObject
    {
        public TempItemInfo Info { get; }

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

        public string FullPath => Info.FullPath;
        public string Name => Info.Name;
        public TempCategory Category => Info.Category;
        public string CategoryName => Info.CategoryName;
        public bool IsDirectory => Info.IsDirectory;
        public long SizeBytes => Info.SizeBytes;
        public string SizeFormatted => TempCleanerViewModel.FormatSize(Info.Size);
        public int FileCount => Info.FileCount;
        public DateTime LastModified => Info.LastModified;
        public string TypeIcon => IsDirectory ? "📁" : "📄";

        public string CategoryBrush => Category switch
        {
            TempCategory.UserTemp => "#58a6ff",
            TempCategory.SystemTemp => "#bc8cff",
            TempCategory.CrashDumps => "#f85149",
            TempCategory.UpdateCache => "#3fb950",
            TempCategory.WebCache => "#d29922",
            _ => "#8b949e"
        };

        public string StatusBrush => Status switch
        {
            "Deleted" => "#3fb950",
            "Skipped" or "Partially Cleaned" => "#d29922",
            "Error" => "#f85149",
            _ => "#8b949e"
        };

        public TempItemViewModel(TempItemInfo info)
        {
            Info = info;
            _isChecked = info.IsRecommended;
        }
    }

    public partial class TempCleanerViewModel : ViewModelBase
    {
        private List<TempItemViewModel> _allTempItems = new();
        private CancellationTokenSource? _operationCts;

        [ObservableProperty]
        private ObservableCollection<TempItemViewModel> _filteredTempItems = new();

        [ObservableProperty]
        private TempItemViewModel? _selectedItem;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready to scan temporary files";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private int _progressMax = 100;

        [ObservableProperty]
        private int _totalItemsCount;

        [ObservableProperty]
        private int _selectedItemsCount;

        [ObservableProperty]
        private FileSize _totalSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _selectedSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _userTempSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _systemTempSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _crashDumpsSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _updateCacheSize = FileSize.Empty;

        [ObservableProperty]
        private FileSize _webCacheSize = FileSize.Empty;

        [ObservableProperty]
        private int _selectedCategoryFilterIndex = 0; // 0: All, 1: UserTemp, 2: SystemTemp, 3: CrashDumps, 4: UpdateCache, 5: WebCache

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
        private string _lastFreedSizeFormatted = string.Empty;

        [ObservableProperty]
        private int _remainingUndeletedCount;

        [ObservableProperty]
        private int _remainingUnselectedCount;

        public int RemainingTotalCount => _allTempItems.Count;
        public bool HasRemainingItems => RemainingTotalCount > 0;
        public string ShowRemainingButtonText => HasRemainingItems
            ? $"Show Undeleted & Unselected Items ({RemainingTotalCount})"
            : "No Remaining Items (0)";

        public string CompletionHeadlineText => RemainingTotalCount == 0
            ? "All Temporary Files Cleaned!"
            : "Cleanup Completed!";

        public string CompletionHighlightText => RemainingTotalCount == 0
            ? $"Freed {LastFreedSizeFormatted} across {LastDeletedCount} file(s) • 100% Spotless"
            : $"Freed {LastFreedSizeFormatted} across {LastDeletedCount} file(s)";

        public string CompletionDetailsText
        {
            get
            {
                if (RemainingTotalCount == 0)
                {
                    return $"Spotless! Successfully freed {LastFreedSizeFormatted} across {LastDeletedCount} temporary file(s). All temporary cache locations are now completely clean.";
                }
                else if (RemainingUndeletedCount > 0 && RemainingUnselectedCount > 0)
                {
                    return $"Freed {LastFreedSizeFormatted} across {LastDeletedCount} file(s). {RemainingTotalCount} item(s) remain ({RemainingUndeletedCount} in-use/skipped, {RemainingUnselectedCount} unselected).";
                }
                else if (RemainingUndeletedCount > 0)
                {
                    return $"Freed {LastFreedSizeFormatted} across {LastDeletedCount} file(s). {RemainingUndeletedCount} item(s) could not be removed because they are currently locked or in use by Windows.";
                }
                else
                {
                    return $"Freed {LastFreedSizeFormatted} across {LastDeletedCount} file(s). {RemainingUnselectedCount} item(s) were left intact because they were not selected.";
                }
            }
        }

        public string UserTempSizeFormatted => FormatSize(UserTempSize);
        public string SystemTempSizeFormatted => FormatSize(SystemTempSize);
        public string CrashDumpsSizeFormatted => FormatSize(CrashDumpsSize);
        public string UpdateCacheSizeFormatted => FormatSize(UpdateCacheSize);
        public string WebCacheSizeFormatted => FormatSize(WebCacheSize);
        public string TotalSizeFormatted => FormatSize(TotalSize);
        public string SelectedSizeFormatted => FormatSize(SelectedSize);

        public static string FormatSize(FileSize size)
        {
            if (size.GetKbSize() <= 0)
                return "0 KB";
            var str = size.ToString();
            return string.IsNullOrWhiteSpace(str) ? "0 KB" : str;
        }

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
        public string SelectionSummaryText => $"Selected: {SelectedItemsCount} / {TotalItemsCount} item(s) ({SelectedSizeFormatted})";
        public string DeleteButtonText => SelectedItemsCount > 0 
            ? $"Clean Selected Temp Files ({SelectedSizeFormatted})" 
            : "Clean Selected Temp Files";

        public bool CanDelete => SelectedItemsCount > 0 && !IsBusy;
        public bool CanScan => !IsBusy;
        public bool AreAllSelected => FilteredTempItems.Count > 0 && FilteredTempItems.All(x => x.IsChecked);
        public string ToggleSelectAllText => AreAllSelected ? "Select None" : "Select All";
        public string ToggleSelectAllIcon => AreAllSelected ? "✕" : "✓";
        public string ToggleSelectAllTooltip => AreAllSelected ? "Deselect all visible temp items" : "Select all visible temp items";

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

        public TempCleanerViewModel()
        {
            _ = ScanAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasSearchText));
            ApplyFilter();
        }

        partial void OnSelectedCategoryFilterIndexChanged(int value)
        {
            ApplyFilter();
        }

        [RelayCommand]
        public void ClearSearch()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        public async Task ScanAsync()
        {
            if (IsBusy) return;

            IsCompleted = false;
            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();

            IsBusy = true;
            StatusMessage = "Scanning for temporary files and residual caches...";
            ProgressValue = 0;
            ProgressMax = 100;

            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ProgressValue = p.current;
                ProgressMax = Math.Max(1, p.total);
                StatusMessage = p.message;
            });

            try
            {
                var items = await TempCleaningService.Instance.ScanTempLocationsAsync(progress, _operationCts.Token);
                
                var vms = items.Select(i =>
                {
                    var vm = new TempItemViewModel(i);
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TempItemViewModel.IsChecked))
                        {
                            UpdateSelectionStats();
                        }
                    };
                    return vm;
                }).ToList();

                _allTempItems = vms;
                CalculateCategorySizes();
                ApplyFilter();

                StatusMessage = $"Scan completed. Found {_allTempItems.Count} items ({TotalSizeFormatted}).";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanScan));
            }
        }

        private void CalculateCategorySizes()
        {
            long totalBytes = _allTempItems.Sum(x => x.SizeBytes);
            TotalSize = FileSize.FromBytes(totalBytes);
            TotalItemsCount = _allTempItems.Count;

            UserTempSize = FileSize.FromBytes(_allTempItems.Where(x => x.Category == TempCategory.UserTemp).Sum(x => x.SizeBytes));
            SystemTempSize = FileSize.FromBytes(_allTempItems.Where(x => x.Category == TempCategory.SystemTemp).Sum(x => x.SizeBytes));
            CrashDumpsSize = FileSize.FromBytes(_allTempItems.Where(x => x.Category == TempCategory.CrashDumps).Sum(x => x.SizeBytes));
            UpdateCacheSize = FileSize.FromBytes(_allTempItems.Where(x => x.Category == TempCategory.UpdateCache).Sum(x => x.SizeBytes));
            WebCacheSize = FileSize.FromBytes(_allTempItems.Where(x => x.Category == TempCategory.WebCache).Sum(x => x.SizeBytes));

            OnPropertyChanged(nameof(UserTempSizeFormatted));
            OnPropertyChanged(nameof(SystemTempSizeFormatted));
            OnPropertyChanged(nameof(CrashDumpsSizeFormatted));
            OnPropertyChanged(nameof(UpdateCacheSizeFormatted));
            OnPropertyChanged(nameof(WebCacheSizeFormatted));
            OnPropertyChanged(nameof(TotalSizeFormatted));
            OnPropertyChanged(nameof(SelectedSizeFormatted));
        }

        private void ApplyFilter()
        {
            IEnumerable<TempItemViewModel> query = _allTempItems;

            if (SelectedCategoryFilterIndex > 0)
            {
                var targetCat = (TempCategory)(SelectedCategoryFilterIndex - 1);
                query = query.Where(x => x.Category == targetCat);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(x => 
                    x.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.CategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            FilteredTempItems = new ObservableCollection<TempItemViewModel>(query);
            UpdateSelectionStats();
        }

        private void UpdateSelectionStats()
        {
            var checkedItems = _allTempItems.Where(x => x.IsChecked).ToList();
            SelectedItemsCount = checkedItems.Count;
            SelectedSize = FileSize.FromBytes(checkedItems.Sum(x => x.SizeBytes));

            OnPropertyChanged(nameof(SelectedSizeFormatted));
            OnPropertyChanged(nameof(SelectionSummaryText));
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(AreAllSelected));
            OnPropertyChanged(nameof(ToggleSelectAllText));
            OnPropertyChanged(nameof(ToggleSelectAllIcon));
            OnPropertyChanged(nameof(ToggleSelectAllTooltip));
        }

        [RelayCommand]
        public void ToggleSelectAll()
        {
            if (FilteredTempItems.Count == 0) return;

            bool target = !AreAllSelected;
            foreach (var item in FilteredTempItems)
            {
                item.IsChecked = target;
            }
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var item in FilteredTempItems)
                item.IsChecked = true;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var item in FilteredTempItems)
                item.IsChecked = false;
            UpdateSelectionStats();
        }

        [RelayCommand]
        public async Task DeleteSelectedAsync()
        {
            var selected = _allTempItems.Where(x => x.IsChecked && !x.IsDeleted).ToList();
            if (selected.Count == 0 || IsBusy) return;

            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();

            IsBusy = true;
            StatusMessage = $"Cleaning {selected.Count} selected temporary items...";
            ProgressValue = 0;
            ProgressMax = selected.Count;

            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ProgressValue = p.current;
                ProgressMax = Math.Max(1, p.total);
                StatusMessage = p.message;
            });

            try
            {
                var result = await TempCleaningService.Instance.CleanItemsAsync(
                    selected.Select(x => x.Info),
                    (path, success, errorMsg) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            var vm = selected.FirstOrDefault(x => x.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                            if (vm != null)
                            {
                                if (success && string.IsNullOrEmpty(errorMsg))
                                {
                                    vm.Status = "Deleted";
                                    vm.IsDeleted = true;
                                    vm.IsChecked = false;
                                }
                                else if (success && !string.IsNullOrEmpty(errorMsg))
                                {
                                    vm.Status = "Partially Cleaned";
                                    vm.HasError = true;
                                    vm.ErrorMessage = errorMsg;
                                    vm.IsChecked = false;
                                }
                                else
                                {
                                    vm.Status = "Skipped (In Use)";
                                    vm.HasError = true;
                                    vm.ErrorMessage = errorMsg;
                                }
                            }
                        });
                    },
                    progress,
                    _operationCts.Token);

                // Remove fully deleted items from all items list
                _allTempItems.RemoveAll(x => x.IsDeleted);
                CalculateCategorySizes();
                ApplyFilter();

                string freedSizeStr = FormatSize(result.DeletedSize);
                StatusMessage = result.SkippedFilesCount > 0
                    ? $"Cleanup finished: Deleted {result.DeletedFilesCount} files ({freedSizeStr}), {result.SkippedFilesCount} skipped (in use)."
                    : $"Cleanup finished: Successfully freed {freedSizeStr} ({result.DeletedFilesCount} files deleted)!";

                LastDeletedCount = result.DeletedFilesCount;
                LastFreedSizeFormatted = freedSizeStr;
                RemainingUndeletedCount = _allTempItems.Count(x => x.HasError || x.Status.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase) || x.Status == "Partially Cleaned");
                RemainingUnselectedCount = Math.Max(0, _allTempItems.Count - RemainingUndeletedCount);

                OnPropertyChanged(nameof(RemainingTotalCount));
                OnPropertyChanged(nameof(HasRemainingItems));
                OnPropertyChanged(nameof(ShowRemainingButtonText));
                OnPropertyChanged(nameof(CompletionHeadlineText));
                OnPropertyChanged(nameof(CompletionHighlightText));
                OnPropertyChanged(nameof(CompletionDetailsText));
                IsCompleted = true;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Cleanup cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cleanup error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanScan));
            }
        }

        [RelayCommand]
        public void OpenItemLocation()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath))
                return;

            try
            {
                string targetPath = SelectedItem.FullPath;
                if (SelectedItem.IsDirectory && Directory.Exists(targetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{targetPath}\"",
                        UseShellExecute = true
                    });
                }
                else if (File.Exists(targetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{targetPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        [RelayCommand]
        public async Task RestartExplorerAsync()
        {
            StatusMessage = "Restarting Windows Explorer...";
            bool success = await Task.Run(() => UninstallTools.Junk.ProcessLockHelper.RestartExplorer(force: true));
            StatusMessage = success ? "Windows Explorer restarted successfully." : "Windows Explorer restart requested.";
        }
    }
}
