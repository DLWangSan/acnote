# AcNote Windows release

The first public package uses per-user installation under `%LOCALAPPDATA%\Programs\AcNote`; the installer registers AcNote for Word and offers `.acnote`, EndNote, BibTeX, RIS, CSL JSON, and SQLite files in Windows “Open with”. It does not change another application's default file association. The installer adds a Start menu shortcut and optionally a desktop shortcut.

## Build locally

Install .NET 9 SDK and Inno Setup 6. Then run:

```powershell
./scripts/Build-Release.ps1 -Version 0.1.0
```

The output is `artifacts/0.1.0/AcNote-Setup-0.1.0-win-x64.exe`, `SHA256SUMS.txt`, and `latest.json`. The installer includes the signed Microsoft .NET Desktop Runtime bootstrapper and installs it only if the user's system lacks it. It has no AcNote code-signing certificate yet, so Windows SmartScreen may show the standard unknown-publisher warning until a publisher certificate is available.

To point packaged installations to a static mirror manifest, pass `-UpdateSource https://downloads.example.org/acnote/latest.json` and set `-DownloadBase` to the corresponding file host before publishing. The manifest contains `version`, `downloadUrl`, `sha256`, and `notes`. No server-side database or dynamic API is needed. The app's update window can also store a per-user HTTPS source override.

The default checker reads the GitHub Releases API. A GitHub Release must include the exact asset name `AcNote-Setup-<version>-win-x64.exe`. Downloads are opened in the browser; AcNote does not silently replace running files.

## Publish from a tag

Push a tag such as `v0.1.0`. The `Windows release` workflow builds the installer, creates a GitHub Release with generated notes, and attaches the installer, checksum file, and update manifest. Review the draft tag and installer workflow before pushing it: pushing `v*` publishes a public release.
