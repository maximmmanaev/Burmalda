#!/usr/bin/env bash
# Смоук-прогон игрового поля на подключённом Android-устройстве (issue
# "ввод не реагирует на тап без движения"). Раньше проверка геймплейных
# правок руками на устройстве дважды срывалась из-за того, что синтетический
# `adb input tap`/короткий `input swipe` не давал хода вовсе (см.
# GridTraceInputController.ProcessInputFrame — исправлено тем же коммитом,
# что добавил этот скрипт). Теперь это штатная проверка для геймплейных
# задач — не только регресс-тест на конкретный баг.
#
# Что делает:
#   1. Ставит APK (Builds/Android/Burmalda.apk — собрать заранее через
#      Burmalda.EditorTools.BuildScript.BuildAndroid), запускает игру.
#   2. Пишет screenrecord всё время прогона (на устройствах, где он
#      недоступен, — см. ниже, — деградирует до скриншотов до/после
#      каждого тапа) и снимает logcat.
#   3. Последовательность обычных `adb shell input tap` (БЕЗ движения —
#      именно тот сценарий, что был сломан) по нескольким фиксированным
#      экранным точкам вперёд по центральному столбцу.
#   4. Вытягивает видео (или скриншоты) + лог на хост, печатает пути,
#      проверяет лог на исключения/ошибки.
#
# ВАЖНО про координаты: тайлы генерируются процедурно и случайно на каждый
# забег (RESTART = новый seed) — фиксированная экранная точка не гарантирует
# ПРОХОДИМЫЙ тайл на каждый конкретный прогон (может быть заблокирован/не
# смежен — тогда `CanAdvanceTo` корректно отклонит шаг, это не баг: HUD
# просто не покажет прирост Маны на этом конкретном ходу). Смысл прогона —
# доказать, что ТАП БЕЗ ДВИЖЕНИЯ доходит до игры на реальном устройстве и
# ничего не падает, а не провести игрока по гарантированному маршруту.
# Отчёт по факту прогона (растёт ли Мана на скриншотах/видео) — смотреть
# глазами, скрипт сам это не парсит.
#
# Запуск: scripts/device-smoke-test.sh [папка_для_вывода] [число_ходов]
# (оба необязательны — по умолчанию Builds/SmokeTest и 5 ходов).
# Требует: adb в PATH, ANDROID_HOME/platform-tools или
# ~/Library/Android/sdk/platform-tools, APK уже собран, устройство
# подключено и разблокировано (при locked экране input событиям некуда
# попадать).

set -euo pipefail

ADB="${ADB:-adb}"
if ! command -v "$ADB" >/dev/null 2>&1; then
  if [ -n "${ANDROID_HOME:-}" ] && [ -x "$ANDROID_HOME/platform-tools/adb" ]; then
    ADB="$ANDROID_HOME/platform-tools/adb"
  elif [ -x "$HOME/Library/Android/sdk/platform-tools/adb" ]; then
    ADB="$HOME/Library/Android/sdk/platform-tools/adb"
  else
    echo "adb не найден — укажи путь через переменную ADB=..." >&2
    exit 1
  fi
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APK="$REPO_ROOT/Builds/Android/Burmalda.apk"
PACKAGE="com.maxman.burmalda"
ACTIVITY="com.unity3d.player.UnityPlayerGameActivity"
OUT_DIR="${1:-$REPO_ROOT/Builds/SmokeTest}"
mkdir -p "$OUT_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
VIDEO_DEVICE_PATH="/sdcard/smoke_${STAMP}.mp4"
VIDEO_HOST_PATH="$OUT_DIR/smoke_${STAMP}.mp4"
LOG_HOST_PATH="$OUT_DIR/smoke_${STAMP}_logcat.txt"

if [ ! -f "$APK" ]; then
  echo "APK не найден: $APK — собери через Burmalda.EditorTools.BuildScript.BuildAndroid" >&2
  exit 1
fi

echo "== Установка APK =="
"$ADB" install -r "$APK"

echo "== Запуск игры =="
"$ADB" logcat -c
"$ADB" shell am start -n "$PACKAGE/$ACTIVITY"

echo "== Ждём фокус окна =="
for _ in $(seq 1 30); do
  if "$ADB" shell dumpsys window 2>/dev/null | grep -q "mCurrentFocus.*$PACKAGE"; then
    break
  fi
  sleep 1
done

echo "== Старт screenrecord (фон на устройстве) =="
# Некоторые прошивки (эмпирически — Realme/ColorOS) запрещают screenrecord
# из обычного adb shell ("inaccessible or not found" при живом бинарнике —
# Permission denied на `ls`, не отсутствие файла) — деградируем до
# скриншотов до/после каждого тапа вместо падения всего прогона:
# видео — лучше, но не обязательное условие смысла скрипта (доказать, что
# тап без движения даёт ход, можно и по паре скриншотов + логу).
SCREENRECORD_OK=1
"$ADB" shell screenrecord --time-limit 30 "$VIDEO_DEVICE_PATH" &
RECORD_PID=$!
sleep 1 # дать screenrecord реально начать писать, прежде чем тапать
if ! "$ADB" shell "test -f $VIDEO_DEVICE_PATH" 2>/dev/null; then
  echo "  screenrecord недоступен на этом устройстве (см. docs/rules — известное ограничение части прошивок Android) — переключаюсь на скриншоты до/после каждого тапа."
  SCREENRECORD_OK=0
  kill "$RECORD_PID" 2>/dev/null || true
  wait "$RECORD_PID" 2>/dev/null || true
