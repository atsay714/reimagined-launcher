#!/usr/bin/env bash
# Exercise the real updater with a disposable identity and no launcher settings.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
WORK="$(mktemp -d /private/tmp/reimagined-update-smoke.XXXXXX)"
IDENTITY="D2RReimagined.UpdateProbe.$(uuidgen | tr -d '-')"
APP_NAME='Reimagined Update Smoke'
mkdir -p "$WORK/feed" "$WORK/installed"
for VERSION in 1.0.1 1.0.2; do
    APP="$WORK/raw/$APP_NAME.app"
    rm -rf "$WORK/raw"
    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    "$DOTNET" publish "$ROOT/macos/smoke/UpdateProbe.csproj" -c Release -r osx-arm64 --self-contained \
        -p:Version="$VERSION" -p:PublishSingleFile=true -o "$APP/Contents/MacOS"
    cp "$ROOT/artifacts/macos/Reimagined Launcher.app/Contents/Resources/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
    python3 - "$APP" "$VERSION" "$IDENTITY" <<'PY'
import plistlib, sys
from pathlib import Path
app, version, identity = sys.argv[1:]
with (Path(app) / 'Contents/Info.plist').open('wb') as output:
    plistlib.dump(dict(CFBundleName='Reimagined Update Smoke', CFBundleIdentifier=identity,
                       CFBundleExecutable='UpdateProbe', CFBundleVersion=version,
                       CFBundleShortVersionString=version, CFBundlePackageType='APPL',
                       CFBundleIconFile='AppIcon', LSUIElement=True), output)
PY
    "$DOTNET" dnx vpk@1.2.161 --allow-roll-forward -- '[osx]' --yes --skip-updates pack \
        --packId "$IDENTITY" --packVersion "$VERSION" --packTitle "$APP_NAME" \
        --packDir "$APP" --mainExe UpdateProbe --runtime osx-arm64 --channel osx-arm64 \
        --noInst --delta None --signAppIdentity '-' --outputDir "$WORK/feed"
    if [[ "$VERSION" == 1.0.1 ]]; then
        ZIP=("$WORK/feed/"*Portable.zip)
        ditto -x -k "${ZIP[0]}" "$WORK/installed"
    fi
done
"$WORK/installed/$APP_NAME.app/Contents/MacOS/UpdateProbe" "$WORK/feed" "$WORK/result.txt" before
python3 - "$WORK" <<'PY'
from pathlib import Path
import sys, time
root = Path(sys.argv[1])
for _ in range(60):
    result = root / 'result.txt'
    if result.exists():
        assert result.read_text() == '1.0.2', result.read_text()
        print('PASS: real Mac updater replaced 1.0.1 and relaunched 1.0.2 with an isolated identity.')
        break
    time.sleep(0.5)
else:
    raise SystemExit('Update/relaunch failed; inspect fixture at ' + str(root))
PY
codesign --verify --deep --strict "$WORK/installed/$APP_NAME.app"
echo "Smoke evidence: $WORK/result.txt"
