#!/usr/bin/env bash
# Build the portable download and Velopack update feed with source provenance.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
[[ $# == 2 ]] || { echo 'Usage: macos/release.sh VERSION OUTPUT' >&2; exit 2; }
[[ -z "$(git -C "$ROOT" status --porcelain)" ]] || { echo 'Release builds require committed, clean source for provenance' >&2; exit 1; }
VERSION="$1"
OUTPUT="$2"
[[ ! -d "$OUTPUT" || -z "$(ls -A "$OUTPUT")" ]] || { echo 'Release output must be empty' >&2; exit 1; }
TAG="${UPSTREAM_TAG:-$(python3 - "$ROOT/ReimaginedLauncher/ReimaginedLauncher.csproj" <<'PYTAG'
import sys, xml.etree.ElementTree as ET
print('v' + ET.parse(sys.argv[1]).getroot().findtext('PropertyGroup/Version'))
PYTAG
)}"
UPSTREAM_COMMIT="$(git -C "$ROOT" rev-parse "$TAG^{commit}")"
FORK_COMMIT="$(git -C "$ROOT" rev-parse HEAD)"
PROVENANCE="$(python3 "$ROOT/macos/release_metadata.py" provenance "$VERSION" "$TAG" "$UPSTREAM_COMMIT" "$FORK_COMMIT")"
INSTALL=0 VERSION="$VERSION" "$ROOT/macos/package.sh"
mkdir -p "$OUTPUT"
cp "$ROOT/artifacts/macos/releases/"* "$OUTPUT/"
printf '%s\n' "$PROVENANCE" > "$OUTPUT/provenance.json"
"$ROOT/artifacts/macos/packaged/Reimagined Launcher.app/Contents/MacOS/ReimaginedLauncher" --mac-build-info > "$OUTPUT/build-info.json"
python3 - "$OUTPUT/build-info.json" "$TAG" "$VERSION" <<'PYINFO'
import json, sys
with open(sys.argv[1]) as source:
    info = json.load(source)
assert info['upstreamVersion'] == sys.argv[2][1:], 'Assembly compatibility version must match official upstream'
assert info['version'] == sys.argv[3], 'Mac display version must match the release'
PYINFO
python3 "$ROOT/macos/publish_release.py" checksums "$OUTPUT"
echo "Release assets: $OUTPUT"
