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
        private bool _iconLoaded;

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
        public bool QuietUninstallPossible => Entry.QuietUninstallPossible;
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
        public string CertificateIssuer => Entry.GetCertificate()?.Subject ?? "Unsigned / No Certificate";
        public string Comment => Entry.Comment ?? string.Empty;

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
        public bool CanStandardUninstall => !IsProtected && (IsValid || !string.IsNullOrWhiteSpace(UninstallString));
        public bool CanQuietUninstall => !IsProtected && QuietUninstallPossible;
        public bool CanManualUninstall => !string.IsNullOrWhiteSpace(UninstallString) || !string.IsNullOrWhiteSpace(InstallLocation) || !string.IsNullOrWhiteSpace(UninstallerLocation);
        public bool CanModify => IsMsi || !string.IsNullOrWhiteSpace(Entry.ModifyPath);
        public bool HasUninstallString => !string.IsNullOrWhiteSpace(UninstallString);
        public bool HasQuietUninstallString => !string.IsNullOrWhiteSpace(QuietUninstallString);
        public bool HasBundleProviderKey => !string.IsNullOrWhiteSpace(BundleProviderKey);
        public bool HasRegistryPath => !string.IsNullOrWhiteSpace(RegistryPath);
        public bool HasRegistryEntry => !string.IsNullOrWhiteSpace(RegistryPath) || !string.IsNullOrWhiteSpace(RegistryKeyName);
        public bool HasInstallLocation => !string.IsNullOrWhiteSpace(InstallLocation) && System.IO.Directory.Exists(InstallLocation);
        public bool HasUninstallerLocation => !string.IsNullOrWhiteSpace(UninstallerLocation) || (!string.IsNullOrWhiteSpace(UninstallerFullFilename) && System.IO.File.Exists(UninstallerFullFilename));
        public bool HasAboutUrl => !string.IsNullOrWhiteSpace(AboutUrl) && (AboutUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || AboutUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        public bool CanRunExecutable
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InstallLocation) || !System.IO.Directory.Exists(InstallLocation)) return false;
                try
                {
                    return System.IO.Directory.GetFiles(InstallLocation, "*.exe", System.IO.SearchOption.TopDirectoryOnly).Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Bitmap Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _iconLoaded = true;
                    _icon = IconExtractionService.Instance.GetIcon(Entry);
                }
                return _icon ?? IconExtractionService.DefaultApplicationIcon;
            }
        }
    }
}