fi

# Экранные точки-кандидаты (device-пиксели, разрешение 1080×2400 — при
# другом разрешении/DPI пересчитать пропорционально), одна на "ряд" вперёд
# по центральному столбцу. Диапазон Y намеренно НЕ поднимается выше ~900 —
# выше по экрану на этой сборке лежат отладочные кнопки поверх игрового
# поля (в частности временная "Preview Boss Room" из HUD-дизайн-задачи,
# issue #178) — тап по ним попадает в UI, а не в плитку, и смоук-прогон
# вместо хода получает экран превью Комнаты Босса (проверено эмпирически на
# этой же сборке). Если игра продвинулась дальше этих пяти рядов и они
# перестали быть видимой частью поля — сдвинуть диапазон, ориентируясь на
# актуальный скриншот, а не тащить его выше вслепую.
declare -a ROWS_X=(540 540 540 540 540)
declare -a ROWS_Y=(1860 1600 1350 1120 900)

TILE_COUNT="${2:-5}"

echo "== Тапы без движения (adb input tap, тот самый сценарий бага) =="
if [ "$SCREENRECORD_OK" -eq 0 ]; then
  "$ADB" shell screencap -p "/sdcard/smoke_${STAMP}_00_before.png"
fi
for i in $(seq 0 $((TILE_COUNT - 1))); do
  x="${ROWS_X[$i]}"
  y="${ROWS_Y[$i]}"
  echo "  ход $((i + 1))/$TILE_COUNT: tap $x $y"
  "$ADB" shell input tap "$x" "$y"
  sleep 0.5
  if [ "$SCREENRECORD_OK" -eq 0 ]; then
    "$ADB" shell screencap -p "/sdcard/smoke_${STAMP}_$(printf '%02d' $((i + 1))).png"
  fi
done

echo "== Снятие лога =="
"$ADB" logcat -d -s Unity > "$LOG_HOST_PATH"

if [ "$SCREENRECORD_OK" -eq 1 ]; then
  echo "== Остановка screenrecord =="
  kill -INT "$RECORD_PID" 2>/dev/null || true
  wait "$RECORD_PID" 2>/dev/null || true
  sleep 1 # screenrecord дописывает файл после сигнала — не дёргать pull раньше времени
  echo "== Снятие видео =="
  "$ADB" pull "$VIDEO_DEVICE_PATH" "$VIDEO_HOST_PATH"
  "$ADB" shell rm -f "$VIDEO_DEVICE_PATH"
else
  echo "== Снятие скриншотов (фолбэк вместо видео) =="
  mkdir -p "$OUT_DIR/smoke_${STAMP}_screens"
  "$ADB" pull "/sdcard/smoke_${STAMP}_00_before.png" "$OUT_DIR/smoke_${STAMP}_screens/" 2>/dev/null || true
  for i in $(seq 1 "$TILE_COUNT"); do
    "$ADB" pull "/sdcard/smoke_${STAMP}_$(printf '%02d' "$i").png" "$OUT_DIR/smoke_${STAMP}_screens/" 2>/dev/null || true
  done
  "$ADB" shell rm -f "/sdcard/smoke_${STAMP}_"*.png
  VIDEO_HOST_PATH="$OUT_DIR/smoke_${STAMP}_screens/ (screenrecord недоступен на устройстве — набор скриншотов до/после каждого тапа)"
fi

echo "== Проверка на исключения в логе =="
# Не голое grep -i "error" — среди прочего ловит легитимные строки вроде
# "GL_KHR_no_error" (имя OpenGL-расширения) и даёт ложный сигнал тревоги.
# Логкэт-формат: "дата время pid tid УРОВЕНЬ tag: сообщение" — берём строки
# именно с уровнем E(rror)/F(atal), плюс отдельно "Exception" (C#-исключения
# почти всегда так называются, регистр важен) — и глушим уже известный
# безобидный ClassNotFoundException про AssetPackManager (see BuildScript.cs
# doc — Play Core, не используется, не влияет на игру).
if grep -E '^\S+ \S+ +[0-9]+ +[0-9]+ [EF] |Exception|FATAL' "$LOG_HOST_PATH" | grep -v "ClassNotFoundException.*AssetPackManager"; then
  echo "!! Найдены строки с ошибками/исключениями выше — проверить вручную." >&2
else
  echo "Лог чист (кроме известного ClassNotFoundException AssetPackManager — не связано с игрой)."
fi

echo
echo "Готово. Видео: $VIDEO_HOST_PATH"
echo "        Лог:   $LOG_HOST_PATH"
