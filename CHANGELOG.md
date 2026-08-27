# Changelog

All notable changes to the Any Uninstaller project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
