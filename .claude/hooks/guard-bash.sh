#!/usr/bin/env bash
# PreToolUse-хук (matcher: Bash). Блокирует опасные git/gh-команды и атрибуцию агента.
# Контракт: JSON со stdin; код 2 = блокировка, stderr уходит агенту как причина.
# Сбой разбора или нет tool_input.command -> тоже 2 (закрыто, а не пропуск).
# Правила — проект: docs/rules/forbidden-actions.md, docs/rules/git-workflow.md.

PY_SRC=$(cat <<'PY'
import json, os, re, shlex, subprocess, sys

def block(msg):
    sys.stderr.write("ЗАБЛОКИРОВАНО guard-bash: " + msg + "\n")
    sys.exit(2)

try:
    data = json.load(sys.stdin)
    cmd = data["tool_input"]["command"]
    if not isinstance(cmd, str) or not cmd.strip():
        raise ValueError("пустая команда")
except Exception as e:
    block("не удалось разобрать вход хука (%s). Команда не выполнена." % e)

cwd = data.get("cwd") or os.environ.get("CLAUDE_PROJECT_DIR") or "."
PROTECTED = ("main", "master")
ATTRIBUTION = re.compile(
    r"co-authored-by:\s*claude|generated with|noreply@anthropic\.com", re.I)

# Тела heredoc не разбираем как команды (там сообщение коммита).
def strip_heredocs(s):
    return re.sub(r"<<-?\s*(['\"]?)(\w+)\1[^\n]*\n.*?\n[ \t]*\2[ \t]*(?=\n|$)",
                  "", s, flags=re.S)

def segments(s):
    lex = shlex.shlex(s, posix=True, punctuation_chars=";&|\n")
    lex.whitespace_split = True
    lex.whitespace = " \t\r"
    seg = []
    for tok in lex:
        if tok and all(c in ";&|\n" for c in tok):
            if seg:
                yield seg
            seg = []
        else:
            seg.append(tok)
    if seg:
        yield seg

def current_branch():
    try:
        return subprocess.run(["git", "-C", cwd, "rev-parse", "--abbrev-ref", "HEAD"],
                              capture_output=True, text=True, timeout=10).stdout.strip()
    except Exception:
        return ""

def short_cluster(tok):
    return re.fullmatch(r"-[A-Za-z]+", tok) is not None

def check_git(args):
    # пропускаем глобальные опции git
    i = 0
    while i < len(args):
        a = args[i]
        if a in ("-C", "-c", "--git-dir", "--work-tree", "--namespace", "--exec-path"):
            i += 2
        elif a.startswith("-"):
            i += 1
        else:
            break
    if i >= len(args):
        return
    sub, rest = args[i], args[i + 1:]
    opts = [a for a in rest if a.startswith("-")]

    if sub == "add":
        for a in rest:
            if a == "--":
                continue
            if a in ("--all", "--update", ".", "./", "*") or a.startswith(":/") or \
               a.startswith(":(top)") or \
               (short_cluster(a) and ("A" in a or "u" in a)):
                block("`git add %s` добавляет всё подряд (могут уехать чужие файлы). "
                      "Добавляй только явные пути: `git add путь/к/файлу`, "
                      "перед коммитом смотри `git status --short`." % a)
    elif sub == "commit":
        for a in rest:
            if a == "--":
                break
            if a == "--all" or (short_cluster(a) and "a" in a):
                block("`git commit -a/-am` коммитит все изменённые файлы. "
                      "Сначала `git add` явных путей, затем `git commit -m \"...\"`.")
    elif sub == "push":
        pos = [a for a in rest if not a.startswith("-") and not a.startswith("+")]
        for a in rest:
            if a in ("--force", "--force-with-lease", "--force-if-includes", "--mirror") \
               or a.startswith(("--force=", "--force-with-lease=", "--force-if-includes=")) \
               or (short_cluster(a) and "f" in a) or a.startswith("+"):
                block("force-push запрещён. Ветку при необходимости обновляй новым "
                      "коммитом/merge; переписывание истории согласуй с владельцем.")
        for a in rest:
            if a == "--delete" or (short_cluster(a) and "d" in a):
                block("`git push --delete` удаляет ветку на удалённом репозитории. "
                      "Удаление веток согласуй с владельцем (он делает это на GitHub).")
        if "--all" in rest:
            block("`git push --all` затрагивает и main. Пушь одну ветку явно: "
                  "`git push origin <ветка>`.")
        refspecs = pos[1:]
        for r in refspecs:
            if r.startswith(":"):
                block("`git push origin :ветка` удаляет ветку на удалённом репозитории. "
                      "Удаление веток согласуй с владельцем.")
            dst = r.split(":")[-1]
            dst = re.sub(r"^refs/heads/", "", dst)
            if dst in PROTECTED:
                block("push в %s запрещён: в main/master не пушим, мерж делает владелец "
                      "через PR. Пушь рабочую ветку: `git push origin <ветка>`." % dst)
        if len(refspecs) == 0 and current_branch() in PROTECTED:
            block("текущая ветка %s: push без явной ветки ушёл бы в неё. Создай рабочую "
                  "ветку (`git switch -c ...`) и пушь её." % current_branch())
    elif sub == "branch":
        for a in rest:
            if a == "--":
                break
            if short_cluster(a) and ("D" in a or ("d" in a and "f" in a)):
                block("`git branch -D` удаляет ветку без проверки слияния. Используй "
                      "`git branch -d` (откажет, если не смержена) или спроси владельца.")
        if "--delete" in rest and ("--force" in rest or any(short_cluster(a) and "f" in a for a in rest)):
            block("принудительное удаление ветки запрещено; используй `git branch -d`.")
    elif sub == "clean":
        dry = any(a == "--dry-run" or (short_cluster(a) and "n" in a) for a in rest)
        if not dry:
            block("`git clean` безвозвратно удаляет неотслеживаемые файлы (в т.ч. чужие "
                  "рабочие файлы владельца). Посмотри, что будет удалено: `git clean -n`; "
                  "удаляй конкретный файл по явному пути или спроси владельца.")
    elif sub == "checkout":
        if any(a in (".", "./", ":/") for a in rest):
            block("`git checkout .` / `checkout -- .` затирает все правки в рабочей "
                  "директории. Восстанови конкретный файл по явному пути "
                  "(`git restore путь/к/файлу`) или спроси владельца.")
    elif sub == "restore":
        if any(a in (".", "./", ":/", "*") for a in rest):
            block("`git restore .` (в т.ч. с --source) затирает все правки. Восстанови "
                  "конкретный файл по явному пути (`git restore путь/к/файлу`) или "
                  "спроси владельца.")
    elif sub == "stash":
        pos = [a for a in rest if not a.startswith("-")]
        if pos and pos[0] in ("drop", "clear"):
            block("`git stash %s` безвозвратно удаляет сохранённые правки. Посмотри "
                  "`git stash list`; удаление записей согласуй с владельцем." % pos[0])
    elif sub == "reset":
        if "--hard" in rest:
            block("`git reset --hard` необратимо теряет изменения. Используй `git stash` "
                  "или `git restore <путь>` для конкретных файлов; иначе спроси владельца.")

OWNER_ONLY = ("Мерж и изменение защиты веток делает только владелец вручную. "
              "Агент заканчивает работу открытым PR с зелёным CI и сообщает владельцу.")

def check_gh(args):
    # пропускаем глобальные опции gh (-R/--repo, --hostname)
    i = 0
    while i < len(args):
        a = args[i]
        if a in ("-R", "--repo", "--hostname"):
            i += 2
        elif a.startswith("-"):
            i += 1
        else:
            break
    if i >= len(args):
        return
    sub, rest = args[i], args[i + 1:]
    if sub == "pr":
        pos = [a for a in rest if not a.startswith("-")]
        if pos and pos[0] == "merge":
            block("`gh pr merge` (с любыми флагами, включая --admin и --auto) запрещён. " + OWNER_ONLY)
    elif sub == "api":
        method = None
        implicit_post = False
        endpoint = None
        j = 0
        while j < len(rest):
            a = rest[j]
            if a in ("-X", "--method") and j + 1 < len(rest):
                method = rest[j + 1].upper(); j += 2; continue
            if a.startswith("--method="):
                method = a.split("=", 1)[1].upper()
            elif re.fullmatch(r"-X[A-Za-z]+", a):
                method = a[2:].upper()
            elif a in ("-f", "-F", "--field", "--raw-field", "--input"):
                implicit_post = True; j += 2; continue
            elif a in ("-H", "--header", "-q", "--jq", "-t", "--template", "--cache", "--hostname"):
                j += 2; continue
            elif not a.startswith("-") and endpoint is None:
                endpoint = a
            j += 1
        if endpoint is None:
            return
        ep = endpoint.lstrip("/")
        if "/merge" in ep:
            block("`gh api` к пути с /merge запрещён. " + OWNER_ONLY)
        if endpoint == "graphql" and re.search(r"mergePullRequest|enablePullRequestAutoMerge|enableAutoMerge",
                                              " ".join(rest)):
            block("GraphQL-мутация мержа запрещена. " + OWNER_ONLY)
        eff = method or ("POST" if implicit_post else "GET")
        if eff in ("PUT", "DELETE", "PATCH", "POST") and \
           re.search(r"(^|/)pulls(/|$)|/branches/.+/protection|(^|/)rulesets(/|$)", ep):
            block("`gh api -X %s %s` меняет PR или защиту веток. " % (eff, endpoint) + OWNER_ONLY)

def check_segment(seg, depth=0):
    # убираем префиксные присваивания VAR=val и обёртки env/command/time/sudo
    while seg and (re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*=.*", seg[0]) or
                   seg[0] in ("env", "command", "time", "exec", "nohup")):
        seg = seg[1:]
    if not seg:
        return
    prog = os.path.basename(seg[0])
    if prog in ("bash", "sh", "zsh") and depth < 3:
        for j, a in enumerate(seg):
            if a.startswith("-") and "c" in a and not a.startswith("--") and j + 1 < len(seg):
                inner = strip_heredocs(seg[j + 1])
                for s2 in segments(inner):
                    check_segment(s2, depth + 1)
                return
    if prog == "git":
        check_git(seg[1:])
    elif prog == "gh":
        check_gh(seg[1:])

try:
    for s in segments(strip_heredocs(cmd)):
        check_segment(s)
except SystemExit:
    raise
except Exception as e:
    block("не удалось разобрать команду (%s). Упрости команду (без сложных кавычек)." % e)

# Атрибуция агента в коммите/PR (по сырому тексту, включая heredoc).
if re.search(r"\bgit\b.*\bcommit\b|\bgh\s+pr\s+(create|edit|comment)\b", cmd, re.S) \
   and ATTRIBUTION.search(cmd):
    block("в сообщении коммита/PR найдена атрибуция агента (Co-Authored-By: Claude / "
          "Generated with ...). В этом проекте она запрещена (AGENTS.md, "
          "docs/rules/git-workflow.md). Убери эти строки и повтори команду.")
sys.exit(0)
PY
)
exec python3 -I -c "$PY_SRC"
