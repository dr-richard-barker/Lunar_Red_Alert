#!/bin/bash
set -e

echo "Downloading dotnet-install.sh..."
curl -sSL https://raw.githubusercontent.com/dotnet/install-scripts/main/src/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh

echo "Installing .NET 10.0..."
./dotnet-install.sh --channel 10.0 --install-dir ./.dotnet

export DOTNET_ROOT="$(pwd)/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

echo "Installing wasm-tools workload..."
dotnet workload install wasm-tools

echo "Publishing OpenRA.WasmProbe..."
dotnet publish OpenRA.WasmProbe/OpenRA.WasmProbe.csproj -c Release

echo "Staging mod files (like in wasm-port.yml)..."
WWWROOT=$(dirname "$(find bin/publish -name dotnet.js -path '*_framework*' | head -1)")/..
if [ -z "$WWWROOT" ] || [ "$WWWROOT" = "/.." ]; then
  echo "Could not find WWWROOT"
  exit 1
fi
mkdir -p "$WWWROOT/probe-data/mods" "$WWWROOT/probe-data/glsl"

rsync -a --include='*/' --include='*.yaml' --include='*.ftl' --include='*.lua' \
  --include='*.ttf' --include='*.png' --include='*.oramap' \
  --include='*.bin' --include='*.shp' --include='*.pal' --include='*.aud' \
  --include='*.dat' --exclude='*' \
  mods/ra mods/ra-content mods/common mods/common-content mods/spaceage "$WWWROOT/probe-data/mods/"

cp glsl/*.vert glsl/*.frag "$WWWROOT/probe-data/glsl/"

echo "Fetching EA freeware content (if not present)..."
mkdir -p "$WWWROOT/probe-data/supportdir/Content/ra/v2"
if [ ! -f /tmp/ra-quickinstall.zip ]; then
  curl -fsSL https://www.openra.net/packages/ra-quickinstall-mirrors.txt | grep '^http' > /tmp/mirrors.txt
  fetched=0
  while read -r MIRROR; do
    echo "trying content mirror: $MIRROR"
    if curl -fsSL --max-redirs 10 --max-time 60 "$MIRROR" -o /tmp/ra-quickinstall.zip \
      && echo "44241f68e69db9511db82cf83c174737ccda300b  /tmp/ra-quickinstall.zip" | sha1sum -c; then
      fetched=1
      break
    fi
    echo "mirror failed, trying next..."
  done < /tmp/mirrors.txt
  if [ "$fetched" -ne 1 ]; then
    echo "All content mirrors failed"
    exit 1
  fi
fi

unzip -q -o /tmp/ra-quickinstall.zip -d "$WWWROOT/probe-data/supportdir/Content/ra/v2/"
(cd "$WWWROOT/probe-data" && zip -q -r -0 ../probe-data.zip .)

echo "Build complete. To test, run:"
echo "cd $WWWROOT && python3 -m http.server 8123"
