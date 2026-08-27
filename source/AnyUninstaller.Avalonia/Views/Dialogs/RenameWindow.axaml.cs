using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AnyUninstaller.Avalonia.Views.Dialogs
{
    public partial class RenameWindow : Window
    {
        public string? NewName { get; private set; }
        public bool Confirmed { get; private set; }

        public RenameWindow()
        {
            InitializeComponent();
        }

        public RenameWindow(string currentName) : this()
        {
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
        }

        private void OnRenameClick(object? sender, RoutedEventArgs e)
        {
            var text = NameTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                NewName = text;
                Confirmed = true;
                Close();
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
