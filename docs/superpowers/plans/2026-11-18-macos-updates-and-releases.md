# Mac Updates and Releases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Mac binary updates from this fork and automated tested upstream merges and GitHub Releases.
**Architecture:** Reuse Velopack 1.2.161 for Mac packaging and updating. GitHub Actions checks official stable releases and tests candidate merges before publishing the fork branch and downloadable packages.
**Tech Stack:** C#/.NET 10, Avalonia, Velopack 1.2.161, Bash, Python standard library, GitHub Actions.
**Spec:** `docs/superpowers/specs/2026-11-18-macos-updates-and-releases-design.md`

## Global Constraints

- Keep the sole local source checkout; no new worktree, clone, move or reset.
- Preserve saves, inventory dimensions, shared Windows paths, local Mac paths and backups.
- Only Mac uses `https://github.com/atsay714/reimagined-launcher`; Windows/Linux keep the official feed.
- Use the `osx-arm64` release channel, distinct Mac package identity, existing app name and Apple bundle identifier.
- No app-side source rebuild; no shipped tokens; no destructive/forced upstream sync.
- Public downloads are initially ad-hoc signed and unnotarized; document that limitation.
- User authorized implementation, fork pushes, automatic merges and release publication in this session.

## Review Focus

- Restart during a game, launch, conversion or installation must be refused and settings preserved.
- A failed update/backup must leave the installed app intact; fixture tests cover backup failure.
- Wrong feed/channel or same upstream with a newer Mac build must not strand users.
- Merge conflict, failing verification or concurrent remote commits must not advance the fork or release.
- Release publication must use exact tested source and preserve symlinks, signatures and checksum integrity.

### Task 1: Mac update feed, button and guarded restart

**Files:** Modify `Utilities/LauncherUpdateService.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`, `Views/Launch/LaunchView.axaml.cs`; create `Utilities/MacLauncherUpdateBackup.cs` and `ReimaginedLauncher.Tests/MacLauncherUpdateTests.cs` (paths under the corresponding projects).
**Interfaces:** Produce `LauncherUpdateService.GetRepositoryUrl(bool)`, `GetRestartBlockReason(bool,bool)`, and `MacLauncherUpdateBackup.CreateAsync(string,string)` returning the rollback app path. UI consumes these and existing updater state. Expose launch busy state through a read-only property; block any owned conversion window conservatively.

- [ ] Write tests for platform feed selection, restart guards, successful disposable bundle backup and backup failure preserving the source.
- [ ] Run focused tests; expected FAIL before new APIs exist.
- [ ] Implement the feed selector, visible Mac-only check button, guarded restart with settings save and Mac-only local rollback copy. Avoid changing Velopack's Windows/Linux flow.
- [ ] Run focused tests and existing launcher update tests; expected zero failures. Commit.

### Task 2: Self-updating Mac packages

**Files:** Modify `macos/package.sh`, `macos/Info.plist`; add `macos/release.sh` and `macos/tests/test_release_tools.py` plus isolated smoke fixture resources as needed.
**Interfaces:** `INSTALL=0 VERSION=<four-part-version> ./macos/package.sh` produces an app without installation. `macos/release.sh VERSION OUTPUT` produces ZIP, nupkg/feed, SHA256SUMS and provenance. The default packaging command still installs locally after preserving the prior app.

- [ ] Add failing argument/version/provenance validation tests; run them RED.
- [ ] Separate bundle build from install; use pinned matching vpk CLI for Mac packaging, integrated ad-hoc signing and `--noInst`. Preserve custom resources and symlinks, set supported minimum OS, inject Mac assembly version without editing upstream project version.
- [ ] Test actual release packaging and validate resulting app metadata, arm64, resource link, signature and checksum/feed; expected PASS. Commit.

### Task 3: Tested upstream merges and release automation

**Files:** Add `macos/upstream_sync.py`, `macos/tests/test_upstream_sync.py`, `.github/workflows/macos-release.yml`; modify `.github/workflows/launcher-tests.yml` and Mac README.
**Interfaces:** Sync helper prepares candidate merge from immutable official tag, exports exact commit to a Git bundle for build jobs, and publishes only with the expected remote head. Reusable release workflow consumes source bundle and run-number-derived version; build job uploads only verified packages, publisher imports tested commit and normally pushes/tag/releases with bounded concurrency.

- [ ] Write local Git fixture tests for already-merged no-op, clean candidate merge, conflict leaving remote unchanged, failed verification not publishing, and concurrent remote-head rejection; run RED.
- [ ] Implement preparation/publication helper and six-hour/manual/push workflow. Avoid reliance on GITHUB_TOKEN push cascades; verify expected remote head immediately before normal push. Only publish job has write access. No forced branch/tag updates.
- [ ] Run fixture tests, syntax checks, workflow validation and full .NET suite; expected zero failures. Commit.

### Task 4: End-to-end release and update proof

**Files:** Update README, Mac README, spec/plan verification record and project-local memory outside the source checkout.
**Interfaces:** Actual GitHub release ZIP and `releases.osx-arm64.json` must be consumable by the packaged app.

- [ ] Run a fresh-context whole-change review and fix important findings with regression tests.
- [ ] Preserve current app/settings and save fingerprints, build/install/open first updater-enabled version, verify UI and settings preservation.
- [ ] Push the approved work and follow the real GitHub Actions release to completion; download and verify published artifacts.
- [ ] Prove update/restart between two disposable fixture app versions with isolated identity/data, using the matching Velopack toolchain. No live game launch/conversion.
- [ ] Update dated project notes with exact commits, workflow/release URLs, tests and signing/game-entry limits; finish with clean committed/pushed source.
