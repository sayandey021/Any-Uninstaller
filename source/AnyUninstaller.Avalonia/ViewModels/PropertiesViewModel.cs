using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using CommunityToolkit.Mvvm.ComponentModel;
using Klocman.Extensions;
using Klocman.IO;
using Klocman.Localising;
using UninstallTools;

namespace AnyUninstaller.Avalonia.ViewModels
{
    public class PropertyItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public PropertyItemViewModel() { }

        public PropertyItemViewModel(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public partial class PropertiesViewModel : ObservableObject
    {
        public ApplicationUninstallerEntry Entry { get; }

        public string WindowTitle => $"Properties {Entry.DisplayName}";
        public string DisplayName => Entry.DisplayName;

        [ObservableProperty]
        private ObservableCollection<PropertyItemViewModel> _overviewItems = new();

        [ObservableProperty]
        private ObservableCollection<PropertyItemViewModel> _uninstallerItems = new();

        [ObservableProperty]
        private ObservableCollection<PropertyItemViewModel> _registryItems = new();

        [ObservableProperty]
        private ObservableCollection<PropertyItemViewModel> _certificateItems = new();

        public PropertiesViewModel(ApplicationUninstallerEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            LoadAllProperties();
        }

        private void LoadAllProperties()
        {
            LoadOverview();
            LoadUninstallerInfo();
            LoadRegistryInfo();
            LoadCertificateInfo();
        }

        private void LoadOverview()
        {
            var items = new List<PropertyItemViewModel>();

            var props = typeof(ApplicationUninstallerEntry).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props.OrderBy(p => p.Name))
            {
                try
                {
                    var val = prop.GetValue(Entry);
                    if (val == null) continue;

                    string strVal;
                    if (val is bool b)
                        strVal = b ? "Yes" : "No";
                    else if (val is DateTime dt)
                    {
                        if (dt == DateTime.MinValue || dt == DateTime.MaxValue) continue;
                        strVal = dt.ToString("dd-MM-yyyy HH:mm:ss");
                    }
                    else if (val is Guid g)
                    {
                        if (g == Guid.Empty) continue;
                        strVal = g.ToString("B");
                    }
                    else if (val is Enum e)
                        strVal = e.ToString();
                    else if (val is ICollection col)
                        strVal = string.Join(" | ", col.Cast<object>().Select(x => x.ToString()));
                    else
                        strVal = val.ToString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(strVal))
                    {
                        string name = prop.GetLocalisedName();
                        if (string.IsNullOrEmpty(name)) name = SplitCamelCase(prop.Name);
                        items.Add(new PropertyItemViewModel(name, strVal));
                    }
                }
                catch { }
            }

            OverviewItems = new ObservableCollection<PropertyItemViewModel>(items.OrderBy(x => x.Name));
        }

