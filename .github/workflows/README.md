# CI/CD Pipeline — Filesystem-Toolbox

Event-driven pipeline (no cron). Workflows live here; their helper scripts live
in `scripts/`.

| File | Trigger | Purpose |
|------|---------|---------|
| `ci.yml` | push + PR on `master` + `workflow_call` | Build the Windows app (whole solution) on windows |
| `release.yml` | **manual dispatch** | Build the app, then cut the dated `vyyyyMMdd` Release |
| `nightly.yml` | successful CI on `master` + manual | Publish `nightly-yyyyMMdd` prerelease and prune old ones |
| `_build.yml` | `workflow_call` (internal) | Publish the Windows app zip as a build artifact |
| `scripts/version.pl` | invoked by workflows | Stamp the project's own `<Version>` + its folder's commit count (`--stamp`) |
| `scripts/update-changelog.mjs` | invoked by workflows | Bucketise commits into release notes by `+ - * # !` prefix |
| `scripts/prune-nightlies.mjs` | invoked by workflows | GFS retention: 7 daily + 4 weekly + 3 monthly |

## Notes

- **No tests.** This repo has no test project, so CI is build-only.
- **No NuGet.** This is a single Windows application (`WinExe`); nothing is
  packed or pushed to nuget.org.
- **Versioning — files drive, never tags.** The app carries its own `<Version>`
  in `Filesystem-Toolbox.csproj`; `version.pl --stamp` appends the project
  folder's commit count. The repo-level Release/tag is the date marker
  `vyyyyMMdd`.
