# Changelog

All notable changes to the Any Uninstaller project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---
## [1.4.0] - 2026-09-05

### 🚀 System App Uninstallation & Real Deletion Engine
- **Automatic Ownership & Permission Granting (`WindowsTools.TakeOwnershipAndGrantPermissions`)**:
  - Automatically takes ownership from `NT SERVICE\TrustedInstaller` / `SYSTEM` for the Administrators group (`takeown /a /r /d y /skipsl`) and grants recursive Full Control ACLs with inheritance (`icacls ... /grant:r *S-1-5-32-544:(OI)(CI)F /t /c /q`).
  - Strips read-only, hidden, and system file attributes (`attrib -r -s -h`) and ensures traverse rights on `C:\Program Files\WindowsApps`.
  - Enables true physical deletion of protected Store App package folders and system remnants without permission errors.
- **Elimination of Fake Deletions & Accurate Residual Accounting (`FileSystemJunk` & `JunkManager`)**:
  - `FileSystemJunk.Delete()` now strictly raises exceptions on failure instead of silently swallowing them or falsely reporting success when files or folders remain on disk.
  - Fixed false-success reporting in `JunkManager.ExecuteElevatedBatchCleanup`: items that still exist on disk after cleanup are recorded in `result.FailedItems` with detailed failure reasons ("Access denied or folder is locked by system") and never added to `result.SuccessfullyDeleted`.
  - Relocates locked in-use DLLs (such as Explorer shell extensions) to `%TEMP%\AnyUninstaller_PendingDelete` so parent application directories can be deleted cleanly.
  - Explicitly excluded `C:\Windows\SystemApps` subdirectories from junk scanning to prevent accidental damage to Windows Search or core shell components.
- **Windows Store & Inbox App All-Users De-Provisioning (`StoreAppHelper` & `BulkUninstallEntry`)**:
  - Upgraded uninstallation engine with multi-stage uninstallation using `RemovalOptions.RemoveForAllUsers` and WinRT de-provisioning (`DeprovisionPackageForAllUsersAsync`), eliminating error `0x80070032` (*"cannot be uninstalled on a per-user basis"*) and `0x80073CFA`.
  - Added resilient PowerShell/DISM fallback for stubborn system and inbox packages (removing all-users package, per-user package, and provisioned package definitions).
  - Enhanced package discovery in `QueryApps()` to scan across all users with deduplication.
  - Validates Store App exit code 0 against actual package existence across all users; triggers elevated fallback if still provisioned or present in another user profile.
- **Microsoft OneDrive & Cortana Uninstallation Overhaul (`PredefinedFactory`)**:
  - **OneDrive**: Unregisters shell extension `FileSyncShell64.dll`, safely relocates locked DLLs to `%TEMP%`, takes ownership of leftover directories, and deletes files cleanly.
  - **Cortana**: Removed phantom entry linked to immutable `SystemApps` folder (`InstallLocation = null`); targets modern Cortana AppX package (`Microsoft.549981C3F1010`), de-provisions it for all users, and respects policy disable state (`AllowCortana == 0`).
- **Refined Protection Check (`IsProtected`)**:
  - Replaced overly broad signature check (`SignatureKind == PackageSignatureKind.System`) with strict protection of essential core OS infrastructure (Settings, ShellExperienceHost, StartMenuExperienceHost, Taskbar Search, LockApp, SecHealthUI).
  - Unlocked Cortana, Microsoft Photos, Camera, Xbox, Phone Link, Weather, and all user-facing Store apps so users can run standard, quiet, or bulk uninstalls without being blocked by false protection flags.
  - Enhanced `UninstallerExecutionService` to respect user-initiated uninstalls and queue selected apps as `Waiting` instead of skipping them as `Protected`.

### 🔒 In-Use Folder Process Detection, Automatic Unlock & Process Restart
- **In-Use Folder & File Lock Detection (`ProcessLockHelper`)**:
  - Implemented dual-engine process locking detection using the native Windows **Restart Manager API** (`rstrtmgr.dll`) and running process module analysis (`Process.GetProcesses()`).
  - Identifies which applications or background services are holding open file handles or running executables/DLLs inside folders scheduled for deletion.
  - Strict system process protection ensures critical OS services (System, csrss, lsass, svchost, etc.) and Any Uninstaller itself are never terminated.
  - Provides robust process termination supporting process trees (`proc.Kill(entireProcessTree: true)`) and elevated `taskkill /F /T /PID` fallback if administrative privileges are required.
- **Automatic Process Restart & Windows Explorer Revival (`RestartProcesses` & `RestartExplorer`)**:
  - Automatically restarts terminated applications once file deletion completes, preventing killed background apps, file managers, or shell tools from staying closed.
  - Specially safeguards Windows Explorer (`explorer.exe`): monitors native shell desktop (`GetShellWindow()`), taskbar tray window (`FindWindow("Shell_TrayWnd")`), and active responding explorer processes in the current interactive user session.
  - Multi-tier revival engine (`RestartExplorer`): executes 4 distinct launch tiers (ShellExecute with Windows working directory, direct Win32 process creation, `cmd.exe /c start`, and PowerShell `Start-Process` decoupled from elevated parent context) with hung/zombie process clearing (`force: true`), ensuring the shell taskbar and desktop always revive.
  - Automatic restart on deletion: `DeleteJunkBatchAsync` and `DeleteSingleJunkAsync` automatically verify shell health and revive Explorer even if no explicit process restart was queued.
  - Window close protection: closing `JunkRemoveWindow` or `TempCleanerWindow` automatically checks and revives Explorer if down.
