using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using AnyUninstaller.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Klocman.IO;
using UninstallTools;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public partial class ApplicationEntryViewModel : ObservableObject
    {
        public ApplicationUninstallerEntry Entry { get; }

        [ObservableProperty]
        private bool _isChecked;

        private Bitmap? _icon;
        private bool _iconLoadingRequested;

        public ApplicationEntryViewModel(ApplicationUninstallerEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        public string DisplayName => Entry.DisplayName ?? string.Empty;
        public string Publisher => Entry.Publisher ?? string.Empty;
        public string DisplayVersion
        {
            get
            {
                var ver = Entry.DisplayVersion ?? string.Empty;
                if (!string.IsNullOrEmpty(ver) && ver.Contains(' '))
                {
                    var parts = ver.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        if (DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ||
                            DateTime.TryParse(parts[0], System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out _))
                        {
                            return parts[1];
                        }
                        if (DateTime.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ||
                            DateTime.TryParse(parts[1], System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out _))
                        {
                            return parts[0];
                        }
                    }
                }
                return ver;
            }
        }

        public DateTime InstallDate
        {
            get
            {
                if (Entry.InstallDate > DateTime.MinValue && Entry.InstallDate < DateTime.MaxValue)
                    return Entry.InstallDate;

                var ver = Entry.DisplayVersion ?? string.Empty;
                if (!string.IsNullOrEmpty(ver) && ver.Contains(' '))
                {
                    var parts = ver.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        if (DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d1) ||
                            DateTime.TryParse(parts[0], System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out d1))
                        {
                            return d1;
                        }
                        if (DateTime.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d2) ||
                            DateTime.TryParse(parts[1], System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out d2))
                        {
                            return d2;
                        }
                    }
                }

                return DateTime.MinValue;
            }
        }
        public FileSize EstimatedSize => Entry.EstimatedSize;
        public string InstallLocation => Entry.InstallLocation ?? string.Empty;
        public string UninstallerKind => Entry.UninstallerKind.ToString();
        public bool QuietUninstallPossible => Entry.QuietUninstallPossible ||
                                              !string.IsNullOrWhiteSpace(Entry.QuietUninstallString) ||
                                              Entry.UninstallerKind == UninstallerType.Msiexec ||
                                              Entry.UninstallerKind == UninstallerType.StoreApp ||
                                              Entry.UninstallerKind == UninstallerType.Steam ||
                                              Entry.UninstallerKind == UninstallerType.Oculus ||
                                              Entry.UninstallerKind == UninstallerType.Chocolatey ||
                                              Entry.UninstallerKind == UninstallerType.WindowsFeature ||
                                              Entry.UninstallerKind == UninstallerType.WindowsUpdate;
        public bool IsValid => Entry.IsValid;
        public bool IsProtected => Entry.IsProtected;
        public bool IsOrphaned => Entry.IsOrphaned;
        public bool IsSystemComponent => Entry.SystemComponent;
        public bool IsUpdate => Entry.IsUpdate;
        public bool IsStoreApp => Entry.UninstallerKind == UninstallerType.StoreApp;
        public string RatingId => Entry.RatingId ?? string.Empty;
        public string RegistryKeyName => Entry.RegistryKeyName ?? string.Empty;
        public string DisplayNameTrimmed => Entry.DisplayNameTrimmed ?? string.Empty;
        public string DisplayIcon => Entry.DisplayIcon ?? string.Empty;
        public IEnumerable<string> SortedExecutables => Entry.GetSortedExecutables();

        // Extended Metadata for ContextMenu & Properties Dialog
        public string AboutUrl => Entry.AboutUrl ?? string.Empty;
        public string BundleProviderKey => Entry.BundleProviderKey != Guid.Empty ? Entry.BundleProviderKey.ToString("B") : string.Empty;
        public string RegistryPath => Entry.RegistryPath ?? string.Empty;
        public string UninstallString => Entry.UninstallString ?? string.Empty;
        public string QuietUninstallString => Entry.QuietUninstallString ?? string.Empty;
        public string UninstallerFullFilename => Entry.UninstallerFullFilename ?? string.Empty;
        public string UninstallerLocation => Entry.UninstallerLocation ?? string.Empty;
        public string InstallSource => Entry.InstallSource ?? string.Empty;
        public bool Is64Bit => Entry.Is64Bit == Klocman.Tools.MachineType.X64 || Entry.Is64Bit == Klocman.Tools.MachineType.Ia64 || Entry.Is64Bit == Klocman.Tools.MachineType.ARM64;
        public string Architecture => Entry.Is64Bit != Klocman.Tools.MachineType.Unknown ? Entry.Is64Bit.ToString() : (Is64Bit ? "64-bit (x64)" : "32-bit (x86)");
        public string CertificateIssuer => IsStoreApp ? "Microsoft Store Verified" : (Entry.IsCertificateValid(true) == true ? "Digitally Signed" : "Unsigned / No Certificate");
        public string Comment => Entry.Comment ?? string.Empty;

        public bool IsGame => Entry.UninstallerKind == UninstallerType.Steam || 
                              Entry.UninstallerKind == UninstallerType.Oculus || 
                              (!string.IsNullOrEmpty(InstallLocation) && (
                                  InstallLocation.Contains("SteamApps", StringComparison.OrdinalIgnoreCase) ||
                                  InstallLocation.Contains("Epic Games", StringComparison.OrdinalIgnoreCase) ||
                                  InstallLocation.Contains("GOG Games", StringComparison.OrdinalIgnoreCase) ||
                                  InstallLocation.Contains("Riot Games", StringComparison.OrdinalIgnoreCase) ||
                                  InstallLocation.Contains("Ubisoft Game Launcher", StringComparison.OrdinalIgnoreCase) ||
                                  InstallLocation.Contains("EA Games", StringComparison.OrdinalIgnoreCase)));
        public bool IsWindowsFeature => Entry.UninstallerKind == UninstallerType.WindowsFeature;
        public bool IsDesktopApp => !IsStoreApp && !IsWindowsFeature && !IsUpdate && !IsSystemComponent && !IsGame;
        public bool IsVerified => IsValid && !IsOrphaned && !IsProtected;

        private bool? _isSigned;
        public bool IsSigned
        {
            get
            {
                if (_isSigned.HasValue) return _isSigned.Value;
                if (IsStoreApp) { _isSigned = true; return true; }
                var valid = Entry.IsCertificateValid(true);
                if (valid.HasValue)
                {
                    _isSigned = valid.Value;
                    return _isSigned.Value;
                }
                return false;
            }
        }

        public bool HasStartupEntries => Entry.HasStartups;
        public long EstimatedSizeKb => EstimatedSize.GetKbSize();
        public bool HasInstallDate => InstallDate > DateTime.MinValue && InstallDate < DateTime.MaxValue && InstallDate.Year >= 1980;
        public double InstallAgeDays => HasInstallDate ? (DateTime.Now - InstallDate).TotalDays : double.MaxValue;

        public string StatusDescription
        {
            get
            {
                if (IsOrphaned) return "Orphaned";
                if (!IsValid) return "Invalid / Broken";
                if (IsProtected) return "Protected";
                if (IsSystemComponent) return "System";
                if (IsUpdate) return "Update";
                return "Verified";
            }
        }

        // ContextMenu & Action Capability Checks
        public bool IsMsi => Entry.UninstallerKind == UninstallerType.Msiexec || Entry.BundleProviderKey != Guid.Empty;
        public bool HasRealUninstaller => IsValid &&
                                          !IsOrphaned &&
                                          Entry.UninstallerKind != UninstallerType.SimpleDelete &&
                                          Entry.UninstallerKind != UninstallerType.Unknown &&
                                          (!string.IsNullOrWhiteSpace(UninstallString) || IsMsi);
        public bool CanStandardUninstall => !IsProtected && HasRealUninstaller;
        public bool CanQuietUninstall => !IsProtected && HasRealUninstaller && QuietUninstallPossible;
        public bool CanManualUninstall => Entry != null;
        public bool CanModify => IsMsi || !string.IsNullOrWhiteSpace(Entry.ModifyPath);
        public bool HasUninstallString => !string.IsNullOrWhiteSpace(UninstallString);
        public bool HasQuietUninstallString => !string.IsNullOrWhiteSpace(QuietUninstallString);
        public bool HasBundleProviderKey => !string.IsNullOrWhiteSpace(BundleProviderKey);
        public bool HasRegistryPath => !string.IsNullOrWhiteSpace(RegistryPath);
        public bool HasRegistryEntry => !string.IsNullOrWhiteSpace(RegistryPath) || !string.IsNullOrWhiteSpace(RegistryKeyName);

        private bool? _hasInstallLocation;
        public bool HasInstallLocation => _hasInstallLocation ??= (!string.IsNullOrWhiteSpace(InstallLocation) && System.IO.Directory.Exists(InstallLocation));

        private bool? _hasUninstallerLocation;
        public bool HasUninstallerLocation => _hasUninstallerLocation ??= (!string.IsNullOrWhiteSpace(UninstallerLocation) || (!string.IsNullOrWhiteSpace(UninstallerFullFilename) && System.IO.File.Exists(UninstallerFullFilename)));

        public bool HasAboutUrl => !string.IsNullOrWhiteSpace(AboutUrl) && (AboutUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || AboutUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        private bool? _canRunExecutable;
        public bool CanRunExecutable
        {
            get
            {
                if (_canRunExecutable.HasValue) return _canRunExecutable.Value;
                if (string.IsNullOrWhiteSpace(InstallLocation) || !HasInstallLocation)
                {
                    _canRunExecutable = false;
                    return false;
                }
                try
                {
                    _canRunExecutable = System.IO.Directory.GetFiles(InstallLocation, "*.exe", System.IO.SearchOption.TopDirectoryOnly).Length > 0;
                }
                catch
                {
                    _canRunExecutable = false;
                }
                return _canRunExecutable.Value;
            }
        }

        public Bitmap Icon
        {
            get
            {
                if (_icon != null) return _icon;

                if (IconExtractionService.Instance.TryGetCachedIcon(Entry, out var cached) && cached != null)
                {
                    _icon = cached;
                    return _icon;
                }

                if (!_iconLoadingRequested)
                {
                    _iconLoadingRequested = true;
                    _ = LoadIconAsync();
                }

                return IconExtractionService.GetFallbackIcon(Entry);
            }
        }

        private async System.Threading.Tasks.Task LoadIconAsync()
        {
            try
            {
                var loaded = await IconExtractionService.Instance.GetIconAsync(Entry);
                if (loaded != null)
                {
                    await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _icon = loaded;
                        OnPropertyChanged(nameof(Icon));
                    });
                }
            }
            catch
            {
                // Ignore background extraction exceptions
            }
        }
    }
}
