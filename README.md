# Filesystem-Toolbox

[![License](https://img.shields.io/github/license/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/blob/master/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/Filesystem-Toolbox?color=8957D5)](https://github.com/Hawkynt/Filesystem-Toolbox)

[![CI](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/Filesystem-Toolbox?branch=master)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/Filesystem-Toolbox)

[![Stars](https://img.shields.io/github/stars/Hawkynt/Filesystem-Toolbox?color=FFD700)](https://github.com/Hawkynt/Filesystem-Toolbox/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/Filesystem-Toolbox?color=008080)](https://github.com/Hawkynt/Filesystem-Toolbox/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/Filesystem-Toolbox?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/Filesystem-Toolbox?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?sort=semver)](https://github.com/Hawkynt/Filesystem-Toolbox/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?include_prereleases=true&sort=date&label=nightly&color=FF9800)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/Filesystem-Toolbox/total)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)

> A Windows tray application that guards folders against silent file corruption (bit rot): it keeps a per-folder SHA-512 checksum database, tracks deliberate changes live via filesystem watchers so they don't raise false alarms, and periodically re-verifies every file — reporting the ones whose content changed without anyone touching them.

## Install

Download the latest [release](https://github.com/Hawkynt/Filesystem-Toolbox/releases/latest) (or a [nightly](https://github.com/Hawkynt/Filesystem-Toolbox/releases)) and unpack it anywhere — no installer. Requires Windows with .NET Framework 4.6 or later.

## Usage

1. Create or edit `CheckedFolders.lst` next to the executable — one absolute folder path per line.
2. Start `Filesystem-Toolbox.exe`; it lives in the system tray (double-click the icon to open the main window).
3. For each watched folder a hidden, NTFS-compressed `checksum.db` is kept in its root, storing file size + SHA-512 per file. Use *Rebuild database* from the tray menu to (re)create it initially.
4. Every 10 minutes (the `CheckInterval` setting in `Filesystem-Toolbox.exe.config`) all folders are re-verified; files whose checksum no longer matches — and new files missing from the database — appear in the main window. *Verify folders* in the tray menu triggers a check on demand.
5. Right-click a reported file and *accept* the difference if the change is legitimate; the stored checksum is updated.

While the app is running, filesystem watchers keep the database in sync automatically, so deliberate edits, renames and deletions are absorbed without alarms — only unexpected content changes surface.

## Features

- watches any number of folder trees, configured via `CheckedFolders.lst`
- per-folder checksum database (size + SHA-512 per file), stored hidden/system and NTFS-compressed in the folder itself
- live database maintenance through filesystem watchers (create / change / rename / delete), debounced and queued
- periodic background verification with results in a sortable grid, including unrecorded new files
- accept individual differences from the grid; rebuild a folder's database from the tray menu (with confirmation, progress shown)
- tray-first UI: starts minimized to the notification area, double-click opens the window

### Planned

- configure watched folders and the check interval from the GUI instead of files
- single-instance guard
- user-definable action to run when broken files are found (per file and per batch)
- duplicate finder that replaces copies with NTFS hard-links (setting the read-only attribute to dodge the missing copy-on-write semantics)

## Building

A .NET Framework 4.6 WinForms project — build on Windows:

```bash
dotnet build Filesystem-Toolbox.sln --configuration Release
```

Note: the project compiles shared sources expected two levels above the project file (`..\..\Framework\*.cs`, `..\..\Libraries\ApplicationTitler.cs`) and imports `..\..\VersionSpecificSymbols.Common.prop`, so those need to be present in that layout for the build to succeed.

## License

Licensed under LGPL-3.0 — see [LICENSE](LICENSE).
