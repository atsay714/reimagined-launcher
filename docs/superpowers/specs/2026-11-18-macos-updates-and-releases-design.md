# Mac binary updates and automated upstream releases

Date: 2026-11-18 (Asia/Taipei)
Status: Approved by Andrew on 2026-11-18: “Yes—implement it here.”

## User decisions

- The installed app downloads updates published by our fork. It does not download
  source, apply patches, or invoke Git/.NET/build tools.
- Use standard GitHub Actions and GitHub Releases for downloadable Mac binaries.
- Automate merging new official upstream updates into our fork, testing/building,
  and publishing a new Mac release. Preserve Mac patches and existing history.
- Retain the sole local source checkout, saves, add-ons, shared Windows settings,
  local Mac paths and backups.

This replaces the earlier proposal for an in-app source rebuild button.

## Approach

Use the existing Velopack 1.2.161 dependency and updater. Its documented Mac support
handles downloading packages, checking integrity, replacing the app and restarting.
Package our Mac bundle with the matching `vpk` CLI, and route only Mac update checks
to `atsay714/reimagined-launcher`. Windows/Linux retain their existing official feed.

The alternatives were a custom updater/helper or rebuilding inside the app. Andrew
selected binary updates from our fork; using the existing cross-platform updater
keeps the implementation smaller than a second update framework.

## App behavior

- Add a visible Mac-only **Check for Updates** button near the launcher version.
  The existing version-label click, startup check, hourly check and downloaded
  update banner use the same updater and fork feed.
- Keep the existing preference to disable launcher updates and preview-build rules.
  Checks honor the preference consistently; no GitHub token is shipped in the app.
- While downloading, show update status. Once ready, **Restart & Update** installs
  the published Mac package and relaunches the native app.
- Before restart, save settings normally, block installation while the game,
  launching, mod installation or save conversion is active, and retain a local copy
  of the current app for rollback. A failed backup prevents installation.
- Do not convert saves, reapply add-ons, or modify game files during a launcher update.
  Keep update cache and rollback copies local, outside shared OneDrive settings.
- Existing manually packaged installations need one replacement with the first
  Velopack-enabled build. Later updates use the in-app flow.

```text
Launcher v0.19.13.1
[Check for Updates]

Checking → Downloading → Update ready
[Restart & Update]
```

## Packaging and versions

- Preserve our custom `.app` structure and Resources/Assets symlink. Pass the bundle
  to pinned `vpk` 1.2.161 to add the Mac updater metadata/helper and release packages.
- Use one Mac arm64 release channel and a distinct Mac package identity. Preserve
  the existing Apple bundle identifier and installed app name.
- Mac release versions contain the official three-part version plus an increasing
  Mac build number, e.g. `0.19.13.1`. This allows Mac fixes without an upstream version
  bump. Derive the build number from the release workflow’s run number.
- Publish immutable `macos-v<version>` tags at the exact tested source commit. Keep
  official upstream tags and the upstream project’s version file unchanged; inject
  the Mac package version during publishing.
- Separate packaging from installation so CI never installs into an Applications
  directory. Produce a portable ZIP, Velopack package/feed, checksum file and source
  provenance (official tag/commit and fork commit). A DMG is optional, not required.

## GitHub Actions

- Keep tests for `macos` pushes and PRs. Mac release builds use the documented
  Apple Silicon `macos-15` runner and .NET 10. PR builds never publish.
- A scheduled check (every six hours) and a manual **Sync upstream** run query the
  newest non-draft/non-prerelease release of the official launcher. Target that
  immutable release commit, rather than unreleased commits on upstream main.
- If the release is already in the fork, the scheduled sync is a no-op. Otherwise,
  merge it into the current fork head in the runner’s checkout without rewriting
  history. A conflict stops with a clear failed run; do not resolve automatically.
- Build/test/package the candidate merge before updating `macos`. If verification
  fails, do not advance the fork branch or publish a release.
- A separate publish job advances `macos` with a normal non-forced push only if its
  remote head still matches the head tested against. If it changed, stop and rerun
  from the new head. Never overwrite someone else’s commits.
- Publish verified assets to a GitHub Release in this fork after success. Explicitly
  continue the release job after the bot’s push; do not rely on a GITHUB_TOKEN push
  triggering another workflow. Preserve older releases and skip existing tags.
- Ordinary `macos` source pushes and a manual **Release Mac build** action can also
  produce releases, so fork-only fixes ship without waiting for upstream.
- Use one concurrency group for sync/publication. Build/test jobs have read-only
  repository permissions; only publication has write permission. No Apple signing
  credentials are needed to merge or run upstream tests.

## Signing limitation

No usable Apple signing identity or repository signing secrets were found during
reconnaissance. Implement the packaging path for ad-hoc signed builds and document
that they are unnotarized: downloaded apps may be blocked by Gatekeeper and are not
frictionless public distribution. Do not disable Gatekeeper or claim Apple trust.
Support Developer ID signing/notarization when separately supplied credentials are
available, keeping them out of source, app settings, logs and build artifacts.

## Verification

- Tests establish Mac/fork versus Windows/Linux feed selection, channel/version
  behavior, disabled-update behavior, failed-download retry and restart guards.
- Exercise bundle packaging and the actual pinned CLI; verify metadata, arm64
  executable, resources, signing, package checksum and feed compatibility.
- Test upstream-sync decisions and conflict/test-failure/concurrent-push behavior
  with disposable local Git fixtures, without changing the sole development checkout.
- Run full tests, retain the previous installed app/settings, and install/open the
  new Mac build. Compare save hashes and local/shared path fields.
- Push the workflow and follow a real run through successful GitHub publication.
  Download its release assets and verify contents/checksums. Use an isolated
  fixture app identity to prove an older Mac package updates and restarts into a
  newer version without sharing live launcher settings.
- Record evidence and limitations in project-local memory. Game entry is a separate
  check and is not established by passing tests or a successful launcher update.

## References

- Current updater: `ReimaginedLauncher/Utilities/LauncherUpdateService.cs`.
- https://docs.velopack.io/packaging/operating-systems/macos
- https://docs.velopack.io/reference/cli/content/vpk-osx
- https://docs.velopack.io/integrating/update-sources
- https://docs.velopack.io/packaging/signing
- https://docs.github.com/en/actions/reference/runners/github-hosted-runners
- https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows
