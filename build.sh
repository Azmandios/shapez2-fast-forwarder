#!/usr/bin/env bash
# Сборка мода FastForwarder. Результат кладётся сразу в папку модов игры.
set -euo pipefail

cd "$(dirname "$0")"

# dotnet живёт в ~/.dotnet (установлен без sudo); PATH из login-shell может отсутствовать.
if [ -n "${DOTNET:-}" ]; then
  DOTNET_BIN="$DOTNET"
elif command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="$(command -v dotnet)"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET_BIN="$HOME/.dotnet/dotnet"
else
  echo "ERROR [FastForwarder.Build] dotnet не найден" >&2
  exit 1
fi

SHAPEZ_PATH="${ShapezPath:-/Applications/shapez 2.app/Contents/Resources/Data/Managed}"
MODS_DIR="${ModsDir:-$HOME/Library/Application Support/com.tobspr.shapez-2/mods}"

if [ ! -f "$SHAPEZ_PATH/SPZGameAssembly.dll" ]; then
  echo "ERROR [FastForwarder.Build] игровые сборки не найдены: $SHAPEZ_PATH" >&2
  exit 1
fi

echo "INFO [FastForwarder.Build] dotnet=$DOTNET_BIN"
echo "INFO [FastForwarder.Build] shapez=$SHAPEZ_PATH"
echo "INFO [FastForwarder.Build] output=$MODS_DIR/FastForwarder"

DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
  "$DOTNET_BIN" build src/FastForwarder.csproj -c Release -v minimal --nologo \
  -p:ShapezPath="$SHAPEZ_PATH" -p:ModsDir="$MODS_DIR"
