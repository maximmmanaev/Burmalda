# CI — Unity Tests

Workflow: [`.github/workflows/unity-tests.yml`](../../.github/workflows/unity-tests.yml).

## Что делает

Триггерится на каждый `pull_request`. Шаги:

1. **Checkout** репозитория с `lfs: true` (Unity-проекты обычно хранят бинарные ассеты через Git LFS).
2. **Проверка наличия Unity-проекта** — ищет файл `ProjectSettings/ProjectVersion.txt` в корне репозитория. Пока в репозитории нет Unity-проекта (см. [roadmap](../wiki/roadmap.md), Спринт 1), этого файла не существует.
3. **Запуск тестов** — если файл найден, выполняется [`game-ci/unity-test-runner@v4`](https://game.ci/docs/github/test-runner) с `testMode: all`, прогоняющий Unity Test Framework.

Если Unity-проекта ещё нет, шаг прогона тестов **пропускается** (условие `if` по результату проверки), а не падает — джоба остаётся зелёной.

## UNITY_LICENSE

`game-ci/unity-test-runner` для запуска Unity в headless-режиме на CI требует лицензию, переданную через секрет репозитория `UNITY_LICENSE`.

Секрет **пока не создан** — это ожидаемо, пока в репозитории нет Unity-проекта. Перед тем как Unity-проект появится (см. Спринт 1 в [roadmap](../wiki/roadmap.md)), нужно вручную:

1. Пройти процесс активации Unity Personal license для game-ci — см. официальную инструкцию: https://game.ci/docs/github/activation
2. Добавить полученный `.ulf`-файл лицензии как секрет `UNITY_LICENSE` в настройках репозитория (Settings → Secrets and variables → Actions).

Это действие требует ручного шага пользователя (активация лицензии через email/логин Unity ID) и не может быть автоматизировано агентом.
