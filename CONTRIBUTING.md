# Contributing to Any Uninstaller

Thank you for your interest in contributing to Any Uninstaller! We welcome bug reports, feature proposals, documentation improvements, and code contributions.

---

## Code of Conduct

Please help maintain a welcoming, respectful, and collaborative community. Treat all participants with kindness and professional courtesy.

---

## How to Contribute

### 1. Reporting Bugs
- Search existing [GitHub Issues](https://github.com/sayandey021/Any-Uninstaller/issues) to ensure the bug hasn't already been reported.
- If not reported, open a new issue using the **Bug Report** template.
- Include:
  - Operating system version (e.g. Windows 11 23H2).
  - Clear steps to reproduce the issue.
  - Expected vs actual behavior.
  - Screenshots or log outputs if applicable.

### 2. Suggesting Features
- Open a **Feature Request** issue to propose new functionality or UI improvements.
- Clearly describe the problem it solves and why it would benefit users.

### 3. Submitting Code Changes
1. **Fork the repository** on GitHub.
2. **Clone your fork locally**:
   ```bash
   git clone https://github.com/your-username/Any-Uninstaller.git
   cd Any-Uninstaller
   ```
3. **Create a descriptive topic branch**:
   ```bash
   git checkout -b feature/my-feature-name
   ```
4. **Make your changes**:
   - Adhere to established C# and Avalonia XAML naming conventions.
   - Maintain code comments and clean formatting.
   - Ensure the solution builds with `dotnet build` with 0 errors.
5. **Commit your changes**:
   ```bash
   git commit -m "feat: add support for custom uninstaller switches"
   ```
6. **Push and create a Pull Request**:
   - Push to your fork: `git push origin feature/my-feature-name`
   - Open a Pull Request against the `main` branch with a concise summary of the changes made.

---

## Building & Testing Locally

- Prerequisites: Windows 10/11 and **.NET 10.0 SDK**.
- Run `run_avalonia.bat` or run:
  ```powershell
  dotnet build source/AnyUninstaller.Avalonia/AnyUninstaller.Avalonia.csproj
  dotnet run --project source/AnyUninstaller.Avalonia/AnyUninstaller.Avalonia.csproj
  ```

Thank you for helping make Any Uninstaller even better!
