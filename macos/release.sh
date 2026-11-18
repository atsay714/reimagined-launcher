#!/usr/bin/env bash
# Build the portable download and Velopack update feed with source provenance.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
[[ $# == 2 ]] || { echo 'Usage: macos/release.sh VERSION OUTPUT' >&2; exit 2; }
[[ -z "$(git -C "$ROOT" status --porcelain)" ]] || { echo 'Release builds require committed, clean source for provenance' >&2; exit 1; }
VERSION="$1"
OUTPUT="$2"
TAG="${UPSTREAM_TAG:-$(git -C "$ROOT" describe --tags --match 'v[0-9]*' --abbrev=0)}"
UPSTREAM_COMMIT="$(git -C "$ROOT" rev-parse "$TAG^{commit}")"
FORK_COMMIT="$(git -C "$ROOT" rev-parse HEAD)"
PROVENANCE="$(python3 "$ROOT/macos/release_metadata.py" provenance "$VERSION" "$TAG" "$UPSTREAM_COMMIT" "$FORK_COMMIT")"
INSTALL=0 VERSION="$VERSION" "$ROOT/macos/package.sh"
mkdir -p "$OUTPUT"
cp "$ROOT/artifacts/macos/releases/"* "$OUTPUT/"
printf '%s\n' "$PROVENANCE" > "$OUTPUT/provenance.json"
(cd "$OUTPUT" && shasum -a 256 ./*.zip ./*.nupkg ./*.json > SHA256SUMS)
echo "Release assets: $OUTPUT"
