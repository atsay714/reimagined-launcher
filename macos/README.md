# Mac build

Run `./macos/package.sh` to build and install `~/Applications/Reimagined Launcher.app`.

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
