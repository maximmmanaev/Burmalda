# Burmalda — карта проекта для агента

**Никогда не подписывай коммиты и PR атрибуцией себе** — ни "Generated with
Claude Code", ни "Co-Authored-By: Claude...", ни аналогов. См.
[docs/rules/git-workflow.md](docs/rules/git-workflow.md).

- НИКОГДА не пушить напрямую в master/main.
- Мерж PR делает только пользователь вручную на GitHub.
- Агент **всегда** коммитит результат работы в свою ветку по ходу дела, не дожидаясь одобрения владельца продукта.
- НЕ трогать .unity и .prefab файлы автономно — это YAML-сцены, ломаются при автоправках. Такие изменения — только вручную.
- Баланс (кривые множителя, вероятности d20, экономика Ритуала) не менять без явного запроса — это требует плейтеста, не только кода.
- `docs/raw/`: агенту запрещено редактировать, удалять или переписывать что-либо в этой папке.
- Между красным и зелёным запускай целевые тесты: scripts/check.sh targeted <фильтр>. Перед каждым коммитом запускай полный EditMode: scripts/check.sh full (около 20 секунд); коммитить только при коде возврата 0. См. `docs/rules/implementation-workflow.md`.

## Когда что читать

- Начало задачи — `docs/rules/token-economy.md`; расход контекста — `docs/rules/agent-efficiency.md`.
- Правка кода, баг — `docs/rules/implementation-workflow.md`, `docs/rules/code-style.md`.
- Термины, фичи — `docs/rules/terminology.md`, нужные разделы актуального PRD (самый свежий файл в docs/raw; какой версии — написано в `docs/wiki/roadmap.md`).
- Баланс, валюты, артефакты, .unity/.prefab, названия и тексты — `docs/rules/forbidden-actions.md`.
- Конец задачи, wiki, changelog, тесты и CI — `docs/rules/documentation.md`, `docs/rules/ci.md`.
- Локальный прогон: scripts/check.sh, см. `docs/rules/ci.md`.
- PR, конфликты, сборка спринта — `docs/rules/git-workflow.md`; карта wiki — `docs/wiki/index.md`.
