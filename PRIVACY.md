# Privacy Policy for Any Uninstaller

**Last Updated:** August 27, 2026  
**Effective Date:** August 27, 2026  

This Privacy Policy explains how **Any Uninstaller** ("the Application", "we", "our", or "us"), developed by **Sayan Dey**, handles user information.

We strongly value your privacy. Any Uninstaller is built with a **privacy-first, offline-by-design** architecture: the application runs locally on your machine, does not track you, and does not collect or transmit your personal data.

---

## 1. Information We Do Not Collect

Any Uninstaller **does not collect, store, transmit, or share** any Personally Identifiable Information (PII) or telemetry. Specifically:

- **No Personal Data:** We do not collect names, email addresses, phone numbers, IP addresses, physical locations, or user credentials.
- **No Usage Analytics or Telemetry:** We do not use third-party analytics SDKs, trackers, cookies, or user tracking services (e.g., Google Analytics, App Center, Telemetry).
- **No Network Tracking:** The application does not send background diagnostic reports, crash dumps, or usage statistics to any remote server.
- **No Account Requirement:** You do not need to create an account or sign in to use Any Uninstaller.

---

## 2. Local Device Permissions & Data Usage

Any Uninstaller operates strictly as a local system utility on your Windows operating system. To perform its core functionalities, the application interacts with local system resources exclusively on your device:

### A. Windows Registry Access
- **Purpose:** Scans registry hives (`HKEY_LOCAL_MACHINE` and `HKEY_CURRENT_USER`) under `SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` to enumerate installed software, display application details (publisher, version, install date, size), and detect orphaned registry keys.
- **Modification:** Registry modifications or deletions are executed **only** upon explicit user instruction (e.g., deleting leftover registry keys associated with an uninstalled application).

### B. File System Access
- **Purpose:** Reads metadata, file sizes, and executables in local directories (such as `C:\Program Files`, `C:\Program Files (x86)`, and `C:\ProgramData`) to detect installed software footprints and locate application uninstaller executables.
- **Modification:** File deletions or directory cleanups are executed **only** when explicitly authorized and confirmed by the user.

### C. Administrator Privileges (Elevation)
- **Purpose:** Certain system-wide software installations and leftovers reside in protected system locations that require elevated permissions.
- **Scope:** Administrator privileges are requested strictly on-demand for user-initiated software removals and residual file/registry cleanups. The application contains no background daemons or automated background tasks.

---

## 3. Local Configuration Storage

Any Uninstaller stores your preferences (such as selected theme, table column visibility, and window dimensions) strictly on your local computer:

- **Storage Location:** Stored in a local JSON configuration file (`AnyUninstaller_Settings.json`) located in the application directory or `%APPDATA%\Any Uninstaller\`.
- **Content:** Contains only UI configuration values (e.g., `SelectedThemeIndex`, `ShowColumnPublisher`, `EnableAnimations`). No personal data or file lists are stored.
- **Control:** You can reset or delete this configuration file at any time without affecting your system.

---

## 4. Internet Connectivity & External Links

- **Offline Functionality:** Any Uninstaller does not require an active internet connection to discover, manage, or uninstall applications.
- **External Web Links:** The application contains external links to the developer's GitHub repository and LinkedIn profile within the *Settings > About* section. Clicking these links opens the URL directly in your default web browser. These external websites are governed by their respective privacy policies.

---

## 5. Third-Party Sharing & Disclosure

We do **not** sell, trade, rent, or transfer any user data or system information to outside parties. All operational processes occur entirely within the boundaries of your local operating system.

---

## 6. Children's Privacy

Any Uninstaller is a general-audience system utility. Because we do not collect any personal data whatsoever, we do not knowingly collect, maintain, or disclose personal information from children under the age of 13.

---

## 7. Security Safeguards

To prevent accidental data loss or unintended system modification:
- **Confirmation Prompts:** A confirmation modal lists all targeted applications, files, and registry entries before any deletion process begins.
- **Windows System Restore:** An optional safeguard allows creating a Windows System Restore Point prior to initiating bulk uninstallation actions.
- **System Directory Protection:** Hardcoded whitelists prevent critical operating system directories (e.g., `C:\Windows`, `C:\Windows\System32`) and core OS processes from being targeted or deleted.

---

## 8. Changes to This Privacy Policy

We may update this Privacy Policy from time to time to reflect updates to the application or changes in legal requirements. Any modifications will be posted in the project's official repository with an updated "Last Updated" date.

---

## 9. Contact Us

If you have questions, feedback, or concerns regarding this Privacy Policy or Any Uninstaller, please contact:

- **Developer:** Sayan Dey
- **Email:** [saayanstudiosoft@gmail.com](mailto:saayanstudiosoft@gmail.com)
- **GitHub Repository:** [https://github.com/sayandey021/Any-Uninstaller](https://github.com/sayandey021/Any-Uninstaller)
- **LinkedIn:** [https://www.linkedin.com/in/sayan-dey021](https://www.linkedin.com/in/sayan-dey021)
