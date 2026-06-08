# Filesystem-Toolbox

[![License](https://img.shields.io/github/license/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/blob/main/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/Filesystem-Toolbox?color=8957D5)](https://github.com/Hawkynt/Filesystem-Toolbox)

[![CI](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/Filesystem-Toolbox?branch=main)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/Filesystem-Toolbox)

[![Stars](https://img.shields.io/github/stars/Hawkynt/Filesystem-Toolbox?color=FFD700)](https://github.com/Hawkynt/Filesystem-Toolbox/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/Filesystem-Toolbox?color=008080)](https://github.com/Hawkynt/Filesystem-Toolbox/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/Filesystem-Toolbox?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/Filesystem-Toolbox?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?sort=semver)](https://github.com/Hawkynt/Filesystem-Toolbox/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?include_prereleases&sort=date&label=nightly&color=FF9800)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/Filesystem-Toolbox/total)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)

> A Windows tray application that protects folders - especially on USB sticks and SD cards that silently "forget" data as their flash cells lose charge - by detecting bit rot through SHA-512 checksums, repairing it from locally stored Reed-Solomon parity (and versioned GFS backups), and preventing it by periodically rewriting aging files to recharge the cells.

## 📦 Install

Download the latest [release](https://github.com/Hawkynt/Filesystem-Toolbox/releases/latest) (or a [nightly](https://github.com/Hawkynt/Filesystem-Toolbox/releases)) and unpack it anywhere - no installer. Requires Windows with .NET Framework 4.8 or later (a .NET 8 build is produced too).

## 🚀 Usage

1. Start `Filesystem-Toolbox.exe`; it lives in the system tray (a second start just pops the window of the running instance).
2. Open *Settings…* from the tray menu and add the folders to watch. Settings **inherit along the path**: top-level entries are watch roots, nested entries override single settings for their subtree (check a box to override, uncheck it to fall back to the inherited value - shown grayed). Per folder you can configure:
   - **Parity redundancy** (default 25 %): how much Reed-Solomon parity to keep - N % extra disk repairs up to N % damaged regions per file. Resolved *per file*, so a subtree can carry more protection than its root.
   - **Auto-repair**: heal detected bit rot without asking.
   - **Verify schedule** (default `every 10m`): also `daily 03:30` or `weekly Sunday 03:30`; missed windows (PC was off) are caught up once on the next start.
   - **Backup target + schedule**: versioned grandfather-father-son snapshots, deduplicated via hard links against the previous snapshot. Retention defaults to 7 daily / 4 weekly / 12 monthly snapshots; the two newest always survive. Only verified-clean files enter a snapshot, and restores are hash-gated even against rot inside the backup itself.
   - **Refresh interval** (default 180 days, 0 = off): preventive rewrite of verified-good files.
   - **On-corruption command**: external command with `{file}`/`{folder}` placeholders, run for files that stay broken.
   - **Duplicate merging**: allow replacing identical files with hard links (NTFS only).
   - **Degradation threshold** (default 5 errors/month) and **balloon notifications** on/off.
3. Use *Rebuild* from the tray menu once to take the initial fingerprint of existing folders.
4. Folders are verified on their schedule. Each finding is **classified**:

   | Status | Meaning |
   |---|---|
   | `BitRot` | content changed although size and modification time did not - the medium lost data |
   | `Modified` | content *and* timestamp changed - looks like an intentional edit |
   | `New` | file exists but is not fingerprinted yet |
   | `Missing` | fingerprinted file is gone |
   | `Error` | the file could not be read at all |

5. Right-click a finding: **Repair** (from parity, falls back to backup snapshots), **Restore from backup**, **Accept change** (take the new content as the truth), or **Run command**. Intentional edits are never "repaired" backwards - accept them instead.
6. Balloon notifications follow the workflow: a found or auto-repaired defect is a **warning** (the medium did lose data), an unrestorable file is an **error** - and then a dialog asks whether to restore it from a backup snapshot, rename it to `*.corrupt`, delete it, or ignore the damage (with "apply to all" for batches).
7. *Statistics…* in the tray shows per-folder KPIs - errors found/corrected (total / 30 days / 7 days), mean time between failures, a Healthy/Degrading/Failing badge with a once-a-day warning toast when the monthly error budget is exceeded, a best-effort SMART readout, plus pie/bar charts of the last verify run and monthly corrections.

While running, filesystem watchers keep the checksum database and the parity store current, so deliberate edits, renames and deletions never raise false alarms. The checksum database itself is parity-protected and heals itself on load - using the same write-after-parity signature so a newer legitimate save is never regressed.

### How the protection works

- Every file's size, modification time and SHA-512 go into a hidden `checksum.db` in the watched root.
- Parity lives in a hidden `<root>\.fst\parity\` tree (plain files - works on FAT32/exFAT sticks): per file a systematic Reed-Solomon code over GF(2⁸) with 64 KiB shards, 16 data + *m* parity shards per stripe (25 % → *m* = 4). Per-shard CRC-32C locates damaged regions so they count as *erasures*, giving the full *m*-shards-per-stripe repair capacity. Every repair is verified against the recorded SHA-512 before the file is atomically replaced - a wrong "fix" can never be shipped.
- Parity is cryptographically bound to the file state it was built from; stale parity (file legitimately edited since) is detected and rebuilt, never applied.
- **Limit:** with less than 100 % redundancy a *completely lost* file cannot be reconstructed from parity - that is what the GFS backup is for (snapshot copies are themselves hash-verified before being restored, searching newest to oldest).
- **Refresh** rewrites verified-clean files in place (read → write → flush to device → restore timestamps) to recharge flash cells. Each refresh costs one program/erase cycle - the 180-day default means ~2 cycles/year, negligible against NAND endurance. Useful for passive media (USB sticks, SD cards); pointless for managed SSDs, leave it off there.

## ✨ Features

- watches any number of folder trees with path-inherited per-folder policies (`FilesystemToolbox.json` v2; v1 files and legacy `CheckedFolders.lst` migrate automatically)
- bit-rot **detection** that distinguishes silent corruption from intentional edits via the size/mtime/hash triple
- bit-rot **repair** from local Reed-Solomon parity with configurable redundancy, hash-verified and atomic
- versioned GFS backup fallback for whole-file restore (snapshots searched newest to oldest), hash-gated against rot inside the backup
- preventive flash **refresh** with persisted per-file timestamps
- auto-repair mode per folder; on-corruption command hook per file
- duplicate-to-hardlink merger (NTFS): size-bucketed, block-compared, new links read-only by default since NTFS hard links are not copy-on-write
- per-root schedules (interval, daily or weekly) with downtime catch-up; self-healing checksum database; balloon notifications and an interactive unrepairable-file dialog
- statistics window: KPIs (errors found/corrected, MTBF), degradation badge with threshold warnings, SMART readout, pie/bar charts
- single-instance tray app; sortable problem grid with status classification

## 🛠️ Building

```bash
dotnet build Filesystem-Toolbox.slnx -c Release
dotnet test  Filesystem-Toolbox.Tests/Filesystem-Toolbox.Tests.csproj -c Release
```

The solution (slnx format, SDK ≥ 9) contains the WinForms app (`net48;net8.0-windows`), the UI-free domain library `Filesystem-Toolbox.Core` (`net48;net8.0`) and an NUnit test suite (`net8.0`) with Unit/Integration categories. `run-tests-with-coverage.bat`/`.sh` produces an HTML coverage report under `TestResults/CoverageReport/`.

## ❤️ Support

If this project saves you time or money, consider supporting its development:

[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor-EA4AAA?logo=githubsponsors)](https://github.com/sponsors/Hawkynt)
[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?logo=paypal)](https://www.paypal.me/hawkynt)

## 📜 License

Licensed under LGPL-3.0-or-later — see [LICENSE](LICENSE).
