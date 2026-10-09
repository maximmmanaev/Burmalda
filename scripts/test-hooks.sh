#!/usr/bin/env bash
# Тесты хуков-защитников (.claude/hooks): подаёт JSON на stdin, проверяет код выхода.
# Запрещённое -> 2, разрешённое -> 0. Код выхода скрипта: 0 если все тесты прошли.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GB="$ROOT/.claude/hooks/guard-bash.sh"
GF="$ROOT/.claude/hooks/guard-files.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Временный проект: docs/raw, симлинк на него, репозиторий на main и клон на feature.
PROJ="$TMP/proj"; mkdir -p "$PROJ/docs/raw" "$PROJ/Assets"
ln -s "$PROJ/docs/raw" "$PROJ/rawlink"
git -C "$PROJ" init -q -b main
git -C "$PROJ" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
FEAT="$TMP/feat"; git clone -q "$PROJ" "$FEAT" && git -C "$FEAT" switch -q -c feature/x

# Строки атрибуции собираем из частей, чтобы этот файл не срабатывал на собственные хуки.
CO="Co-Authored-""By: Claude <noreply@anthropic.com>"
GEN="Gener""ated with Claude Code"

pass=0; fail=0
check() { # имя ожидаемый_код фактический_код
  if [ "$2" = "$3" ]; then pass=$((pass+1)); else fail=$((fail+1)); echo "FAIL: $1 (ожидалось $2, получено $3)"; fi
}
bash_json() { python3 -c 'import json,sys;print(json.dumps({"tool_name":"Bash","cwd":sys.argv[2],"tool_input":{"command":sys.argv[1]}}))' "$1" "$2"; }
file_json() { python3 -c 'import json,sys;print(json.dumps({"tool_name":sys.argv[3],"tool_input":{sys.argv[1]:sys.argv[2]}}))' "$1" "$2" "$3"; }

tb() { # ожидаемый_код команда [cwd]
  local cwd="${3:-$FEAT}"
  bash_json "$2" "$cwd" | "$GB" >/dev/null 2>&1; check "bash: $2" "$1" "$?"
}
tf() { # ожидаемый_код поле путь [инструмент]
  file_json "$2" "$3" "${4:-Edit}" | CLAUDE_PROJECT_DIR="$PROJ" "$GF" >/dev/null 2>&1; check "file: $2=$3" "$1" "$?"
}

# --- guard-bash: запрещено ---
for c in 'git add -A' 'git add --all' 'git add .' 'git add -u' 'git add -Av' 'git add --update' \
         'git commit -a -m x' 'git commit -am "x"' 'git commit --all -m x' \
         'git push --force origin feature/x' 'git push -f origin feature/x' 'git push origin +feature/x' \
         'git push --force-with-lease' 'git push origin main' 'git push origin master' \
         'git push origin HEAD:main' 'git push origin feature/x:refs/heads/main' 'git push --all' \
         'git branch -D old' 'git branch -Df old' 'git reset --hard' 'git reset --hard HEAD~1' \
         'git -C /tmp add -A' 'cd x && git add -A' 'echo a; git add .' \
         'bash -c "git add -A"' \
         "git commit -m x -m \"$CO\"" \
         "gh pr create --title t --body \"$GEN\"" \
         "git commit -m \"\$(cat <<'EOT'
fix

$CO
EOT
)\""; do
  tb 2 "$c"
done
tb 2 'git push' "$PROJ"            # текущая ветка main, ветка не указана
tb 2 'git push origin' "$PROJ"

# --- guard-bash: разрешено ---
for c in 'git add AGENTS.md' 'git add -- scripts/check.sh docs/rules/ci.md' 'git commit -m "fix: x"' \
         'git push origin feature/x' 'git push -u origin feature/x' 'git push' \
         'git branch -d old' 'git status --short' 'git reset --soft HEAD~1' 'git reset HEAD file' \
         'git commit -m "docs: про git add . и git push --force в тексте"' \
         "git commit -m \"\$(cat <<'EOT'
fix: x

git add -A не использовать
EOT
)\"" \
         'gh pr create --title t --body "Closes #1"' 'ls -la'; do
  tb 0 "$c"
done
tb 0 'git push origin feature/x' "$PROJ"   # с main-ветки, но явная рабочая ветка

# --- guard-bash: сбои -> блокировка ---
printf '' | "$GB" >/dev/null 2>&1; check "bash: пустой stdin" 2 "$?"
printf 'не json' | "$GB" >/dev/null 2>&1; check "bash: мусор в stdin" 2 "$?"
printf '{"tool_input":{}}' | "$GB" >/dev/null 2>&1; check "bash: нет command" 2 "$?"
printf '{"tool_input":{"command":"git commit -m \\"x"}}' | "$GB" >/dev/null 2>&1; check "bash: битые кавычки" 2 "$?"

# --- guard-files: запрещено ---
tf 2 file_path "$PROJ/docs/raw/PRD.md"
tf 2 file_path "$PROJ/docs/raw/sub/x.md" Write
tf 2 file_path "$PROJ/docs/raw/../raw/x.md"
tf 2 file_path "$PROJ/docs/wiki/../raw/x.md"
tf 2 file_path "$PROJ/rawlink/x.md"                  # симлинк на docs/raw
tf 2 file_path "$PROJ/DOCS/RAW/x.md"                 # регистр (macOS)
tf 2 file_path "$PROJ/Assets/Scenes/Main.unity"
tf 2 file_path "$PROJ/Assets/Prefabs/Player.prefab" Write
tf 2 file_path "/elsewhere/Some.UNITY"
tf 2 notebook_path "$PROJ/docs/raw/n.ipynb" NotebookEdit
tf 2 notebook_path "$PROJ/Assets/x.prefab" NotebookEdit
printf '' | CLAUDE_PROJECT_DIR="$PROJ" "$GF" >/dev/null 2>&1; check "files: пустой stdin" 2 "$?"
printf '{"tool_input":{"content":"x"}}' | CLAUDE_PROJECT_DIR="$PROJ" "$GF" >/dev/null 2>&1; check "files: нет пути" 2 "$?"
printf 'мусор' | CLAUDE_PROJECT_DIR="$PROJ" "$GF" >/dev/null 2>&1; check "files: мусор в stdin" 2 "$?"

# --- guard-files: разрешено ---
tf 0 file_path "$PROJ/docs/wiki/changelog.md"
tf 0 file_path "$PROJ/Assets/Scripts/X.cs" Write
tf 0 file_path "$PROJ/docs/rawfoo/x.md"              # похожее имя, не docs/raw
tf 0 file_path "$PROJ/Assets/x.unity.meta"
tf 0 notebook_path "$PROJ/notes/n.ipynb" NotebookEdit

total=$((pass+fail))
echo "тестов: $total, прошло: $pass, упало: $fail"
[ "$fail" -eq 0 ]
