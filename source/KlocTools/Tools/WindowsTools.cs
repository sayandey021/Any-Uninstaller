/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Klocman.Extensions;
using Klocman.Native;
using Microsoft.Win32;

namespace Klocman.Tools
{
    public static class WindowsTools
    {
        public static Version Windows10 => new(10, 0);
        public static Version Windows8P1 => new(6, 3);
        public static Version WindowsServer2012R2 => new(6, 3);
        public static Version Windows8 => new(6, 2);
        public static Version WindowsServer2012 => new(6, 2);
        public static Version Windows7 => new(6, 1);
        public static Version WindowsServer2008R2 => new(6, 1);
        public static Version WindowsServer2008 => new(6, 0);
        public static Version WindowsVista => new(6, 0);
        public static Version WindowsServer2003R2 => new(5, 2);
        public static Version WindowsServer2003 => new(5, 2);
        public static Version WindowsXp64 => new(5, 2);
        public static Version WindowsXp => new(5, 1);
        public static Version Windows2000 => new(5, 0);

        /// <summary>
        /// Check if the current process is running with elevated Administrator privileges.
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if .NET Framework v4 is available. Returns null if not installed, or highest installed version.
        /// </summary>
        /// <param name="full">If true, check for full version, otherwise check for client version.</param>
        public static Version CheckNetFramework4Installed(bool full)
        {
            using (var ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                if (ndpKey == null) return null;

                var keys = ndpKey.GetSubKeyNames();
                if (!keys.Contains("v4")) return null;

                using (var netKey = ndpKey.OpenSubKey("v4"))
                {
                    if (netKey == null) return null;

                    keys = netKey.GetSubKeyNames();
                    var subKeyName = full ? "Full" : "Client";
                    if (!keys.Contains(subKeyName)) return null;

                    using (var subKey = netKey.OpenSubKey(subKeyName))
                    {
                        if (subKey?.GetValue("Install", "")?.ToString() != "1") return null;

                        var version = subKey.GetStringSafe("Version");
                        return string.IsNullOrEmpty(version) ? new Version(4, 0, 0) : new Version(version);
                    }
                }
            }
        }

