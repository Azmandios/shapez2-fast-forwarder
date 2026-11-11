#!/usr/bin/env bash
# Сборка + проверка, что в папке мода лежит всё необходимое для загрузки игрой.
set -euo pipefail

cd "$(dirname "$0")"

MODS_DIR="${ModsDir:-$HOME/Library/Application Support/com.tobspr.shapez-2/mods}"
TARGET="$MODS_DIR/FastForwarder"

./build.sh

echo
echo "INFO [FastForwarder.Install] target=$TARGET"
missing=0
for f in manifest.json FastForwarder.dll; do
  if [ -f "$TARGET/$f" ]; then
    printf '  ok   %-18s %8s bytes\n' "$f" "$(stat -f%z "$TARGET/$f")"
  else
    printf '  MISS %s\n' "$f"
    missing=1
  fi
done

if [ "$missing" -ne 0 ]; then
  echo "ERROR [FastForwarder.Install] не все файлы на месте" >&2
  exit 1
fi

echo "INFO [FastForwarder.Install] installed. Перезапусти игру и проверь лог:"
echo "  grep -i FastForwarder \"\$HOME/Library/Logs/tobspr Games/shapez 2/Player.log\""
