/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Globalization;

namespace UninstallTools.Factory.InfoAdders
{
    public class VersionCleaner : IMissingInfoAdder
    {
        public void AddMissingInformation(ApplicationUninstallerEntry target)
        {
            if (string.IsNullOrEmpty(target.DisplayVersion)) return;

            var rawVersion = target.DisplayVersion.Trim();

            // Handle combined driver date + version format like "01/05/2024 1.19.41.156" or "2024-01-05 1.19.41.156"
            var parts = rawVersion.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ||
                    DateTime.TryParse(parts[0], CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate))
                {
                    if (target.InstallDate == DateTime.MinValue && parsedDate > DateTime.MinValue && parsedDate.Year >= 1970)
                    {
                        target.InstallDate = parsedDate;
                    }
                    rawVersion = parts[1];
                }
                else if (DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate) ||
                         DateTime.TryParse(parts[1], CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate))
                {
                    if (target.InstallDate == DateTime.MinValue && parsedDate > DateTime.MinValue && parsedDate.Year >= 1970)
                    {
                        target.InstallDate = parsedDate;
                    }
                    rawVersion = parts[0];
                }
            }

            target.DisplayVersion = ApplicationEntryTools.CleanupDisplayVersion(rawVersion);
        }

        public string[] RequiredValueNames { get; } = {
            nameof(ApplicationUninstallerEntry.DisplayVersion)
        };

        public bool RequiresAllValues { get; } = true;
        public bool AlwaysRun { get; } = true;
        public string[] CanProduceValueNames { get; } = {
            nameof(ApplicationUninstallerEntry.InstallDate)
        };
        public InfoAdderPriority Priority { get; } = InfoAdderPriority.RunDeadLast;
    }
}