        /// <summary>
        /// Check if .NET Framework v3.5 is available.
        /// </summary>
        public static bool CheckNetFramework35Installed()
        {
            try
            {
                AppDomain.CurrentDomain.Load(
                    "System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static readonly string CplPath = Path.Combine(Environment.SystemDirectory, "control.exe");

        private static readonly IEnumerable<string> LibraryTypes = new[]
        {
            "DLL",
            "SYS"
        };

        private static readonly IEnumerable<string> SystemExecutableTypes = new[]
        {
            "BAT",
            "BIN",
            "CMD",
            "COM",
            "CPL",
            "EXE",
            "GADGET",
            "INF1",
            "INS",
            "INX",
            "ISU",
            "JOB",
            "JSE",
            "LNK",
            "MSC",
            "MSI",
            "MSP",
            "MST",
            "PAF",
            "PIF",
            "PS1",
            "REG",
            "RGS",
            "SCT",
            "SHB",
            "SHS",
            "U3P",
            "VB",
            "VBE",
            "VBS",
            "VBSCRIPT",
            "WS",
            "WSF"
        };

        private static readonly IEnumerable<string> ThirdPartyExecutableTypes = new[]
        {
            "0XE",
            "73K",
            "89K",
            "A6P",
            "AC",
            "ACC",
            "ACR",
            "ACTM",
            "AHK",
            "AIR",
            "APP",
            "ARSCRIPT",
            "AS",
            "ASB",
            "AWK",
            "AZW2",
            "BEAM",
            "BTM",
            "CEL",
            "CELX",
            "CHM",
            "COF",
            "CRT",
            "DEK",
            "DLD",
            "DMC",
            "DOCM",
            "DOTM",
            "DXL",
            "EAR",
            "EBM",
            "EBS",
            "EBS2",
            "ECF",
            "EHAM",
            "ELF",
            "ES",
            "EX4",
            "EXOPC",
            "EZS",
            "FAS",
            "FKY",
            "FPI",
            "FRS",
            "FXP",
            "GS",
            "HAM",
            "HMS",
            "HPF",
            "HTA",
            "IIM",
            "IPF",
            "ISP",
            "JAR",
            "JS",
            "JSX",
            "KIX",
            "LO",
            "LS",
            "MAM",
            "MCR",
            "MEL",
            "MPX",
            "MRC",
            "MS",
            "MS",
            "MXE",
            "NEXE",
            "OBS",
            "ORE",
            "OTM",
            "PEX",
            "PLX",
            "POTM",
            "PPAM",
            "PPSM",
            "PPTM",
            "PRC",
            "PVD",
            "PWC",
            "PYC",
            "PYO",
            "QPX",
            "RBX",
            "ROX",
            "RPJ",
            "S2A",
            "SBS",
            "SCA",
            "SCAR",
            "SCB",
            "SCR",
            "SCRIPT",
            "SMM",
            "SPR",
            "TCP",
            "THM",
            "TLB",
            "TMS",
            "UDF",
            "UPX",
            "URL",
            "VLX",
            "VPM",
            "WCM",
            "WIDGET",
            "WIZ",
            "WPK",
            "WPM",
            "XAP",
            "XBAP",
            "XLAM",
            "XLM",
            "XLSM",
            "XLTM",
            "XQT",
            "XYS",
            "ZL9"
        };

        private static long _uniqueUserId;
        public static string DefaultValueName => string.Empty;

        public static string GetEnvironmentPath(CSIDL target)
        {
            var path = new StringBuilder(260);
            NativeMethods.SHGetSpecialFolderPath(IntPtr.Zero, path, (int)target, false);
            return path.ToString();
        }

        /// <summary>
        ///     Return 32 bit program files directory.
        /// </summary>
        public static string GetProgramFilesX86Path()
        {
            var result = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (result.IsNotEmpty())
                return result;

            return GetEnvironmentPath(CSIDL.CSIDL_PROGRAM_FILES);
        }

        /// <summary>
        ///     Returns path of the system drive, for example C:\
        /// </summary>
        public static string GetSystemDrive()
        {
            return Path.GetPathRoot(GetEnvironmentPath(CSIDL.CSIDL_WINDOWS));
        }

        /// <summary>
        ///     Returns name of the system drive, for example C: (without the slash)
        /// </summary>
        public static string GetSystemDriveName()
        {
            return Path.GetPathRoot(GetEnvironmentPath(CSIDL.CSIDL_WINDOWS))?.TrimEnd('\\', '/');
        }

        /// <summary>
        ///     Based on User Sid, not guaranteed to be unique, but should be good enough.
        /// </summary>
        public static long GetUniqueUserId()
        {
            if (_uniqueUserId == 0)
            {
                var digitsOnly = new Regex(@"[^\d]");
                var parts = digitsOnly.Replace(GetUserSid().Value, " ")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    _uniqueUserId += long.Parse(part);
                }
            }
            return _uniqueUserId;
        }

        public static SecurityIdentifier GetUserSid()
        {
            return WindowsIdentity.GetCurrent().User;
        }

        /// <summary>
        /// Returns executable paths of all installed web browsers
        /// </summary>
        public static string[] GetInstalledWebBrowsers()
        {
            // Check for built-in browsers that obviously don't have to conform to standards
            var results = new List<string>(new[]
            {
                Path.Combine(GetProgramFilesX86Path(), @"Internet Explorer\iexplore.exe"),
                Path.Combine(GetEnvironmentPath(CSIDL.CSIDL_PROGRAM_FILES), @"Internet Explorer\iexplore.exe"),
                Path.Combine(GetEnvironmentPath(CSIDL.CSIDL_WINDOWS), @"SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe\MicrosoftEdgeCP.exe"),
                Path.Combine(GetEnvironmentPath(CSIDL.CSIDL_WINDOWS), @"SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe\MicrosoftEdge.exe")
            }.Where(File.Exists));

            // Check for 3rd party browsers in standard reg keys
            foreach (var internetKeyName in new[]
            {
                @"SOFTWARE\Clients\StartMenuInternet",
                @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet"
            })
            {
                using (var key = Registry.LocalMachine.OpenSubKey(internetKeyName))
                {
                    if (key == null) continue;

                    foreach (var registryKey in key.GetSubKeyNames())
                    {
                        var subkey = registryKey + @"\shell\open\command";
                        try
                        {
                            using var commandKey = key.OpenSubKey(subkey);
                            var path = commandKey?.GetStringSafe(null);
                            if (path != null) results.Add(path.Trim('\"'));
                        }
                        catch (SystemException e)
                        {
                            // Most likey access denied, safe to ignore since these are not critical
                            Console.WriteLine($@"Failed to open {key.Name}\{subkey} - {e}");
                        }
                    }
                }
            }

            return results.Distinct((x, y) => x.Equals(y, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }

        /// <summary>
        ///     Check if the file can be executed.
        ///     Only the string is compared, the path or file doesn't have to exist.
        /// </summary>
        /// <param name="filename">Path containing the file name, it must contain the extension. The file doesn't have to exist.</param>
        /// <param name="onlySystemTypes">Should file types executed by third party applications be included?</param>
        public static bool IsExectuable(string filename, bool onlySystemTypes)
        {
            return IsExectuable(filename, onlySystemTypes, false);
        }

        /// <summary>
        ///     Check if the file can be executed and optionally if it's a library.
        ///     Only the string is compared, the path or file doesn't have to exist.
        /// </summary>
        /// <param name="filename">Path containing the file name, it must contain the extension. The file doesn't have to exist.</param>
        /// <param name="onlySystemTypes">Should file types executed by third party applications be included?</param>
        /// <param name="includeLibraries">Should library file types be included in the comparison?</param>
        public static bool IsExectuable(string filename, bool onlySystemTypes, bool includeLibraries)
        {
            filename = filename.ExtendedTrimEndAny(new[] { "'", "\"" }, StringComparison.CurrentCultureIgnoreCase);

            if (includeLibraries &&
                LibraryTypes.Any(x => filename.EndsWith(x, StringComparison.CurrentCultureIgnoreCase)))
                return true;

            if (SystemExecutableTypes.Any(x => filename.EndsWith(x, StringComparison.CurrentCultureIgnoreCase)))
                return true;

            if (!onlySystemTypes &&
                ThirdPartyExecutableTypes.Any(x => filename.EndsWith(x, StringComparison.CurrentCultureIgnoreCase)))
                return true;

            return false;
        }

        /// <summary>
        ///     Indicates whether any network connection is available
        ///     Filter connections below a specified speed, as well as virtual network cards.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if a network connection is available; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNetworkAvailable()
        {
            return IsNetworkAvailable(0);
        }

        /// <summary>
        ///     Indicates whether any network connection is available.
        ///     Filter connections below a specified speed, as well as virtual network cards.
        /// </summary>
        /// <param name="minimumSpeed">The minimum speed required. Passing 0 will not filter connection using speed.</param>
        /// <returns>
        ///     <c>true</c> if a network connection is available; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNetworkAvailable(long minimumSpeed)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // discard because of standard reasons
                if ((ni.OperationalStatus != OperationalStatus.Up) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                    continue;

                // this allow to filter modems, serial, etc.
                if (ni.Speed < minimumSpeed)
                    continue;

                // discard virtual cards (virtual box, virtual pc, etc.)
                if ((ni.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase)) ||
                    (ni.Name.Contains("virtual", StringComparison.OrdinalIgnoreCase)))
                    continue;

                // discard "Microsoft Loopback Adapter", it will not show as NetworkInterfaceType.Loopback but as Ethernet Card.
                if (ni.Description.Equals("Microsoft Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                    continue;

                return true;
            }
            return false;
        }

        /// <summary>
        ///     Open target control panel applet and return immidiately.
        /// </summary>
        /// <exception cref="IOException">Failed to open control panel entry</exception>
        public static void OpenControlPanelApplet(ControlPanelCanonicalNames target)
        {
            try
            {
                new ProcessStartInfo(CplPath, "/name Microsoft." + target) { UseShellExecute = true }.Start();
            }
            catch (Exception ex)
            {
                throw new IOException("Failed to open control panel entry", ex);
            }
        }

        /// <exception cref="ArgumentNullException">The value of 'objectPath' cannot be null. </exception>
        /// <exception cref="IOException">Failed to start explorer. </exception>
        public static void OpenExplorerFocusedOnObject(string objectPath)
        {
            if (objectPath == null)
            {
                throw new ArgumentNullException(nameof(objectPath));
            }
            try
            {
                new ProcessStartInfo("explorer.exe", string.Format("/select,\"{0}\"", objectPath)).Start();
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new IOException(e.Message, e);
            }
        }

        #region Shell & File System Notifications / Operations

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        private const uint SHCNF_FLUSH = 0x1000;

        /// <summary>
        /// Notifies Windows Explorer and the Shell that file associations, namespace tree items (e.g. OneDrive/Cloud roots),
        /// or icons have changed, instantly updating Explorer without restarting explorer.exe.
        /// </summary>
        public static void NotifyShellAssociationsChanged()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        [Flags]
        public enum MoveFileFlags : uint
        {
            MOVEFILE_REPLACE_EXISTING = 0x00000001,
            MOVEFILE_COPY_ALLOWED = 0x00000002,
            MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004,
            MOVEFILE_WRITE_THROUGH = 0x00000008
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, MoveFileFlags dwFlags);

        /// <summary>
        /// Registers a locked file or directory to be deleted on the next system reboot via PendingFileRenameOperations.
        /// This allows removal of files locked by Windows Explorer (such as in-use shell extension DLLs) without restarting Explorer.
        /// </summary>
        public static bool ScheduleDeleteOnReboot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return MoveFileEx(path, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Takes ownership of a file or directory for the Administrators group,
        /// grants full control ACL permissions, and strips read-only/system attributes.
        /// If the path is within WindowsApps, ensures Administrators have traverse rights.
        /// </summary>
        public static bool TakeOwnershipAndGrantPermissions(string path, bool isDirectory)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var escaped = path.Replace("\"", "\\\"");

                // If path is inside WindowsApps, ensure Administrators can traverse WindowsApps root
                try
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    var windowsApps = Path.Combine(programFiles, "WindowsApps");
                    if (path.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase))
                    {
                        var psiTraverse = new ProcessStartInfo("cmd.exe", $"/c takeown /f \"{windowsApps}\" /a >nul 2>&1 & icacls \"{windowsApps}\" /grant *S-1-5-32-544:(RX) >nul 2>&1")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var pTrav = Process.Start(psiTraverse);
                        pTrav?.WaitForExit(10000);
                    }
                }
                catch { }

                string cmd;
                if (isDirectory)
                {
                    cmd = $"/c takeown /f \"{escaped}\" /a /r /d y /skipsl >nul 2>&1 & " +
                          $"icacls \"{escaped}\" /grant:r *S-1-5-32-544:(OI)(CI)F /t /c /q >nul 2>&1 & " +
                          $"attrib -r -s -h \"{escaped}\\*.*\" /s /d >nul 2>&1 & " +
                          $"attrib -r -s -h \"{escaped}\" >nul 2>&1";
                }
                else
                {
                    cmd = $"/c takeown /f \"{escaped}\" /a >nul 2>&1 & " +
                          $"icacls \"{escaped}\" /grant:r *S-1-5-32-544:F /c /q >nul 2>&1 & " +
                          $"attrib -r -s -h \"{escaped}\" >nul 2>&1";
                }

                var psi = new ProcessStartInfo("cmd.exe", cmd)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(30000);
                return p?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        public static string ResolveShortcut(string filename)
        {
            var link = new NativeMethods.ShellLink();
            // ReSharper disable once SuspiciousTypeConversion.Global
            ((NativeMethods.IPersistFile)link).Load(filename, NativeMethods.STGM_READ);
            var sb = new StringBuilder(NativeMethods.MAX_PATH);
            var data = new NativeMethods.WIN32_FIND_DATAW();
            // ReSharper disable once SuspiciousTypeConversion.Global
            ((NativeMethods.IShellLinkW)link).GetPath(sb, sb.Capacity, ref data, 0);
            return sb.ToString();
        }

        /// <summary>
        /// Flash a window to indicate a need of attention
        /// </summary>
        public static bool FlashWindowEx(Form form)
        {
            var hWnd = form.Handle;
            var fInfo = new NativeMethods.FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;
            if (!form.Focused)
            {
                fInfo.dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG;
                fInfo.uCount = uint.MaxValue;
                fInfo.dwTimeout = 0;
            }
            else
            {
                fInfo.dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMER;
                fInfo.uCount = 3;
                fInfo.dwTimeout = 0;
            }

            return NativeMethods.FlashWindowEx(ref fInfo);
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

            [StructLayout(LayoutKind.Sequential)]
            internal struct FLASHWINFO
            {
                /// <summary>
                /// The size of the structure in bytes.
                /// </summary>
                public uint cbSize;
                /// <summary>
                /// A Handle to the Window to be Flashed. The window can be either opened or minimized.
                /// </summary>
                public IntPtr hwnd;
                /// <summary>
                /// The Flash Status.
                /// </summary>
                public uint dwFlags;
                /// <summary>
                /// The number of times to Flash the window.
                /// </summary>
                public uint uCount;
                /// <summary>
                /// The rate at which the Window is to be flashed, in milliseconds. If Zero, the function uses the default cursor blink rate.
                /// </summary>
                public uint dwTimeout;
            }

            /// <summary>
            /// Stop flashing. The system restores the window to its original stae.
            /// </summary>
            internal const uint FLASHW_STOP = 0;

            /// <summary>
            /// Flash the window caption.
            /// </summary>
            internal const uint FLASHW_CAPTION = 1;

            /// <summary>
            /// Flash the taskbar button.
            /// </summary>
            internal const uint FLASHW_TRAY = 2;

            /// <summary>
            /// Flash both the window caption and taskbar button.
            /// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
            /// </summary>
            internal const uint FLASHW_ALL = 3;

            /// <summary>
            /// Flash continuously, until the FLASHW_STOP flag is set.
            /// </summary>
            internal const uint FLASHW_TIMER = 4;

            /// <summary>
            /// Flash continuously until the window comes to the foreground.
            /// </summary>
            internal const uint FLASHW_TIMERNOFG = 12;

            internal const uint STGM_READ = 0;
            internal const int MAX_PATH = 260;
            //public const int CSIDL_COMMON_STARTMENU = 0x16; // All Users\Start Menu
            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out] StringBuilder lpszPath,
                int nFolder, bool fCreate);

            /*
                        [DllImport("shfolder.dll", CharSet = CharSet.Auto)]
                        internal static extern int SHGetFolderPath(IntPtr hwndOwner, int nFolder, IntPtr hToken, int dwFlags,
                            StringBuilder lpszPath);
            */

            [Flags]
            internal enum SLGP_FLAGS
            {
                /// <summary>Retrieves the standard short (8.3 format) file name</summary>
                SLGP_SHORTPATH = 0x1,

                /// <summary>Retrieves the Universal Naming Convention (UNC) path name of the file</summary>
                SLGP_UNCPRIORITY = 0x2,

                /// <summary>
                ///     Retrieves the raw path name. A raw path is something that might not exist and may include environment
                ///     variables that need to be expanded
                /// </summary>
                SLGP_RAWPATH = 0x4
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            internal struct WIN32_FIND_DATAW
            {
                public readonly uint dwFileAttributes;
                public readonly long ftCreationTime;
                public readonly long ftLastAccessTime;
                public readonly long ftLastWriteTime;
                public readonly uint nFileSizeHigh;
                public readonly uint nFileSizeLow;
                public readonly uint dwReserved0;
                public readonly uint dwReserved1;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public readonly string cFileName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
                public readonly string cAlternateFileName;
            }

            [Flags]
            internal enum SLR_FLAGS
            {
                /// <summary>
                ///     Do not display a dialog box if the link cannot be resolved. When SLR_NO_UI is set,
                ///     the high-order word of fFlags can be set to a time-out value that specifies the
                ///     maximum amount of time to be spent resolving the link. The function returns if the
                ///     link cannot be resolved within the time-out duration. If the high-order word is set
                ///     to zero, the time-out duration will be set to the default value of 3,000 milliseconds
                ///     (3 seconds). To specify a value, set the high word of fFlags to the desired time-out
                ///     duration, in milliseconds.
                /// </summary>
                SLR_NO_UI = 0x1,

                /// <summary>Obsolete and no longer used</summary>
                SLR_ANY_MATCH = 0x2,

                /// <summary>
                ///     If the link object has changed, update its path and list of identifiers.
                ///     If SLR_UPDATE is set, you do not need to call IPersistFile::IsDirty to determine
                ///     whether or not the link object has changed.
                /// </summary>
                SLR_UPDATE = 0x4,

                /// <summary>Do not update the link information</summary>
                SLR_NOUPDATE = 0x8,

                /// <summary>Do not execute the search heuristics</summary>
                SLR_NOSEARCH = 0x10,

                /// <summary>Do not use distributed link tracking</summary>
                SLR_NOTRACK = 0x20,

                /// <summary>
                ///     Disable distributed link tracking. By default, distributed link tracking tracks
                ///     removable media across multiple devices based on the volume name. It also uses the
                ///     Universal Naming Convention (UNC) path to track remote file systems whose drive letter
                ///     has changed. Setting SLR_NOLINKINFO disables both types of tracking.
                /// </summary>
                SLR_NOLINKINFO = 0x40,

                /// <summary>Call the Microsoft Windows Installer</summary>
                SLR_INVOKE_MSI = 0x80
            }

            /// <summary>The IShellLink interface allows Shell links to be created, modified, and resolved</summary>
            [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
             Guid("000214F9-0000-0000-C000-000000000046")]
            internal interface IShellLinkW
            {
                /// <summary>Retrieves the path and file name of a Shell link object</summary>
                void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath,
                    ref WIN32_FIND_DATAW pfd, SLGP_FLAGS fFlags);

                /// <summary>Retrieves the list of item identifiers for a Shell link object</summary>
                void GetIDList(out IntPtr ppidl);

                /// <summary>Sets the pointer to an item identifier list (PIDL) for a Shell link object.</summary>
                void SetIDList(IntPtr pidl);

                /// <summary>Retrieves the description string for a Shell link object</summary>
                void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

                /// <summary>Sets the description for a Shell link object. The description can be any application-defined string</summary>
                void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

                /// <summary>Retrieves the name of the working directory for a Shell link object</summary>
                void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

                /// <summary>Sets the name of the working directory for a Shell link object</summary>
                void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

                /// <summary>Retrieves the command-line arguments associated with a Shell link object</summary>
                void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

                /// <summary>Sets the command-line arguments for a Shell link object</summary>
                void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

                /// <summary>Retrieves the hot key for a Shell link object</summary>
                void GetHotkey(out short pwHotkey);

                /// <summary>Sets a hot key for a Shell link object</summary>
                void SetHotkey(short wHotkey);

                /// <summary>Retrieves the show command for a Shell link object</summary>
                void GetShowCmd(out int piShowCmd);

                /// <summary>Sets the show command for a Shell link object. The show command sets the initial show state of the window.</summary>
                void SetShowCmd(int iShowCmd);

                /// <summary>Retrieves the location (path and index) of the icon for a Shell link object</summary>
                void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                    int cchIconPath, out int piIcon);

                /// <summary>Sets the location (path and index) of the icon for a Shell link object</summary>
                void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

                /// <summary>Sets the relative path to the Shell link object</summary>
                void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);

                /// <summary>Attempts to find the target of a Shell link, even if it has been moved or renamed</summary>
                void Resolve(IntPtr hwnd, SLR_FLAGS fFlags);

                /// <summary>Sets the path and file name of a Shell link object</summary>
                void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
            }

            [ComImport, Guid("0000010c-0000-0000-c000-000000000046"),
             InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IPersist
            {
                [PreserveSig]
                void GetClassID(out Guid pClassID);
            }

            [ComImport, Guid("0000010b-0000-0000-C000-000000000046"),
             InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IPersistFile : IPersist
            {
                new void GetClassID(out Guid pClassID);

                [PreserveSig]
                int IsDirty();

                [PreserveSig]
                void Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

                [PreserveSig]
                void Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                    [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);

                [PreserveSig]
                void SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

                [PreserveSig]
                void GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
            }

            // CLSID_ShellLink from ShlGuid.h 
            [
                ComImport,
                Guid("00021401-0000-0000-C000-000000000046")
            ]
            internal class ShellLink
            {
            }
        }
    }
}