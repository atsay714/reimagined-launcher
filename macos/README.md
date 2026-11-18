# Mac build

This is the Apple Silicon/CrossOver branch of
[D2R-Reimagined/reimagined-launcher](https://github.com/D2R-Reimagined/reimagined-launcher).
The fork is [atsay714/reimagined-launcher](https://github.com/atsay714/reimagined-launcher),
with `macos` as its default branch.

## Build and run

Requires an Apple Silicon Mac, .NET 10 SDK, CrossOver, and an existing D2R installation
in a CrossOver bottle. The launcher is native; the game runs through CrossOver.
CrossOver is detected in `/Applications` or `~/Applications`.

Run `./macos/package.sh` to build and install `~/Applications/Reimagined Launcher.app`.
The script creates a self-contained arm64 app and signs it locally. It replaces the
installed app, so close the launcher and preserve the previous app before rebuilding.

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

## Upstream updates

`origin` points to this fork; `upstream` points to the official launcher repository.
Fetch official source and tags with `git fetch upstream --tags`. Keep a backup branch
before rebasing the Mac patches onto a newer stable upstream release, then test,
package, and verify the installed app. Back up saves before any save conversion or
gameplay-affecting changes. Passing tests does not confirm character entry.

There is no Mac release feed or automatic source-build updater. Rebuild this branch
for Mac updates; the official Windows/Linux updater does not supply this Mac build.

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
