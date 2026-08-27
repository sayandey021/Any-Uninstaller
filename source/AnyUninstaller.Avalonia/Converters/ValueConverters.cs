using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Klocman.IO;
using UninstallTools;

namespace AnyUninstaller.Avalonia.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static readonly FileSizeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FileSize fs)
            {
                return fs.GetKbSize() <= 0 ? string.Empty : fs.ToString();
            }
            if (value is long bytes)
            {
                return bytes <= 0 ? string.Empty : FileSize.FromBytes(bytes).ToString();
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateTimeFormatConverter : IValueConverter
    {
        public static readonly DateTimeFormatConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dt && dt > DateTime.MinValue && dt < DateTime.MaxValue)
            {
                return dt.ToShortDateString();
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColorConverter : IValueConverter
    {
        public static readonly StatusColorConverter Instance = new();

        private static readonly IBrush ValidBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));     // Green
        private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40));   // Red
        private static readonly IBrush SystemBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192));   // Blue
        private static readonly IBrush OrphanBrush = new SolidColorBrush(Color.FromRgb(239, 108, 0));    // Orange
        private static readonly IBrush ProtectedBrush = new SolidColorBrush(Color.FromRgb(106, 27, 154));// Purple
        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(120, 144, 156)); // Gray

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ViewModels.ApplicationEntryViewModel vm)
            {
                if (vm.IsProtected) return ProtectedBrush;
                if (vm.IsOrphaned) return OrphanBrush;
                if (vm.IsSystemComponent) return SystemBrush;
                if (!vm.IsValid) return InvalidBrush;
                return ValidBrush;
            }

            if (value is ApplicationUninstallerEntry entry)
            {
                if (entry.IsProtected) return ProtectedBrush;
                if (entry.IsOrphaned) return OrphanBrush;
                if (entry.SystemComponent) return SystemBrush;
                if (!entry.IsValid) return InvalidBrush;
                return ValidBrush;
            }

            return DefaultBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
