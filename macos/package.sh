#!/usr/bin/env bash
# Builds "Reimagined Launcher.app" for Apple Silicon and installs it to ~/Applications.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/ReimaginedLauncher/ReimaginedLauncher.csproj"
RID="${RID:-osx-arm64}"
DEST="${DEST:-$HOME/Applications}"
APP_NAME="Reimagined Launcher"
OUT="$ROOT/artifacts/macos"
APP="$OUT/$APP_NAME.app"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT")"

rm -rf "$OUT"
"$DOTNET" publish "$PROJECT" -c Release -r "$RID" --self-contained -p:UseAppHost=true \
    -p:DebugType=none -o "$OUT/publish"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
mv "$APP/Contents/MacOS/Assets" "$APP/Contents/Resources/Assets"
ln -s ../Resources/Assets "$APP/Contents/MacOS/Assets"
sed "s/@VERSION@/$VERSION/g" "$ROOT/macos/Info.plist" > "$APP/Contents/Info.plist"

ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z $size $size "$ROOT/ReimaginedLauncher/Assets/ReimaginedLauncherDesktop.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z $((size * 2)) $((size * 2)) "$ROOT/ReimaginedLauncher/Assets/ReimaginedLauncherDesktop.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"

codesign --force --deep --sign - "$APP"

mkdir -p "$DEST"
rm -rf "$DEST/$APP_NAME.app"
cp -R "$APP" "$DEST/"
echo "Installed $DEST/$APP_NAME.app ($VERSION)"