- **Interactive Process Lock Dialog (`ProcessLockDialog`)**:
  - Displays detected locking applications with application name, process name, PID, locked resource, executable location, and a new **"Action on Delete"** column showing restart status (e.g., *"Will restart Explorer"*, *"Will restart"*, or *"Close only (Uninstall target)"*).
  - Offers **"Close, Delete & Restart"** (with clean SVG badge, no emojis) to cleanly close locking processes, delete the locked items, and automatically restart them.
  - Offers **"Skip Locked Items"** to deselect in-use folders and delete only unlocked items.
- **Comprehensive Integration Across Manual Uninstall & Residual Cleaning**:
  - **Pre-Delete Check**: Automatically checks selected directories/files for locks before starting deletion in `JunkRemovalViewModel`.
  - **Post-Delete Fallback & Retry**: Detects items that failed with in-use/locked errors during cleanup and prompts to terminate locking processes, delete, and restart.
  - **On-Demand Unlock Context Menu**: Right-click context menu option in `JunkRemoveWindow` DataGrid ("Check for Locking Applications / Unlock...") closes locks, deletes the item, and automatically restarts the applications.
  - **Manual Uninstall Pre-Scan Check**: When clicking "Uninstall manually" on a running application, prompts before scanning and automatically restarts Explorer if involved.

### 🎨 Main Window Empty State Display, Vector Icons & Theme Polish
- **Interactive Empty State Display (`MainWindow.axaml` & `MainWindowViewModel`)**:
  - When searching or filtering yields 0 results (`HasNoFilteredResults`), the main table area displays a modern empty state card rather than a blank dark void.
  - Features an emblem badge with an SVG vector search icon, dynamic context headlines ("No matching applications found", "No applications match active filters", "No applications found"), and descriptive explanations quoting the user's active query.
  - Provides instant recovery action buttons: **"Clear Search"** (visible when search text exists), **"Reset Filters"**, and **"Refresh Applications"**.
  - Automatically collapses the TreeMap and splitter when 0 items match (`IsTreeMapVisibleAndHasItems`), dedicating 100% of the viewport to the empty state card and preventing an empty dark void.
  - Real-time status bar updates dynamically display the active search status (e.g. `No applications found matching "film"` or `Showing X of Y applications`).
- **Clean Vector Iconography & Zero Emojis**:
  - Replaced all remaining emojis in the top toolbar ("Target", "Clean Junk", "Settings"), sidebar filters, preset badges, and DataGrid columns (Quiet uninstall vector bolt) with modern SVG `PathIcon` elements and clean typography.
- **Light / White Mode Success Theme & High-Contrast Green Color Palette**:
  - Resolved dark-mode visual artifact in Light / White theme where completion screens (`JunkRemoveWindow` and `TempCleanerWindow`) rendered near-black glowing emblems (`#162b1a` to `#0d1b10`), dark rings, and black chip backgrounds on a white window.
  - Introduced dynamic theme brush system (`SuccessEmblemBgBrush`, `SuccessEmblemBorderBrush`, `SuccessEmblemIconBrush`, `SuccessPulseRing1Brush`, `SuccessPulseRing2Brush`, `SuccessBadgeBgBrush`, `SuccessBadgeBorderBrush`, `SuccessBadgeTextBrush`, `SuccessBadgeNumBrush`, `SuccessBtnBgBrush`, `SuccessBtnBorderBrush`, `DangerBadge...`).
  - In Light Mode, completion emblems dynamically transition to soft, refreshing emerald/mint gradients (`#e8f9ed` -> `#d4f5dc` -> `#e6f9ed`) with emerald borders (`#2da44e`) and rich high-contrast forest green checkmarks and text (`#1a7f37`), while preserving deep glowing green tones in Dark, Midnight, and OLED themes.
  - Refactored `TargetWindow`, `TempCleanerWindow`, `UninstallProgressWindow`, and `MainWindow` status bar to use responsive dynamic brushes instead of hardcoded dark greens.
- **Streamlined Post-Cleanup Completion Screen**:
  - Removed the redundant manual "Restart Explorer" button from `JunkRemoveWindow` and `TempCleanerWindow` completion screens to declutter the UI, relying on automatic shell monitoring and background revival on window close.

---
## [1.3.7] - 2026-09-04

### Invalid & Broken Uninstaller Improvements & Manual Uninstall Overhaul
- **Smart Uninstaller Routing (`ApplicationEntryViewModel`)**:
  - `HasRealUninstaller` now strictly requires `IsValid == true` in addition to non-orphaned status and recognized uninstaller types. Invalid and broken uninstaller entries (missing, corrupted, or deleted uninstaller executables) are properly recognized as having no real uninstaller executable.
  - Standard and Quiet uninstall actions automatically redirect invalid/broken applications directly to the Manual Uninstall flow instead of failing to launch non-existent executables.
  - In context menus, standard "Uninstall" and "Uninstall quietly" options are hidden for broken/invalid entries, presenting "Uninstall manually" as the primary action.
