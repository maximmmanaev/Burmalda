#!/usr/bin/env bash
# Скачивает сгенерированные в Scenario ассеты прямо в Assets/Art/ репозитория.
# Запускать ЛОКАЛЬНО (Terminal.app на Mac) — не через агента, не в песочнице.
# Причина: сетевой доступ агента заблокирован allowlist'ом до cdn.cloud.scenario.com,
# у твоего Mac такого ограничения нет.
#
# Использование:
#   cd Burmalda/scripts
#   bash download-scenario-assets.sh
#
# Подписанные ссылки в scenario_downloads.tsv истекают ~через 24ч с момента
# генерации (2026-08-21/22) — если скрипт выдаёт ошибки скачивания, ссылки
# протухли, нужно заново вызвать asset_download в Scenario MCP и перезаписать TSV.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TSV="$SCRIPT_DIR/scenario_downloads.tsv"
DEST_ROOT="$SCRIPT_DIR/../Assets/Art"

if [ ! -f "$TSV" ]; then
  echo "Не найден $TSV" >&2
  exit 1
fi

mkdir -p "$DEST_ROOT"

total=0
failed=0

while IFS=$'\t' read -r rel_path url; do
  [ -z "${rel_path// }" ] && continue
  [ -z "${url// }" ] && continue

  full_path="$DEST_ROOT/$rel_path"
  mkdir -p "$(dirname "$full_path")"

  total=$((total + 1))
  printf 'Скачиваю %-55s ' "$rel_path"

  if curl -sSL -f -o "$full_path" "$url"; then
    size=$(stat -f%z "$full_path" 2>/dev/null || stat -c%s "$full_path" 2>/dev/null || echo "?")
    echo "OK (${size} bytes)"
  else
    echo "ОШИБКА (ссылка протухла или сеть недоступна)"
    rm -f "$full_path"
    failed=$((failed + 1))
  fi
done < "$TSV"

echo ""
echo "Готово: $((total - failed)) из $total скачано в $DEST_ROOT"
if [ "$failed" -gt 0 ]; then
  echo "Не скачалось: $failed — см. ошибки выше. Скорее всего протухли подписанные ссылки."
  exit 1
fi
