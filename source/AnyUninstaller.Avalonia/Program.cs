using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Avalonia;

namespace AnyUninstaller.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("[AnyU.Avalonia] Starting Application...");
            try
            {
                var builder = BuildAvaloniaApp();
                Console.WriteLine("[AnyU.Avalonia] AppBuilder configured. Starting lifetime...");
                builder.StartWithClassicDesktopLifetime(args);
                Console.WriteLine("[AnyU.Avalonia] Lifetime ended normally.");
            }
            catch (Exception ex)
            {
                try
                {
                    var crashPath = Path.Combine(AppContext.BaseDirectory, "anyu_avalonia_crash.log");
                    File.WriteAllText(crashPath, ex.ToString());
                }
                catch { }
                Console.Error.WriteLine("[AnyU.Avalonia Crash] " + ex);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
