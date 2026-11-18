# Mac build

This is the Apple Silicon/CrossOver branch of
[D2R-Reimagined/reimagined-launcher](https://github.com/D2R-Reimagined/reimagined-launcher).
The fork is [atsay714/reimagined-launcher-macos](https://github.com/atsay714/reimagined-launcher-macos),
with `macos` as its default branch.

## Download and update

Download the **Portable.zip** from [GitHub Releases](https://github.com/atsay714/reimagined-launcher-macos/releases),
extract the app, and place it in `~/Applications`. Requires macOS 14 or newer on Apple
Silicon, CrossOver and your own D2R installation. The app includes .NET; downloads
need no SDK, Git, or build tools.

These builds are **ad-hoc signed and not notarized**. macOS Gatekeeper may block first
launch; this is not an Apple-verified distribution. The app does not disable Gatekeeper.
Developer ID signing and notarization require Apple credentials that are not configured.

**Check for Updates** uses this fork's `osx-arm64` Velopack feed. Once downloaded,
**Restart & Update** saves settings, preserves the current app under the local
`ReimaginedLauncher-Mac/rollback` folder, and replaces/restarts the app. Close the game
and finish other launcher operations first. The existing disabled-update preference
also disables the manual check. Older manually packaged apps require one replacement
with a release ZIP before automatic updates work.

## Build and run

Requires an Apple Silicon Mac, .NET 10 SDK, CrossOver, and an existing D2R installation
in a CrossOver bottle. The launcher is native; the game runs through CrossOver.
CrossOver is detected in `/Applications` or `~/Applications`.

Run `./macos/package.sh` to build and install `~/Applications/Reimagined Launcher.app`.
The script creates a self-contained arm64 app and signs it locally. It replaces the
installed app, so close the launcher before rebuilding. The previous app is copied to
the local rollback folder first. `INSTALL=0` builds packages without installation;
`VERSION=1.0.<build>` selects a Mac version. Mac SemVer `1.0.N` is independent of the
official launcher version, which is retained in `provenance.json`.

The matching pinned `vpk` CLI is downloaded through `dotnet dnx`. Managed assemblies
are published as a single file so their signatures survive update package extraction.
To create downloadable assets, run `./macos/release.sh 1.0.<build> <output-folder>`.
An existing Developer ID identity and notary profile can be supplied through
`MAC_SIGN_IDENTITY` and `MAC_NOTARY_PROFILE`; keep credentials outside this repository.

For tests, use a canonical temporary path to avoid macOS `/var` symlink aliases:

```sh
mkdir -p /private/tmp/reimagined-launcher-tests
TMPDIR=/private/tmp/reimagined-launcher-tests dotnet test ReimaginedLauncher.sln --verbosity minimal
```

Also run the instance/exit regression tests with macOS's normal temporary directory:

```sh
dotnet test ReimaginedLauncher.sln --filter FullyQualifiedName~LauncherInstanceTests --verbosity minimal
```

The Mac instance socket uses a short absolute path under `/tmp` because the default
temporary directory plus .NET's pipe prefix exceeds macOS's socket path limit.
Using only the short test TMPDIR above would mask that shutdown regression.

## Upstream updates and releases

`origin` points to this fork; `upstream` points to the official launcher repository.
The **Mac Release** GitHub Actions workflow checks the newest official stable release
every six hours. It prepares a normal merge into `macos`, runs tooling/.NET tests,
builds arm64 packages, and proves an isolated Mac app can update/restart while
retaining a valid signature. Only successful candidates advance the fork and publish
assets under GitHub Releases. Conflicts, failed tests and concurrent fork commits stop
publication; previous releases remain available. Official tags are preserved.

A `macos` source push publishes fork-only fixes. In Actions, **Run workflow → release**
publishes the current fork, while **sync** checks upstream first. The run number forms
the Mac display/update version (`1.0.N`); the assembly compatibility version remains
the official upstream version. Keep this workflow/version sequence when changing automation.
Only the publication job has write permission. No token is shipped in the app.

GitHub's default job token cannot push changes to workflow files. To synchronize
upstream releases that change `.github/workflows`, configure the repository secret
`UPSTREAM_SYNC_TOKEN` with **Contents: write** and **Workflows: write**, scoped to this
fork, or merge those particular updates manually. Without that secret, such a sync
stops with instructions; ordinary application updates use the default job token.
Existing tags are immutable, interrupted draft uploads can resume on retry, and an
older rerun cannot replace a newer public release as latest.

GitHub can disable scheduled workflows after 60 days of repository inactivity;
re-enable the workflow in Actions if necessary. A failed publication after the tested
merge was pushed can be recovered with the manual `release` mode.

Run `python3 -m unittest discover -s macos/tests -v` for release-tool tests.
After packaging, `./macos/smoke-update.sh` proves real update/restart with a disposable
app identity and generated files. It does not use launcher settings or game saves.
Passing tests does not confirm character entry. Back up saves before any conversion
or gameplay-affecting changes.

## Shared settings and local paths

The Mac build reads the usual `~/Library/Application Support/ReimaginedLauncher/settings.json`,
which may be linked to OneDrive. Add-on registrations, enabled state, order, and tweaks stay in
that shared file.

Mac paths are loaded from `~/Library/Application Support/ReimaginedLauncher-Mac/paths.json`.
Keep this folder local. Paths are keyed by installation `Type`; a null path uses the normal
Mac defaults. The file is created on first launch, retaining existing Mac paths or detecting
Battle.net's game in CrossOver. Changing paths in the launcher updates this local file.

On save, the Mac build reads the latest shared settings to preserve Windows paths, then writes
shared preferences without substituting Mac paths. It does not reload shared preferences live:
close the launcher on one computer, allow OneDrive to sync, then open it on the other. Concurrent
changes to non-path preferences still follow OneDrive's normal last-writer behavior.

This applies to launcher Mod Add-ons. D2RLoader native DLLs and TOML configuration live in the
selected game installation and are not synced by this change.
