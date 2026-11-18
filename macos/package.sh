#!/usr/bin/env bash
# Build a self-updating Apple Silicon bundle; INSTALL=0 leaves the installed app alone.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/ReimaginedLauncher/ReimaginedLauncher.csproj"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
VERSION="${VERSION:-$(python3 "$ROOT/macos/release_metadata.py" version "$PROJECT" "${MAC_BUILD:-1}")}"
python3 - "$VERSION" <<'PY'
import re, sys
if not re.fullmatch(r'1\.0\.([1-9][0-9]*)', sys.argv[1]) or any(int(p) > 65535 for p in sys.argv[1].split('.')):
    raise SystemExit('Invalid Mac package version')
PY
OUT="$ROOT/artifacts/macos"
APP_NAME="Reimagined Launcher"
APP="$OUT/$APP_NAME.app"
rm -rf "$OUT"
"$DOTNET" publish "$PROJECT" -c Release -r osx-arm64 --self-contained -p:UseAppHost=true \
    -p:Version="$VERSION" -p:PublishSingleFile=true -p:DebugType=none -o "$OUT/publish"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
mv "$APP/Contents/MacOS/Assets" "$APP/Contents/Resources/Assets"
ln -s ../Resources/Assets "$APP/Contents/MacOS/Assets"
sed "s/@VERSION@/$VERSION/g" "$ROOT/macos/Info.plist" > "$APP/Contents/Info.plist"
ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ROOT/ReimaginedLauncher/Assets/ReimaginedLauncherDesktop.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$ROOT/ReimaginedLauncher/Assets/ReimaginedLauncherDesktop.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
VPK_VERSION="$(sed -n 's:.*<VelopackVersion>\(.*\)</VelopackVersion>.*:\1:p' "$PROJECT")"
SIGN_ARGS=(--signAppIdentity "${MAC_SIGN_IDENTITY:--}")
if [[ -n "${MAC_NOTARY_PROFILE:-}" ]]; then
    [[ "${MAC_SIGN_IDENTITY:--}" != '-' ]] || { echo 'Notarization requires a Developer ID identity' >&2; exit 1; }
    SIGN_ARGS+=(--notaryProfile "$MAC_NOTARY_PROFILE")
fi
"$DOTNET" dnx "vpk@$VPK_VERSION" --allow-roll-forward -- '[osx]' --yes --skip-updates pack \
    --packId D2RReimagined.ReimaginedLauncher.Mac --packVersion "$VERSION" \
    --packTitle "$APP_NAME" --packAuthors 'D2R Reimagined; Andrew' \
    --packDir "$APP" --mainExe ReimaginedLauncher --runtime osx-arm64 \
    --channel osx-arm64 --noInst --delta None --outputDir "$OUT/releases" "${SIGN_ARGS[@]}"
ZIP=("$OUT/releases/"*Portable.zip)
[[ ${#ZIP[@]} == 1 && -f "${ZIP[0]}" ]] || { echo 'Expected exactly one portable app ZIP' >&2; exit 1; }
mkdir -p "$OUT/packaged"
ditto -x -k "${ZIP[0]}" "$OUT/packaged"
PACKAGED="$OUT/packaged/$APP_NAME.app"
codesign --verify --deep --strict "$PACKAGED"
[[ -L "$PACKAGED/Contents/MacOS/Assets" && -f "$PACKAGED/Contents/MacOS/UpdateMac" && -f "$PACKAGED/Contents/Resources/sq.version" ]]
if [[ "${INSTALL:-1}" != 0 ]]; then
    DEST="${DEST:-$HOME/Applications}"
    TARGET="$DEST/$APP_NAME.app"
    if ps -axo comm= | awk -v exe="$TARGET/Contents/MacOS/ReimaginedLauncher" '$0 == exe {found=1} END {exit !found}'; then
        echo 'Close the installed launcher before replacing it. The packaged build is ready in artifacts/macos/packaged.' >&2
        exit 1
    fi
    mkdir -p "$DEST"
    STAGED="$DEST/.Reimagined-Launcher-install-$$.app"
    ditto "$PACKAGED" "$STAGED"
    codesign --verify --deep --strict "$STAGED"
    if [[ -d "$TARGET" ]]; then
        BACKUP="$HOME/Library/Application Support/ReimaginedLauncher-Mac/rollback/$(date -u +%Y%m%d-%H%M%S)-$$/$APP_NAME.app"
        mkdir -p "$(dirname "$BACKUP")"
        ditto "$TARGET" "$BACKUP"
        OLD="$DEST/.Reimagined-Launcher-previous-$$.app"
        mv "$TARGET" "$OLD"
        if mv "$STAGED" "$TARGET"; then rm -rf "$OLD"; else mv "$OLD" "$TARGET"; exit 1; fi
    else
        mv "$STAGED" "$TARGET"
    fi
    echo "Installed $TARGET ($VERSION)"
fi
echo "Mac release packages: $OUT/releases"
