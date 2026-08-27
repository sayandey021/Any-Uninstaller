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
            if (!EnsureAdministrator(args))
                return;

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

        private static bool EnsureAdministrator(string[] args)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return true;

            // Check if user or caller explicitly bypassed elevation
            if (Array.Exists(args, a => string.Equals(a, "--no-elevation", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(a, "--user", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(a, "elevated", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    return true;

                // Not running as administrator; relaunch with elevation
                Console.WriteLine("[AnyU.Avalonia] Requesting administrator privileges...");
                var processPath = Environment.ProcessPath;
                var baseDir = AppContext.BaseDirectory;
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Directory.Exists(baseDir) ? baseDir : AppDomain.CurrentDomain.BaseDirectory
                };

                if (!string.IsNullOrEmpty(processPath) && !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.FileName = processPath;
                    startInfo.Arguments = string.Join(" ", args);
                }
                else
                {
                    var exePath = Path.Combine(baseDir, "AnyUninstaller.Avalonia.exe");
                    if (File.Exists(exePath))
                    {
                        startInfo.FileName = exePath;
                        startInfo.Arguments = string.Join(" ", args);
                    }
                    else
                    {
                        startInfo.FileName = "dotnet";
                        var assemblyLocation = Assembly.GetEntryAssembly()?.Location;
                        if (!string.IsNullOrEmpty(assemblyLocation))
                        {
                            startInfo.Arguments = $"\"{assemblyLocation}\" {string.Join(" ", args)}";
                        }
                        else
                        {
                            startInfo.Arguments = $"run --project \"{Path.Combine(baseDir, "..", "..", "..", "source", "AnyUninstaller.Avalonia", "AnyUninstaller.Avalonia.csproj")}\"";
                        }
                    }
                }

                var proc = Process.Start(startInfo);
                if (proc != null)
                {
                    return false; // Elevated child process successfully launched, exit current instance
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Elevation was cancelled or not supported; gracefully continue in standard user mode
                Console.WriteLine("[AnyU.Avalonia] Administrator privileges not granted. Continuing in standard user mode.");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AnyU.Avalonia] Elevation check warning: {ex.Message}. Continuing in standard user mode.");
                return true;
            }

            return true;
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
