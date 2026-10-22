#!/usr/bin/env bash
# Сборка WebGL без открытого редактора (issue #326). Результат: Builds/WebGL (вне git).
#
#   scripts/build-webgl.sh
#
# После сборки печатает размер (МБ, 10^6 байт) всего каталога и по типам файлов и
# сравнивает с лимитом Яндекс Игр 100 МБ в распакованном виде (docs/specs/yandex-games.md, п. 1.21).
# Коды выхода: 0 — собралось, размер в лимите; 1 — сборка не удалась или размер больше лимита;
# 2 — проект открыт в редакторе (ничего не запускалось).
# Лог: ~/.cache/burmalda-build/webgl.log (вне репозитория).

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
OUT="${BURMALDA_BUILD_DIR:-$HOME/.cache/burmalda-build}"
LIMIT_BYTES=100000000
BUILD_DIR="$ROOT/Builds/WebGL"

if [ ! -x "$UNITY" ]; then
  echo "Не найден Unity $VERSION: $UNITY (можно задать UNITY_PATH)" >&2
  exit 1
fi
if [ ! -d "$(dirname "$UNITY")/../../../PlaybackEngines/WebGLSupport" ]; then
  echo "У Unity $VERSION не установлен модуль WebGL Build Support (Unity Hub → Installs → Add modules)." >&2
  exit 1
fi

editor_open() {
  [ -e "$ROOT/Temp/UnityLockfile" ] && return 0
  ps -axo command= | grep -F "MacOS/Unity" | grep -v -F "Unity Hub" | grep -v -F "Licensing" \
    | grep -F -e "$ROOT" >/dev/null
}
if editor_open; then
  echo "Проект $ROOT открыт в редакторе Unity. Закройте редактор и повторите. Ничего не запущено." >&2
  exit 2
fi

mkdir -p "$OUT"
log="$OUT/webgl.log"
rm -f "$log"
rm -rf "$BUILD_DIR"
echo "[webgl] сборка... лог: $log"
start=$SECONDS
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -buildTarget WebGL \
  -executeMethod Burmalda.EditorTools.WebGLBuild.Build -logFile "$log"
code=$?
echo "[webgl] Unity завершился с кодом $code за $((SECONDS - start))с"

if [ "$code" -ne 0 ] || [ ! -f "$BUILD_DIR/index.html" ]; then
  echo "[webgl] СБОРКА НЕ УДАЛАСЬ. Первые ошибки:" >&2
  grep -E "error CS[0-9]+|: error |Error building Player|BuildFailedException|Exception:" "$log" 2>/dev/null \
    | awk '!seen[$0]++' | head -40 | cut -c1-220 >&2
  exit 1
fi

python3 - "$BUILD_DIR" "$LIMIT_BYTES" <<'PY'
import os, sys, collections
root, limit = sys.argv[1], int(sys.argv[2])
by_ext = collections.Counter(); total = 0
for d, _, files in os.walk(root):
    for f in files:
        n = os.path.getsize(os.path.join(d, f)); total += n
        by_ext[os.path.splitext(f)[1] or "(без расширения)"] += n
print(f"[webgl] размер: {total / 1e6:.1f} МБ ({total / 2**20:.1f} МиБ), лимит {limit / 1e6:.0f} МБ")
for ext, n in by_ext.most_common():
    print(f"  {ext:<22} {n / 1e6:8.1f} МБ")
sys.exit(1 if total > limit else 0)
PY
rc=$?
[ "$rc" -ne 0 ] && echo "[webgl] ПРЕВЫШЕН лимит Яндекс Игр" >&2
exit "$rc"