- **Guaranteed Residual Leftovers Detection (`JunkCleaningService`)**:
  - `ScanJunkAsync` now proactively captures the uninstaller's registry key (`target.RegistryPath`) as a high-confidence `RegistryKeyJunk` (`ConfidenceRecords.IsUninstallerRegistryKey` & `ExplicitConnection`), ensuring broken or phantom registry entries are always cleanly presented for deletion.
  - Candidate directories (`InstallLocation`, `UninstallerLocation`, and executable directories) are automatically validated with `IsSafeApplicationDirectory` to safely catch residual app folders without touching Windows or system directories.
  - Startup entries (`target.StartupEntries`) are preserved as selectable `StartupJunkNode` items.
  - Stale uninstaller entries with 0 residual items automatically refresh the application catalog to prevent phantom entries.

### Fix False Positive Residual Junk & Self-Directory Protection
- **Prevention of Internal Helper Directory Leakage (`ApplicationUninstallerEntry`)**:
  - Fixed a critical false positive where Any Uninstaller's own application folder (e.g. extracted on Desktop) was erroneously detected as residual junk belonging to Skype with "Good" confidence.
  - **Root Cause**: When scanning Windows Store apps (UWP/MSIX) like Skype, Any Uninstaller configures `UninstallString` to invoke its internal `StoreAppHelper.exe`. Assigning this string inadvertently populated `UninstallerFullFilename` and `UninstallerLocation` with `StoreAppHelper.exe` and its containing directory (Any Uninstaller's own folder).
  - Added `IsSelfOrHelper` and `IsSelfOrHelperDirectory` guards across `ApplicationUninstallerEntry` setters (`UninstallString`, `UninstallerFullFilename`, `UninstallerLocation`): prevents internal helper tools (`StoreAppHelper.exe`, `SteamHelper.exe`, `UninstallerAutomatizer.exe`, etc.) and Any Uninstaller directories (`AppLocation`, `AssemblyLocation`, `AppContext.BaseDirectory`, `ProcessPath`) from ever being recorded as an external application's uninstaller path or location.
  - Updated `StoreAppFactory` and `GenerateSteamHelperStrings` to explicitly ensure `UninstallerFullFilename` and `UninstallerLocation` remain `null`. Store apps are uninstalled via the Windows package manager, not directory-based uninstaller binaries.
- **Watertight Residual Scanner Protections (`JunkCleaningService` & `JunkManager`)**:
  - Excluded Windows Store apps from raw `candidateDirs` scanning in `JunkCleaningService`. Windows Store apps are managed by the Windows AppX service; their package folders inside `WindowsApps` are handled by `InstallLocationScanner` with `IsStoreApp` (-10) safety protection.
  - Strengthened `IsSafeApplicationDirectory` to reject any path matching or inside Any Uninstaller's application directory or helper folders, and protected user profile, desktop, documents, and downloads root directories.
  - Routed all merged residual junk results through `JunkManager.CleanUpResults`, guaranteeing deduplication, prohibited folder exclusion, and `JunkDoesNotPointToSelf` checks are universally enforced before results are presented in the UI.

---
## [1.3.6] - 2026-09-04

### 🛡️ Safe Orphaned & Uninstaller-Unavailable Item Handling
- **Enforce Manual Uninstall for Orphaned Items**:
  - Orphaned items (`IsOrphaned`) and applications lacking a real uninstaller (`SimpleDelete`, `Unknown`, or missing uninstaller command) now strictly offer only **Manual Uninstall**.
  - Standard "Uninstall" and "Quiet Uninstall" are automatically hidden in DataGrid and TreeMap context menus when an item has no real uninstaller executable.
  - Initiating uninstallation via the toolbar or keyboard shortcuts (`Ctrl+U`, `Ctrl+Q`) on orphaned or no-uninstaller items automatically routes directly to the safe **Manual Uninstall** flow (`JunkRemoveWindow`).
- **Full Item Listing with Zero Auto-Deletion**:
  - Displays all scanned files, folders, shortcuts, and registry keys in the manual review checklist with checkboxes.
  - Never automatically deletes files or directories without explicit user inspection and confirmation.
  - Ensured existing application install locations are always captured in the residual scan.
- **UniversalUninstaller Bug Fixes**:
  - Fixed an `InvalidCastException` in `UniversalUninstaller/TargetList.cs` where passing `rootDirectory` (`DirectoryInfo`) instead of `root` (`TreeEntry`) caused the file list to appear completely blank.
  - Disabled silent directory deletion in `UniversalUninstaller` quiet mode (`/Q`), ensuring the review selection window is always displayed and files are not auto-deleted.

---
## [1.3.5] - 2026-09-02

### 🛠️ Manual Uninstallation & Advanced Leftover Cleaning
- **Comprehensive Manual Uninstall Flow (`Uninstall manually`)**:
  - Implemented `RunManualUninstallFlowAsync` to provide complete manual uninstallation and leftover cleanup for any selected application(s).
  - Bypasses broken or missing uninstaller executables and scans for all associated files, folders, and registry entries via `JunkCleaningService`.
  - Displays real-time scan progress in the bottom status bar.
  - Automatically opens the `JunkRemoveWindow` (`JunkRemovalViewModel`) dialog allowing users to inspect, select, and delete residual artifacts.
  - Refreshes the application list automatically once junk removal is finalized.
- **Universal Manual Removal Capability**:
  - Updated `CanManualUninstall` in `ApplicationEntryViewModel` so manual removal is enabled for all application entries, specifically targeting orphaned, corrupted, Store, or registry-only entries.
  - Added **"Uninstall manually"** with keyboard shortcut `Ctrl+M` to the top `_Uninstall` menu bar in addition to the item context menu.
- **Robust Uninstaller & Executable Launcher**:
  - Replaced legacy command-line invocation in `OnRunUninstallerClick` and `OnRunQuietUninstallerClick` with `ProcessTools.SeparateArgsFromCommand` (`KlocTools`) for reliable path resolution and argument separation.

---
## [1.3.4] - 2026-09-01

### 🧹 Dedicated 'Delete Temporary Files' Cleaner Tool
- **New Tool Dialog (`Tools -> Delete temporary files...`)**:
  - Added a dedicated, modern temporary file cleaner dialog to analyze and reclaim gigabytes of junk storage.
  - Scans and cleans 5 major system cache locations:
    - 👤 **User Temp Files**: `%TEMP%`, `%LOCALAPPDATA%\Temp`
    - 🪟 **Windows System Temp**: `%WINDIR%\Temp`
    - 💥 **Crash Dumps & Diagnostics**: `%LOCALAPPDATA%\CrashDumps`, Windows Error Reporting (`WER`) queues and archives
    - 📦 **Windows Update Cache**: `%WINDIR%\SoftwareDistribution\Download`
    - 🌐 **Web & App Caches**: `%LOCALAPPDATA%\Microsoft\Windows\INetCache`
  - **Category Metric Summary Cards**: Real-time disk space usage cards for each category with explicit `0 KB` formatting when empty or cleared.
  - **Interactive DataGrid**: Displays item checkboxes, category badges, type icons, file sizes, last modified dates, and live cleaning status.
  - **Search & Category Filtering**: Instant text search filter capsule and category dropdown filter.
  - **Resilient Background Deletion**: Safely cleans selected files and directories while skipping locked or in-use files with detailed status reporting.
  - **Explorer Integration**: Context menu option to *"Open containing folder in Explorer"* for any selected item.

### 💬 UI Usability & Hover Tooltips
- **Column Hover Tooltips & Text Trimming**:
  - Implemented `ToolTip.Tip` and `TextTrimming="CharacterEllipsis"` across all table columns in the Main Application List, Delete Temporary Files Dialog, and Leftover Junk Cleaner.
  - Hovering over long or clipped file paths, application names, publishers, versions, categories, sizes, timestamps, and confidence ratings displays the full text in a floating tooltip.
- **Explicit '0 KB' Size Formatting**:
  - Fixed size formatting so that cleared categories, empty folders, and zero-byte items explicitly display `0 KB` rather than appearing completely blank.
  - Added `ShowZero` / `ZeroAs0Kb` parameter support to `FileSizeConverter`.

### 🔒 Dynamic Action Button State Management
- **Selection-Aware Action Buttons**:
  - Automatically disable **Clean Junk**, **Uninstall**, and **Quiet Uninstall** toolbar buttons and menu items when no applications are selected or checked.
- **Loading State Protections**:
  - Automatically disable **Target**, **Refresh**, and Selection actions (**Toggle Select All**, **Select All**, **Deselect All**, **Invert Selection**) while scans are in progress to prevent concurrent scan collisions.
- **Visual Disabled States**:
  - Added `:disabled` opacity styling (`Opacity="0.38"`) for clear visual feedback across all button types.

---
## [1.3.3] - 2026-09-01

### ⚡ Startup & Post-Opening Loading Performance
- **Persistent App Info Cache Enabled (`InfoCache.xml`)**:
  - Activated persistent application metadata caching (`UninstallToolsGlobalConfig.EnableAppInfoCache = true`) with automatic fallback to writable `%AppData%\Any Uninstaller` for write-protected environments.
  - Subsequent application openings load and populate the full application list near-instantly (< 300 ms).
- **Eliminated Synchronous WMI Disk Queries**:
  - Replaced heavy `ManagementObjectSearcher` queries (`Win32_DiskDriveToDiskPartition` and `Win32_LogicalDiskToPartition`) in `FactoryThreadedHelpers.SplitByPhysicalDrives` with instant drive root partitioning, saving ~1–2 seconds per scan.
- **Concurrent Independent Package Scanners**:
  - Parallelized `GetMiscUninstallerEntries` to scan Microsoft Store apps, Steam, Oculus, Windows Features, Windows Updates, Scoop, and Chocolatey concurrently via multi-threaded worker pools.
- **Dynamic Multi-Core Worker Scaling**:
  - Scaled `MaxThreadsPerDrive` dynamically based on available logical CPU cores (`Math.Clamp(Environment.ProcessorCount, 4, 16)`) instead of being hardcoded to 2.
- **Asynchronous / Non-Blocking UI Icon Extraction**:
  - Replaced synchronous PE icon decoding on the UI thread with background extraction in `IconExtractionService` and instant placeholder display, ensuring 60+ FPS smooth scrolling.
- **Optimized Startup Item Association**:
  - Pre-extracted valid install and uninstaller directory paths in `StartupManager.AssignStartupEntries` to eliminate quadratic $O(N^2 \cdot M)$ string searching across all entries.
- **Fast Developer Launch Script (`run_avalonia.bat`)**:
  - Added `--no-restore` flag and fixed if-condition nesting in `run_avalonia.bat` to eliminate redundant NuGet package resolution overhead and ensure smooth launches.

---
## [1.3.2] - 2026-09-01

### ⚡ Performance & UI Responsiveness ("Not Responding" Fixes)
- **Throttled Progress Reporting in Background Scanners**:
  - Replaced high-frequency progress callbacks in `ScannerService` and `JunkCleaningService` with a 35ms throttled reporter (~30 FPS dispatch cap).
  - Prevents thousands of progress updates per second from flooding the Avalonia UI message queue during MSI product enumeration, drive file scans, and registry sweeps, eliminating application freezes and Windows "Not Responding" states.
- **Non-Blocking Digital Signature & Certificate Verification**:
  - Replaced synchronous `X509Certificate2.Verify()` calls and online OCSP/CRL network queries during count aggregation with non-blocking cached checks (`Entry.IsCertificateValid(true)`).
- **Cached File System Inspections**:
  - Cached `HasInstallLocation`, `HasUninstallerLocation`, and `CanRunExecutable` property evaluations in `ApplicationEntryViewModel`, preventing repeated synchronous disk I/O (`Directory.Exists`, `File.Exists`, `Directory.GetFiles`) during UI rendering and badge counting.
- **Search Debouncing & Fast Sidebar Counts**:
  - Added a 150ms debounce mechanism to `SearchText` input in `FilterSidebarViewModel` to avoid redundant filtering passes on rapid typing.
  - Converted sidebar category count calculations from 20 separate LINQ passes into a single $O(N)$ pass.
- **Background ViewModel Instantiation & Optimized Filtering**:
  - Offloaded entry wrapper generation to background tasks in `MainWindowViewModel` and streamlined filter queries to minimize memory allocations.

### 📊 DataGrid Column Ascending / Descending Sorting
- **Explicit `SortMemberPath` Mapping**:
  - Configured proper `SortMemberPath` properties across all DataGrid columns in `MainWindow.axaml` (`DisplayName`, `Publisher`, `DisplayVersion`, `EstimatedSizeKb`, `StatusDescription`, `InstallDate`, `UninstallerKind`, `QuietUninstallPossible`, `InstallLocation`).
- **Numerical Size & Chronological Date Sorting**:
  - Bound Size column sorting to `EstimatedSizeKb` (`long`) ensuring accurate numerical comparisons (1 GB > 500 MB > 10 MB) rather than alphabetical text sorting.
  - Bound Install Date column sorting to `InstallDate` (`DateTime?`) for accurate chronological ordering.
- **Dynamic Sort Preservation**:
  - Active column sort direction is now automatically preserved across search queries and sidebar filter toggles.

### 🛠️ Developer Tooling & Build Scripts
- **Bypass Apphost UAC Execution Errors (`run_avalonia.bat`)**:
  - Updated `run_avalonia.bat` to launch `dotnet "%~dp0bin\AnyUninstaller.Avalonia.dll"` directly, bypassing Windows UAC and SmartScreen "Access is denied" execution errors on generated stub executables.
  - Added automated cleanup of lingering `.NET` host processes before builds to prevent locked bin file errors.

---
## [1.3.1] - 2026-08-30

### ⚡ On-Demand Privilege Elevation & Manifest Streamlining
- **Standard User Startup (`asInvoker`)**:
  - Removed startup forced elevation (`EnsureAdministrator`), allowing Any Uninstaller to launch instantly as a standard user process without an initial UAC prompt.
  - Removed the `allowElevation` restricted capability from `AppxManifest.xml`, deploying with standard `runFullTrust` for full Store compatibility and frictionless install.
  - Configured on-demand UAC elevation so administrative rights are requested only when needed (e.g. running uninstaller binaries, executing elevated registry deletions, or cleaning protected system paths).
- **Single-Prompt Batch Leftover Cleanup (`JunkManager.DeleteJunkBatch`)**:
  - Eliminated per-file/per-directory UAC prompt spam during junk removal by replacing individual Windows Shell `SHFileOperation` calls with silent .NET deletion.
  - User-writable files, folders, and registry keys (`AppData`, `HKCU`, user profile) are now deleted directly and silently with zero UAC prompts.
  - All protected system files, directories (`Program Files`, `ProgramData`), and `HKLM` registry entries that require administrator privileges are grouped together and executed in a **single elevated batch pass**, prompting the user with at most **one** UAC confirmation for the entire batch.

---
## [1.3.0] - 2026-08-27

### 🏪 Windows Store Apps & Helper Toolchain
- **Helper Toolchain Compilation & Resolution**:
  - Resolved build failure in `StoreAppHelper`, `SteamHelper`, and `OculusHelper` caused by an internal namespace collision (`ScriptingFileSystemHelper` declaring `namespace HelperTools` shadowing `Klocman.HelperTools`).
  - Linked helper projects directly to `AnyUninstaller.Avalonia.csproj` ensuring helper executables (`StoreAppHelper.exe`, `SteamHelper.exe`, `OculusHelper.exe`) are automatically compiled and bundled into the output directory.
  - Implemented dynamic fallback path resolution for `StoreAppFactory.HelperPath` spanning `AssemblyLocation`, `AppContext.BaseDirectory`, and `AppDomain.CurrentDomain.BaseDirectory`.
  - Added synchronous `WaitForExit()` in `FactoryTools.StartHelperAndReadOutput` to ensure process output streams are completely flushed before evaluating exit codes.
  - Resolved the issue where Windows Store Apps displayed a count of `0`.

### 🎯 Health & Category Filter Synchronization
- **Accurate Category Filtering**:
  - Fixed filter conflict where selecting **Protected Items (79)** displayed only 2 packages because 77 of them were marked with `SystemComponent = 1` and got filtered out by the default unchecked `System Components` rule.
  - Refactored `MainWindowViewModel.ApplyFiltering()` to use unified, category-aligned health state matching (`Protected`, `Orphaned`, `Invalid / Broken`, `Verified Normal`, `System Components`, and `Updates`) preventing unintended mutual exclusions.
  - Synchronized count badges in `FilterSidebarViewModel` so every badge number accurately matches the exact count of items displayed in the list when that option is selected.
  - Enhanced `PresetIssuesOnly` preset to include Store app issues alongside standard Win32 issues.

### 🖼️ Automatic Default Fallback Icons
- **Default Application & Warning Icons**:
  - Implemented fallback icon resolution in `IconExtractionService` for applications lacking an embedded or registered icon.
  - Valid applications without an icon now display the standard Windows application window icon (`SystemIcons.Application`).
  - Invalid / broken entries (missing binaries or broken registry uninstaller paths) display the standard warning/exclamation icon (`SystemIcons.Exclamation`).
  - Updated `ApplicationEntryViewModel.Icon` to guarantee non-null return values, eliminating blank gaps and layout misalignments in the data grid.

### 🎨 Settings Window Symmetry & Layout Polish
- **Balanced Card & Switch Layouts**:
  - Standardized all settings option cards in `SettingsWindow.axaml` to a uniform **66px height** with `Padding="18,0"`, ensuring vertical centering for titles, descriptions, and switches.
  - Overrode FluentTheme `ToggleSwitchPreContentMargin` and `ToggleSwitchPostContentMargin` to `0` and locked `Width="40"`, guaranteeing the toggle switch sits exactly 18px from the card's right edge, matching the 18px left text padding.
  - Normalized `ScrollViewer` padding (`0`) and `StackPanel Spacing="8"` across all settings pages, eliminating horizontal right-side offsets and dangling bottom margins.
  - Normalized dialog dimensions (`860 × 590`) and balanced outer container margins (`20,18,20,18`) across all categories.
- **Streamlined Default Column Visibility**:
  - Set **"Uninstaller Type / Kind"** and **"Quiet Uninstall Support ( ⚡ )"** to **disabled by default**, providing a cleaner, more spacious default table layout. Users can re-enable them at any time in **Settings > Column Visibility** or via **"Show All"**.

### 🛡️ Privacy Policy & Store Compliance
- **Comprehensive Privacy Policy (`PRIVACY.md`)**:
  - Published an official, GDPR/CCPA and Microsoft Store-compliant Privacy Policy documenting offline-by-design architecture, zero telemetry/analytics collection, local JSON configuration storage, and explicit user-directed system permissions.
  - Added direct in-app access to the Privacy Policy within **Settings > About & Info** via the new **"🛡️ Privacy Policy"** button, fulfilling Microsoft Store Certification Policy 10.5.1.

### 🎯 Target Application Detection & Window Targeter
- **Full Window Tracking & Crosshair Interaction**:
  - Upgraded `TargetWindow` to support both **drag-and-drop** (click, hold, drag over window, and release) and **click-to-target** (click crosshair, move cursor, and click target window) interaction patterns.
  - Automatically minimizes the target dialog during active window tracking so desktop windows and background applications are fully visible and clickable without obstruction.
  - Added ESC key cancellation to cleanly abort targeting mode and restore the window.
  - Implemented a 4-tier process executable resolution pipeline (`QueryFullProcessImageName` with limited rights, standard query rights, `Process.MainModule`, and WMI `Win32_Process` fallback) to reliably retrieve the executable path across 32-bit, 64-bit, elevated, and modern UWP/Store processes.
- **Robust Uninstaller Matching & On-the-Fly Orphan Generation**:
  - Upgraded `SelectApplicationsFromPaths` to search across all scanned applications (`ViewModel.AllEntries`) instead of only the currently filtered view.
  - Added automatic on-the-fly uninstaller generation using `DirectoryFactory.TryCreateFromDirectory`: if a targeted folder or standalone application has no registered Windows installer entry, an uninstaller entry is dynamically constructed, added to the uninstaller list, and highlighted.
  - Added **Windows Explorer Open Folder Detection**: dragging the target crosshair onto an Explorer folder window dynamically resolves the specific directory path being browsed via Shell automation.
  - Added **Generic System Directory & OS Component Protection**: strictly excluded root/system directories (`C:\Windows`, `C:\Program Files`, `C:\ProgramData`, root drives) and core Windows processes (`explorer.exe`, `dwm.exe`, `taskhostw.exe`, etc.) from path-matching heuristics, eliminating false-positive matches against third-party applications (e.g. SwarPlug) that registered their install location as `C:\WINDOWS`.
- **Landscape Layout & Global Reticle Cursor Sync (`TargetWindow.axaml`)**:
  - Transformed the dialog into an intuitive, balanced **landscape orientation (`660 × 360px`)**:
    - **Left Column (Drag Targeter)**: Houses the interactive crosshair reticle (`⌖`), real-time target readout box (dynamically displaying window title, process name, and PID), and live status badge (`● Aiming...` / `● Ready`).
    - **Right Column (Browse Filesystem)**: Houses balanced action buttons for **"📁 Install Directory..."** and **"⚡ Application File or Shortcut..."**, along with directory scanning tips.
  - Implemented Win32 `SetCapture` and global `SetCursor(IDC_CROSS)` synchronization: when dragging the targeting reticle across any window or monitor on Windows, the mouse cursor remains the targeting crosshair icon everywhere.
  - Replaced minimization with a non-intrusive floating mode (`Topmost = true`, `Opacity = 0.90`), eliminating window vanishing or failure-to-restore bugs when targeting applications.
  - Positioned live status feedback and Cancel button in a dedicated footer row with clean spacing.

---
## [1.2.0] - 2026-08-27
### 📦 Unified Packaging & Build Pipeline
- **Master Build Automation**: Created `build_packages.bat` (and `build.bat`) accompanied by `scripts/build_packages.ps1` to compile and generate all release formats with a single command.
- **Four Release Targets Generated (`dist/`)**:
  - **App**: Full self-contained application directory containing all runtime libraries and binaries.
  - **Standalone EXE**: Single-file executable distribution (`AnyUninstaller.exe`) with native Skia and HarfBuzz rendering engines.
  - **Portable Package**: Compressed `.zip` distribution featuring local configuration isolation (`portable.dat` and `AnyUninstaller_Settings.json`), ensuring zero footprint in `%AppData%`.
  - **Microsoft Store MSIX**: Desktop Bridge package (`Saayan.AnyUninstaller_1.2.0.0_x64.msix`) packaged with `MakeAppx.exe` and aligned to the Microsoft Store Partner Center package identity.
### 🛡️ Process Execution & Privilege Architecture
- **Desktop Bridge Compatibility Fix**: Reverted embedded PE manifest (`app.manifest`) from `requireAdministrator` to `asInvoker`, resolving the fatal Windows `ERROR_NOT_SUPPORTED` (`0x80070032 - "The request is not supported"`) when launched inside MSIX/Desktop Bridge containers.
- **Elevated Execution via UAC**: Dynamic privilege elevation is managed programmatically via `EnsureAdministrator` on startup using `Verb = "runas"`.
- **MSIX Capabilities**: Added `allowElevation` and `runFullTrust` restricted capabilities to `AppxManifest.xml` for full administrative uninstallation support across Windows 10 & 11.
- **Self-Contained Runtime Packaging**: Published all distribution packages with `--self-contained true`, bundling the required .NET 8 runtime libraries (`coreclr.dll`, `hostfxr.dll`, and BCL assemblies) so the app runs immediately on clean machines without requiring manual .NET runtime installation.
### 🏪 Microsoft Store Integration & Visual Assets
- **Store Identity Setup**: Embedded package identity:
  - **Package Name**: `Saayan.AnyUninstaller`
  - **Publisher**: `CN=37E2AF47-D2FC-489C-BDC1-02C989A7B989`
  - **Publisher Display Name**: `Saayan`
  - **Package Family Name (PFN)**: `Saayan.AnyUninstaller_f0v6x7d2rzc78`
- **Dynamic Asset Generator**: Automated generation of high-DPI Windows Store logos and live tile assets (`StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, `SplashScreen.png`) directly from the primary brand mark.
### 🧹 Branding & Forensic Sanitization
- **Repository Scrub**: Removed all residual traces, short forms, and naming artifacts of the legacy codebase across all source files, namespaces, projects, and over 100 localized `.resx` translation files.
- **Modern About Window**: Redesigned the About dialog with squircle branding, developer attribution (`Sayan Dey`), and clickable social profiles (LinkedIn & GitHub).



## [1.1.0] - 2026-08-26

### 🚀 Branding to Any Uninstaller
- **Complete branding**: Branded the application name to **Any Uninstaller** across window titles, menus, dialogs, about pages, and assembly metadata.
- **Updated Licensing & Attribution**: Updated copyright notices and Apache 2.0 license attributions for commercial & community readiness.

### 🧭 Navigation & Interactive Selection
- **TreeMap ↔ DataGrid Center Auto-Scroll**: Selecting an application block in the Treemap immediately selects the row and smoothly scrolls it directly into the center of the viewport.
- **Smart Dynamic Selection Toggle**: Merged "Select All" and "Deselect All" into a unified toggle button that updates dynamically based on current selection state.
- **Crosshair Window Targeter**: Added "Target an Application..." tool to drag-and-drop or click on any open desktop window to locate and highlight its corresponding installed entry.

### 📋 Contextual Actions & Smart Right-Click Menu
- **Adaptive Action Validation**:
  - Context menu dynamically enables or disables options based on entry capabilities (Quiet uninstall, MSIX / Store app support, manual entry deletion).
  - Invalid / broken entries are clearly flagged with manual removal actions and error diagnostics if deletion fails.
- **Streamlined Menu Structure**: Removed obsolete legacy actions (Rate, Rename) and organized quick access to registry keys, install directories, and web search.

### 🔍 Comprehensive Properties & Details Inspector
- **Crash-Proof Application Properties Dialog**: Redesigned the properties inspector with full support for all uninstaller types.
- **Deep Metadata Display**: Provides complete visibility into installation locations, registry keys, uninstall arguments, quiet flags, binary architecture, digital certificate status, and system component classification.

### 🎨 Visual Hierarchy & UI/UX Polish
- **Dynamic DataGrid Expansion**: When the TreeMap is toggled off, the application table dynamically auto-expands to fill 100% of the available vertical space without blank filler gaps.
- **Scrollbar & Layout Refinements**:
  - Eliminated scrollbar overlapping and text clipping issues across the table and sidebars.
  - Adjusted scrollbar thickness and thumb contrast for improved trackpad and mouse usability.
  - Centered text and icon alignments across all toolbar and sidebar buttons.
  - Locked filter sidebar to fixed non-resizable width for a stable, clean layout.
- **Refined Filter Sidebar**: Modernized filter styling with intuitive switches and instant reactive filtering.

### 🌗 Enhanced Theme & Personalization Engine
- **System Theme Integration**: Added "Use System Theme" support with automatic OS dark/light mode detection.
- **Quick Theme Switcher**: Placed theme controls at the top of the `View` menu with a dedicated submenu for instantaneous live switching (System Theme, Light Mode, Dark Mode, Midnight Blue, OLED Black).

### ⚙️ Settings & Help System Improvements
- **Customizable Column Visibility**: Added granular toggles to show or hide any column (Publisher, Version, Size, Status, Install Date, Type, Quiet, Location, Selection Checkboxes).
- **Reset to Defaults**: Added `Help -> Reset settings to default` to instantly restore standard preferences and column configurations.
- **Integrated Help & About**: Linked `Help -> About Any Uninstaller` directly to the Settings window's About & Info tab.

---

## [0.1.0] - 2026-08-25

### 🚀 Modern UI Framework & Architecture
- **Avalonia UI Implementation**: Migrated the core user interface to Avalonia UI with GPU-accelerated Skia rendering.
- **Dynamic File System RCW Engine**: Replaced COM-based `Scripting.FileSystemObject` with dynamic reflection-based RCW helpers (`ScriptingFileSystemHelper.cs`) to eliminate legacy COM marshalling issues.
- **Advanced Multi-Threaded Scanner**:
  - Enabled drive scanning (`ScanDrives = true`) and `AutoDetectCustomProgramFiles = true` to detect orphaned, broken, and unindexed software (such as Discord, portable apps, and custom installs).
  - Defaulted sidebar filters to display orphaned and invalid items automatically.

### 🎨 Visual Design & Theme System
- **Card-Based UI Architecture**: Upgraded window containers, sidebars, and dialogs to a 12px rounded card layout (`#0d1117` base, `#161b22` cards, `#30363d` borders).
- **Windows 11 Capsule Scrollbars**: Designed slim, rounded 10px overlay scrollbars with smooth pill thumbs (`CornerRadius="999"`).
- **GPU-Accelerated Storage TreeMap**:
  - Implemented Skia Direct3D11/OpenGL hardware-accelerated treemap visualization.
  - Added 4px rounded block rendering (`new RoundedRect(rect, 4, 4)`) and a curated modern color palette.
- **Dynamic Live Theme Engine (`AppSettingsService`)**:
  - Added live runtime theme switching with zero application restarts:
    - **Modern Dark (Default)**: Deep charcoal palette (`#0d1117` / `#161b22`) with GitHub-style blue accents (`#58a6ff`).
    - **Midnight Blue**: Cyber deep blue palette (`#070d19` / `#0f172a`) with electric cyan accents (`#38bdf8`).
    - **OLED Black**: Pitch black contrast palette (`#000000` / `#111111`) with glowing neon accents (`#00f2fe`).
    - **Light Mode**: Clean, high-contrast crisp white palette (`#f6f8fa` / `#ffffff`) with vibrant blue accents (`#0969da`).
  - Added dynamic theme toggles for rounded corners, UI micro-animations, and storage treemap visibility.

### 📊 DataGrid & List Improvements
- **Column Reordering**: Organized table into a logical flow:
  `[✓] | Name | Publisher | Version | Size | Status | Install Date | Type | Quiet | Location`
- **Vertical Grid Lines & Headers**: Added 1px vertical borders (`#30363d`) to column headers and data grid cells for distinct separation between Publisher, Version, and adjacent columns.
- **Eliminated Cell Selection Artifacts**: Disabled `DataGridCell` focus/currency visuals (`CurrencyVisual` / `FocusVisual`) and configured `IsReadOnly="True"` to remove disruptive white cell selection boxes.
- **Scrolling & Viewport Boundary Lock**:
  - Calibrated proportional column widths to fit all 10 columns cleanly on screen without hidden content.
  - Auto-stretched the final `Location` column (`Width="*"`) to eliminate Avalonia's blank filler header and prevent horizontal viewport jitter.
  - Enabled smooth vertical scrolling across hundreds of installed applications.

### 🛠️ Version & Install Date Extraction Engine
- **Compound Date-Version Splitter**:
  - Added smart parsing in `VersionCleaner.cs` and `ApplicationEntryViewModel.cs` to detect compound driver strings (e.g. `"01/05/2024 1.19.41.156"` in DIFxApp Canon packages).
  - Automatically splits the compound string into clean **Version** (`1.19.41.156`) and populates the missing **Install Date** (`01/05/2024`).
- **Comprehensive Registry Date Parser**:
  - Enhanced `RegistryFactory.GetInstallDate()` to parse standard formatted date strings (`MM/dd/yyyy`, `yyyy-MM-dd`, `dd/MM/yyyy`, `yyyyMMdd`, `yyyy/MM/dd`) using invariant and local cultures.

### ⚙️ Professional Settings & Preferences System
- **Master-Detail Settings Window (`SettingsWindow.axaml`)**:
  - Created a master-detail settings dialog with category navigation (General, Scanner, Automation, Appearance, About).
  - Designed Windows 11 style setting cards with titles, descriptions, and interactive `ToggleSwitch` controls.
  - Added live preview for themes and personalization options.
- **Toolbar & Menu Integration**:
  - Placed the `⚙️ Settings` button directly on the primary top toolbar.
  - Added `Tools -> Settings` in the top menu bar while retaining `Help -> About`.

### 🪟 Modernized Dialog Windows
- Redesigned `AboutWindow.axaml`, `UninstallProgressWindow.axaml`, and `JunkRemoveWindow.axaml` with consistent dark card aesthetics, status pills, and rounded action buttons.
