#!/bin/sh
set -eu

cd "$(dirname "$0")/.."
app="$(dirname "$PWD")/editor/MoNoon.app"
mkdir -p "$(dirname "$app")"
work=$(mktemp -d "$(dirname "$app")/.monoon-editor.XXXXXX")
trap 'rm -rf "$work"' EXIT
stage="$work/MoNoon.app"
mkdir -p "$stage/Contents/Resources"
dotnet publish src/Engine.Editor/Engine.Editor.csproj -c Release -r osx-arm64 \
  --self-contained true -nr:false -m:1 -p:UsedAvaloniaProducts= -p:UseSharedCompilation=false -p:NuGetAudit=false -p:PublishSingleFile=false -o "$stage/Contents/MacOS"
cp src/Engine.Editor/Assets/MoNoon.icns "$stage/Contents/Resources/"
cat > "$stage/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>MoNoon</string>
  <key>CFBundleDisplayName</key><string>MoNoon</string>
  <key>CFBundleIdentifier</key><string>local.monoon.editor</string>
  <key>CFBundleExecutable</key><string>MoNoon</string>
  <key>CFBundleIconFile</key><string>MoNoon.icns</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
rsync -a --delete-after "$stage/" "$app/"
printf '%s\n' "$app"