        private void LoadUninstallerInfo()
        {
            var items = new List<PropertyItemViewModel>();

            try
            {
                if (!string.IsNullOrEmpty(Entry.UninstallerFullFilename) && File.Exists(Entry.UninstallerFullFilename))
                {
                    var fi = AdvancedFileInfo.FromPath(Entry.UninstallerFullFilename);
                    var props = typeof(AdvancedFileInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props.OrderBy(p => p.Name))
                    {
                        try
                        {
                            var val = prop.GetValue(fi);
                            if (val == null) continue;

                            string strVal;
                            if (val is bool b)
                                strVal = b ? "Yes" : "No";
                            else if (val is DateTime dt)
                                strVal = dt.ToString("dd-MM-yyyy HH:mm:ss");
                            else
                                strVal = val.ToString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(strVal))
                            {
                                string name = prop.GetLocalisedName();
                                if (string.IsNullOrEmpty(name)) name = SplitCamelCase(prop.Name);
                                items.Add(new PropertyItemViewModel(name, strVal));
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    items.Add(new PropertyItemViewModel("Status", "Uninstaller file does not exist on disk or is an MSI / Store package."));
                    if (!string.IsNullOrEmpty(Entry.UninstallerFullFilename))
                        items.Add(new PropertyItemViewModel("Uninstaller Path", Entry.UninstallerFullFilename));
                    if (!string.IsNullOrEmpty(Entry.UninstallString))
                        items.Add(new PropertyItemViewModel("Uninstall Command", Entry.UninstallString));
                }
            }
            catch (Exception ex)
            {
                items.Add(new PropertyItemViewModel("Error", ex.Message));
            }

            UninstallerItems = new ObservableCollection<PropertyItemViewModel>(items.OrderBy(x => x.Name));
        }

        private void LoadRegistryInfo()
        {
            var items = new List<PropertyItemViewModel>();

            try
            {
                if (Entry.IsRegistered)
                {
                    using var targetKey = Entry.OpenRegKey();
                    if (targetKey != null)
                    {
                        var valueNames = targetKey.GetValueNames();
                        foreach (var vName in valueNames)
                        {
                            try
                            {
                                var val = targetKey.GetValue(vName);
                                string strVal = val != null ? val.ToString() ?? string.Empty : string.Empty;
                                string name = string.IsNullOrEmpty(vName) ? "(Default)" : vName;
                                items.Add(new PropertyItemViewModel(name, strVal));
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        items.Add(new PropertyItemViewModel("Status", "Registry key not found or inaccessible."));
                    }
                }
                else
                {
                    items.Add(new PropertyItemViewModel("Status", "Application is not registered in the Windows Registry (Orphaned / Drive / StoreApp)."));
                }
            }
            catch (Exception ex)
            {
                items.Add(new PropertyItemViewModel("Error", ex.Message));
            }

            RegistryItems = new ObservableCollection<PropertyItemViewModel>(items.OrderBy(x => x.Name));
        }

        private void LoadCertificateInfo()
        {
            var items = new List<PropertyItemViewModel>();

            try
            {
                var cert = Entry.GetCertificate();
                if (cert != null)
                {
                    items.Add(new PropertyItemViewModel("Archived", cert.Archived ? "Yes" : "No"));
                    items.Add(new PropertyItemViewModel("Extensions", string.Join(", ", cert.Extensions.Cast<X509Extension>().Where(x => x.Oid != null).Select(x => x.Oid.FriendlyName))));
                    items.Add(new PropertyItemViewModel("Friendly Name", cert.FriendlyName));
                    items.Add(new PropertyItemViewModel("Has Private Key", cert.HasPrivateKey ? "Yes" : "No"));
                    items.Add(new PropertyItemViewModel("Issuer", cert.Issuer));
                    items.Add(new PropertyItemViewModel("Issuer Name", cert.IssuerName.Format(false)));
                    items.Add(new PropertyItemViewModel("Not After", cert.NotAfter.ToString("dd-MM-yyyy HH:mm:ss")));
                    items.Add(new PropertyItemViewModel("Not Before", cert.NotBefore.ToString("dd-MM-yyyy HH:mm:ss")));
                    items.Add(new PropertyItemViewModel("Public Key Algorithm", "RSA"));
                    items.Add(new PropertyItemViewModel("Raw Data", cert.RawData.ToHexString()));
                    items.Add(new PropertyItemViewModel("Serial Number", cert.SerialNumber));
                    items.Add(new PropertyItemViewModel("Signature Algorithm", cert.SignatureAlgorithm?.FriendlyName ?? cert.SignatureAlgorithm?.Value ?? "sha256RSA"));
                    items.Add(new PropertyItemViewModel("Subject", cert.Subject));
                    items.Add(new PropertyItemViewModel("Subject Name", cert.SubjectName.Format(false)));
                    items.Add(new PropertyItemViewModel("Thumbprint", cert.Thumbprint));
                    items.Add(new PropertyItemViewModel("Version", cert.Version.ToString()));
                }
                else
                {
                    items.Add(new PropertyItemViewModel("Status", "No digital certificate found on the uninstaller executable."));
                }
            }
            catch (Exception ex)
            {
                items.Add(new PropertyItemViewModel("Error", ex.Message));
            }

            CertificateItems = new ObservableCollection<PropertyItemViewModel>(items.OrderBy(x => x.Name));
        }

        private static string SplitCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsUpper(c) && sb.Length > 0)
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
