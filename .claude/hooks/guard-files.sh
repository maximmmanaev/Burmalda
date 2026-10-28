#!/usr/bin/env bash
# PreToolUse-хук (matcher: Edit|Write|NotebookEdit). Блокирует правки защищённых путей:
#   docs/raw/**, *.unity, *.prefab  (правило «только вручную»,
#   см. docs/rules/forbidden-actions.md). Сменится правило — правь только этот файл.
# Код 2 = блокировка. Сбой разбора или нет пути -> тоже 2.

GUARD_FALLBACK_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
export GUARD_FALLBACK_ROOT
PY_SRC=$(cat <<'PY'
import json, os, sys

def block(msg):
    sys.stderr.write("ЗАБЛОКИРОВАНО guard-files: " + msg + "\n")
    sys.exit(2)

# ---- правила (менять здесь) ----
PROTECTED_DIRS = ["docs/raw"]          # относительно корня проекта
PROTECTED_EXT = (".unity", ".prefab")  # где бы файл ни лежал
# --------------------------------

try:
    data = json.load(sys.stdin)
    ti = data["tool_input"]
    path = ti.get("file_path") or ti.get("notebook_path")
    if not isinstance(path, str) or not path:
        raise KeyError("нет tool_input.file_path / notebook_path")
except Exception as e:
    block("не удалось получить путь из входа хука (%s). Правка не выполнена." % e)

project = os.path.realpath(os.environ.get("CLAUDE_PROJECT_DIR")
                           or os.environ["GUARD_FALLBACK_ROOT"])
base = data.get("cwd") or project
if not os.path.isabs(path):
    path = os.path.join(base, path)
real = os.path.realpath(path)  # снимает `..` и симлинки

for cand in {os.path.normpath(path), real}:
    low = cand.lower()
    if low.endswith(PROTECTED_EXT):
        block("%s — сцена/префаб Unity, YAML ломается при автоправках. Такие изменения "
              "делает владелец вручную в редакторе. Опиши нужную правку словами." % cand)
    rel = os.path.relpath(low, project.lower())
    for d in PROTECTED_DIRS:
        if rel == d or rel.startswith(d + os.sep):
            block("%s внутри %s/ — вводные владельца, агенту запрещено их менять. "
                  "Если нужны правки, предложи их текстом в ответе." % (cand, d))
sys.exit(0)
PY
)
exec python3 -I -c "$PY_SRC"
