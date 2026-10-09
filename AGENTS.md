# Burmalda — карта проекта для агента

Этот файл — карта, а не энциклопедия: он только указывает, где что лежит.
Сами правила живут в файлах-источниках ниже и здесь не пересказываются.
Файл читают и Claude Code (через `@AGENTS.md` в `CLAUDE.md`), и Codex.

## Что за проект

Мобильная игра (iOS/Android), жанр greed survival / trace runner.
Движок: Unity, язык: C#. Код игры — в `Assets/`.

## Где что лежит

- `docs/rules/` — правила для агента (индекс: `docs/rules/index.md`).
- `docs/raw/` — неизменяемые вводные от владельца продукта, включая PRD.
- `docs/wiki/` — живая документация проекта (индекс: `docs/wiki/index.md`).
- `docs/design/` — дизайн-материалы.
- `.github/workflows/` — CI.

## Правила

Начни с `docs/rules/token-economy.md` — что читать точечно, а не целиком.
Полный список — в `docs/rules/index.md`:

- `docs/rules/terminology.md` — терминология.
- `docs/rules/git-workflow.md` — git-процесс.
- `docs/rules/code-style.md` — стиль кода.
- `docs/rules/implementation-workflow.md` — порядок реализации задачи
  (архитектура → тест → код).
- `docs/rules/agent-efficiency.md` — дисциплина правок и расход контекста.
- `docs/rules/token-economy.md` — экономия токенов на чтение.
- `docs/rules/forbidden-actions.md` — запрещённые и ограниченные действия.
- `docs/rules/documentation.md` — документация и changelog.
- `docs/rules/ci.md` — CI (Unity Tests).

## PRD

Актуальная версия — `docs/raw/BURMALDA_PRD_v9.md`. Статус версий и то, что
v4–v8 оставлены для истории, описаны в `docs/wiki/roadmap.md` (раздел
в начале файла). Читать только разделы, на которые ссылается задача
(см. `docs/rules/token-economy.md`).

## Wiki

Индекс — `docs/wiki/index.md`. Основные страницы:

- `docs/wiki/roadmap.md` — план по спринтам.
- `docs/wiki/traps.md` — спецификация ловушек.
- `docs/wiki/csharp-glossary.md` — соответствие терминов PRD именам в коде.
- `docs/wiki/changelog.md` — журнал изменений, только дописывается.
- `docs/wiki/task-brief-template.md` — шаблон постановки задачи.

Крупные страницы (changelog, roadmap, traps, csharp-glossary) целиком не
читать — как их читать, написано в `docs/rules/token-economy.md`.

## Тесты

Автоматический прогон — в CI: `.github/workflows/unity-tests.yml`, описание
в `docs/rules/ci.md`. Порядок «целевые тесты в итерациях, полный EditMode
один раз перед PR» — в `docs/rules/implementation-workflow.md`.
Команда локального запуска тестов: не задокументировано.

## Жёсткие ограничения

**Никогда не подписывай коммиты и PR атрибуцией себе** — ни "Generated with
Claude Code", ни "Co-Authored-By: Claude...", ни аналогов. См.
[docs/rules/git-workflow.md](docs/rules/git-workflow.md).

Остальные формулировки — в файлах-источниках, здесь только указатели:

1. Результат работы коммитить в свою ветку сразу, не оставлять рабочее
   дерево незакоммиченным — `docs/rules/git-workflow.md`.
2. Файлы `.unity` и `.prefab` автономно не трогать —
   `docs/rules/forbidden-actions.md`.
3. Баланс не менять без явного запроса; изменения валют и артефактов — со
   ссылкой на раздел PRD — `docs/rules/forbidden-actions.md`.
4. Названия, тексты и раскладки сегментов — за владельцем продукта —
   `docs/rules/forbidden-actions.md`.
5. Папку `docs/raw/` не редактировать — `docs/raw/README.md`.
6. После задачи обновить wiki и дописать запись в changelog (не длиннее
   10 строк) — `docs/rules/documentation.md`.
7. Changelog, roadmap, traps и csharp-glossary читать точечно —
   `docs/rules/token-economy.md`.
