#!/usr/bin/env bash
# Локальный гейт тестов Unity (issue #307). Повторяет CI (.github/workflows/unity-tests.yml:
# testMode: all = playmode, затем editmode).
#
#   scripts/check.sh targeted <фильтр>   EditMode, -testFilter (имя теста/фикстуры/namespace)
#   scripts/check.sh full                playmode + editmode, как в CI
#
# Коды выхода: 0 — упавших нет; 1 — есть упавшие, прогон не дал результата или в `full`
# пройдено меньше MIN_EDITMODE_TESTS;
# 2 — проект открыт в редакторе (ничего не запускалось); 64 — неверные аргументы.
# Лог и XML: ~/.cache/burmalda-check/ (вне репозитория).

set -uo pipefail

# Число пройденных EditMode-тестов не должно падать ниже этого значения; при намеренном
# удалении тестов меняется в том же PR. Применяется только в `full` (в `targeted` — нет).
# Для проверки без правки файла: CHECK_MIN_EDITMODE=99999 scripts/check.sh full
MIN_EDITMODE_TESTS=1500

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
OUT="${BURMALDA_CHECK_DIR:-$HOME/.cache/burmalda-check}"

usage() {
  echo "Использование: scripts/check.sh targeted <фильтр> | scripts/check.sh full" >&2
  exit 64
}

mode="${1:-}"
case "$mode" in
  targeted) [ -n "${2:-}" ] || usage ;;
  full) ;;
  *) usage ;;
esac

if [ ! -x "$UNITY" ]; then
  echo "Не найден Unity $VERSION: $UNITY (можно задать UNITY_PATH)" >&2
  exit 1
fi

# Редактор с этим проектом открыт: lock-файл или процесс Unity с нашим -projectPath.
# Без -projectPath в командной строке (открыт из Hub) проверяем по lock-файлу.
editor_open() {
  [ -e "$ROOT/Temp/UnityLockfile" ] && return 0
  ps -axo command= | grep -F "MacOS/Unity" | grep -v -F "Unity Hub" | grep -v -F "Licensing" \
    | grep -F -e "$ROOT" >/dev/null
}
if editor_open; then
  echo "Проект $ROOT открыт в редакторе Unity (Temp/UnityLockfile или процесс Unity)." >&2
  echo "Закройте редактор и повторите. Ничего не запущено." >&2
  exit 2
fi

mkdir -p "$OUT"
# platform:filter ("" = без фильтра)
if [ "$mode" = targeted ]; then
  runs=("editmode:$2")
else
  runs=("playmode:" "editmode:")
fi

rc=0
for run in "${runs[@]}"; do
  platform="${run%%:*}"
  filter="${run#*:}"
  xml="$OUT/$platform-results.xml"
  log="$OUT/$platform.log"
  rm -f "$xml" "$log"
  args=(-batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform "$platform"
        -testResults "$xml" -logFile "$log")
  [ -n "$filter" ] && args+=(-testFilter "$filter")
  echo "[$platform] запуск${filter:+ (фильтр: $filter)}..."
  start=$SECONDS
  "$UNITY" "${args[@]}"
  code=$?
  elapsed=$((SECONDS - start))

  # Сводка из XML (NUnit3): корневой test-run и упавшие test-case.
  if [ ! -f "$xml" ]; then
    # Playmode без тестов в этом проекте (все asmdef Editor-only) — CI тоже даёт 0 тестов.
    if [ "$code" -eq 0 ] && grep -q "No tests were executed" "$log" 2>/dev/null; then
      echo "[$platform] 0 тестов (нет подходящих сборок), ${elapsed}с, лог: $log"
      continue
    fi
    echo "[$platform] результатов нет (код Unity $code, ${elapsed}с). Лог: $log" >&2
    grep -m5 -E "error CS[0-9]+|Compilation failed|Scripts have compiler errors" "$log" 2>/dev/null | cut -c1-200 >&2
    rc=1
    continue
  fi
  min=0
  [ "$mode" = full ] && [ "$platform" = editmode ] && min="${CHECK_MIN_EDITMODE:-$MIN_EDITMODE_TESTS}"
  python3 - "$xml" "$platform" "$elapsed" "$log" "$min" <<'PY' || rc=1
import sys, xml.etree.ElementTree as ET
xml, platform, elapsed, log, min_passed = sys.argv[1:6]
min_passed = int(min_passed)
root = ET.parse(xml).getroot()
passed = int(root.get("passed", 0)); failed = int(root.get("failed", 0))
total = int(root.get("total", 0)); skipped = int(root.get("skipped", 0))
print(f"[{platform}] пройдено {passed}, упало {failed}, пропущено {skipped}, всего {total}, {elapsed}с")
MAX = 25
bad = [c for c in root.iter("test-case") if c.get("result") == "Failed"]
for c in bad[:MAX]:
    msg = c.find("failure/message")
    first = ((msg.text or "").strip().splitlines() or [""])[0][:160] if msg is not None else ""
    print(f"  FAIL {c.get('fullname')}\n       {first}")
if len(bad) > MAX:
    print(f"  ... и ещё {len(bad) - MAX} упавших (см. XML)")
print(f"  лог: {log}")
too_few = min_passed > 0 and passed < min_passed
if too_few:
    print(f"  ПОРОГ: пройдено {passed} EditMode-тестов, порог {min_passed} "
          f"(MIN_EDITMODE_TESTS в scripts/check.sh). Часть тестов не запустилась или удалена; "
          f"при намеренном удалении поправь порог в том же PR.")
sys.exit(1 if failed or too_few else 0)
PY
  [ "$code" -ne 0 ] && [ "$code" -ne 2 ] && [ "$rc" -eq 0 ] && rc=1
done
exit "$rc"
