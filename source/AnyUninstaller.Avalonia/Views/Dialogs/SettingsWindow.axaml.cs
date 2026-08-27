using System;
using Avalonia.Controls;
using AnyUninstaller.Avalonia.ViewModels;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow() : this(0)
        {
        }

        public SettingsWindow(int initialCategoryIndex)
        {
            InitializeComponent();
            var vm = new SettingsViewModel { SelectedCategoryIndex = initialCategoryIndex };
            DataContext = vm;
            vm.RequestClose += (s, e) => Close();
            Closing += (s, e) => vm.RevertPreview();
        }

        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.RequestClose += (s, e) => Close();
            Closing += (s, e) => vm.RevertPreview();
        }
    }
}
