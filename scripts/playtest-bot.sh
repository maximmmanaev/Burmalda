#!/usr/bin/env bash
# Отчёт бота-плейтестера (docs/wiki/playtest-bot.md): забеги без рендера на реальных системах игры.
#
#   scripts/playtest-bot.sh [игроков] [часов игры на игрока] [seed]
#
# По умолчанию 20 игроков × 2 часа, seed 1. Печатает Markdown-отчёт и путь к файлу.
# Коды выхода: 0 — отчёт готов; 1 — Unity не отработал; 2 — проект открыт в редакторе.
# Лог и отчёт: ~/.cache/burmalda-playtest/ (вне репозитория).

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
OUT="${BURMALDA_PLAYTEST_DIR:-$HOME/.cache/burmalda-playtest}"
PLAYERS="${1:-20}"
HOURS="${2:-2}"
SEED="${3:-1}"

if [ ! -x "$UNITY" ]; then
  echo "Не найден Unity $VERSION: $UNITY (можно задать UNITY_PATH)" >&2
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
log="$OUT/playtest.log"
report="$OUT/report-seed$SEED-${PLAYERS}x${HOURS}h.md"
rm -f "$log" "$report"
start=$SECONDS
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod Burmalda.Playtest.PlaytestReport.RunFromCommandLine \
  -playtestPlayers "$PLAYERS" -playtestHours "$HOURS" -playtestSeed "$SEED" -playtestOut "$report" \
  -logFile "$log"
code=$?
if [ "$code" -ne 0 ] || [ ! -f "$report" ]; then
  echo "[playtest] Unity завершился с кодом $code, отчёта нет. Первые ошибки (лог: $log):" >&2
  grep -E "error CS[0-9]+|Exception" "$log" 2>/dev/null | awk '!seen[$0]++' | head -20 | cut -c1-220 >&2
  exit 1
fi
cat "$report"
echo "[playtest] готово за $((SECONDS - start))с, отчёт: $report"
