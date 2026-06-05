# Agent guide — Filesystem-Toolbox

Working agreement for **all** coding agents and human contributors working in
this repository. These rules are not optional. The full house spec lives in
the `Hawkynt/project-template` repo (`STANDARD.md`); this file is the
per-repo distillation.

## What this is

A **Windows tray application** that protects folders against bit rot:
SHA-512 detection, Reed-Solomon parity repair, GFS-versioned backups, and
preventive rewriting of aging flash media. Solution
`Filesystem-Toolbox.slnx` (UI, `.Core`, `.Tests`); coverage via
`run-tests-with-coverage.*` + `coverage.runsettings`.

## Commits

- **Group changes semantically/logically** — one subsystem/concern per
  commit (detection, repair, backups, UI, notifications).
- **Every subject line starts with a prefix**: `+` added · `-` removed ·
  `*` changed · `#` bug fixed · `!` critical todo.
- Never start a subject with "fix"/"bugfix"/"changed"/"modified".
- **No AI traces anywhere**: no `Co-Authored-By` AI lines, no "Generated
  with" footers, no agent mentions in messages, comments, or authorship.

## The loop (always, in this order)

1. **Before committing**: `dotnet build Filesystem-Toolbox.slnx -c Release`
   and `dotnet test Filesystem-Toolbox.Tests -c Release --filter
   "TestCategory!=Performance"` until green. Tests must stay
   **OS-neutral** — build fixture paths portably, never hardcode Windows
   separators (the linux CI leg runs the Core tests too).
2. **Commit** (rules above) and **push**.
3. **Wait for CI**; on `main` a green CI triggers the nightly (prerelease +
   GFS prune, same-day replace). Fix and loop until everything is green.

Stable releases are **manual** (`gh workflow run release.yml`) — never cut
one unless explicitly asked.

## Code conventions

- Latest C# features; Core logic stays UI-free and OS-neutral — everything
  WinForms/tray/WMI lives in the app project.
- Data-integrity code is the product: repair paths get tests for every
  outcome (repaired, restored-from-backup, unrepairable) and never destroy
  user data — the unrepairable flow always offers the dialog choices.
- Settings inheritance semantics (deepest wins, explicit overrides) are
  documented in the README — keep code and doc in sync.

## README & repo conventions

- Standard frame: title → badges → one-line `>` blockquote; fixed emoji
  mapping for the standard sections (`## 📦 Install`, `## 🚀 Usage`,
  `## ✨ Features`, `## 🛠️ Building`, `## ❤️ Support`, `## 📜 License`).
- License is LGPL-3.0-or-later; the `## ❤️ Support` section and
  `.github/FUNDING.yml` stay intact.
