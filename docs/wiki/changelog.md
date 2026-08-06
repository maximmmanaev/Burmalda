# Changelog

## 2026-08-06

- Организована трёхуровневая документация: `docs/raw/` (неизменяемые вводные пользователя), `docs/wiki/` (живая документация), `docs/rules/` (правила для агента, разбитые на отдельные файлы).
- Корневой `CLAUDE.md` сокращён до краткого файла со ссылкой на `docs/rules/index.md`.
- Добавлен PRD v4 (`docs/raw/BURMALDA_PRD_v4.md`) как неизменяемый источник истины.
- Разработка Burmalda разбита на 12 спринтов по 2 недели (от HTML-прототипа до релиза в сторы), для каждого создан [GitHub Milestone](https://github.com/maximmmanaev/Burmalda/milestones) и набор issues с метками `feature`/`balance`/`refactor`/`infra`. См. [Roadmap](roadmap.md).
- Добавлен CI-workflow `.github/workflows/unity-tests.yml` — прогон Unity Test Framework на каждый Pull Request через `game-ci/unity-test-runner@v4`. Пока в репозитории нет Unity-проекта, шаг тестов пропускается без падения джобы. См. [docs/rules/ci.md](../rules/ci.md).
