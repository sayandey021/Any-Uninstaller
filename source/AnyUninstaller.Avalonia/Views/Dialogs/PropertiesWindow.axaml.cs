using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyUninstaller.Avalonia.ViewModels;
using UninstallTools;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class PropertiesWindow : Window
    {
        private PropertiesViewModel? ViewModel => DataContext as PropertiesViewModel;

        public PropertiesWindow()
        {
            InitializeComponent();
        }

        public PropertiesWindow(ApplicationUninstallerEntry entry) : this()
        {
            DataContext = new PropertiesViewModel(entry);
        }

        public PropertiesWindow(ApplicationEntryViewModel appVm) : this(appVm.Entry)
        {
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnCopyTabClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null || Clipboard == null) return;

            IEnumerable<PropertyItemViewModel>? currentList = MainTabControl.SelectedIndex switch
            {
                0 => ViewModel.OverviewItems,
                1 => ViewModel.UninstallerItems,
                2 => ViewModel.RegistryItems,
                3 => ViewModel.CertificateItems,
                _ => ViewModel.OverviewItems
            };

            if (currentList == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"--- {ViewModel.DisplayName} Properties ---");
            foreach (var item in currentList)
            {
                sb.AppendLine($"{item.Name}\t{item.Value}");
            }

            await Clipboard.SetTextAsync(sb.ToString());
        }
    }
}
