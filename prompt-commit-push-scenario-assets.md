# Скачать Scenario-ассеты, закоммитить и запушить

## Контекст

В Scenario (сервис генерации арта, подключён по MCP) сгенерировано 30
приоритетных ассетов в стиле Duolingo/Mimo (flat vector) — иконки валют,
текстуры тайлов/ловушек, иконки артефактов, карты Знамений, иконка
приложения. Потрачено 44 из 50 выделенных CU.

В репозитории уже лежит всё нужное для скачивания:
- `scripts/download-scenario-assets.sh` — bash-скрипт, раскладывает файлы
  по правильным путям под `Assets/Art/`.
- `scripts/scenario_downloads.tsv` — список `относительный_путь <TAB>
  подписанный_URL` на все 30 файлов.

**Важно:** подписанные ссылки в TSV истекают ~через 24ч с момента
генерации (сгенерированы 2026-08-21/22). Если часть ссылок к моменту
запуска уже протухла — curl вернёт ошибку по этим файлам, это нормально,
не пытайся их угадать/перегенерировать (у тебя нет доступа к Scenario
MCP) — просто зафиксируй в отчёте, какие файлы не скачались.

## Задача

1. Из корня репозитория выполни:
   ```
   bash scripts/download-scenario-assets.sh
   ```
   Скрипт сам создаст `Assets/Art/...` и покажет OK/ОШИБКА по каждому из
   30 файлов + итоговую сводку.

2. Проверь, что каждый скачанный файл — валидный PNG, а не 0 байт и не
   HTML-страница с ошибкой (бывает, если ссылка уже протухла):
   ```
   file Assets/Art/**/*.png
   ```
   Любой файл, который не `PNG image data`, — удали и внеси в отчёт как
   не скачавшийся.

3. Ожидаемое дерево (30 файлов):
   ```
   Assets/Art/Icons/Currencies/currency-{mana-crystal,coin,key,crystal}.png
   Assets/Art/Tiles/tile-{fresh,half-decayed,about-to-decay,destroyed,start,
     blocked,pit,lava,timed-trap-idle,timed-trap-active,explosive-trigger,
     explosive-explosion,gate-closed,gate-open}.png
   Assets/Art/Icons/Artifacts/{amulet-trap-immunity,amulet-second-chance,
     talisman-mana-every-third-tile,talisman-double-keys,
     talisman-double-mana}.png
   Assets/Art/Cards/Omens/omen-{fragile-vault,hunting-path,stingy-altar,
     hungry-boss,blind-descent,rich-vein}.png
   Assets/Art/AppIcon/app-icon.png
   ```

4. Дай Unity импортировать новые PNG (сгенерировать `.meta` файлы) —
   если Unity Editor открыт/фоновый импорт прошёл, `.meta` появятся сами.
   Если нет — предупреди в отчёте, не проставляй Import Settings руками,
   это не входит в эту задачу (см. ниже про интеграцию).

5. Изображения могут содержать едва заметный водяной знак Scenario
   (параметр `wm=true` в URL, платный аккаунт его убирает) — это
   известное ограничение текущей генерации, не баг, не нужно чинить.

6. **Не коммить** `scripts/scenario_downloads.tsv` — в нём подписанные
   URL с токенами доступа, которые всё равно протухнут и не должны быть
   в истории репозитория. Если `.gitignore` его ещё не исключает —
   добавь туда `scripts/scenario_downloads.tsv`.
   `scripts/download-scenario-assets.sh` закоммитить можно — это
   переиспользуемый инструмент без секретов.

7. Атомарный коммит: только `Assets/Art/**` (+ `.meta`) +
   `scripts/download-scenario-assets.sh` + `.gitignore` (если менял).
   Conventional commit style, например:
   ```
   feat(art): add first batch of Scenario-generated art assets
   ```
   **Никакой самоатрибуции/подписи агента в коммите** — ни "Generated
   with...", ни "Co-Authored-By: Claude...", ни аналогов (см.
   `docs/rules/git-workflow.md`).

8. Новая ветка, не мастер/main:
   ```
   git checkout -b feat/scenario-art-assets-batch1
   git add Assets/Art scripts/download-scenario-assets.sh .gitignore
   git commit -m "feat(art): add first batch of Scenario-generated art assets"
   git push -u origin feat/scenario-art-assets-batch1
   ```
   PR не открывай и не мерджи сам — решение по мержу за мной.

9. Пришли отчёт: сколько файлов закоммичено, какие (если есть) не
   скачались, имя ветки, ссылка на diff/PR если GitHub CLI сам её
   предложит при пуше.

## Вне скоупа этой задачи

Это только загрузка сырых файлов в репозиторий. Визуальная интеграция —
замена цветов `TileDebugColor`/`TunnelDebugVisual` на текстуры — отдельная
задача, описана в `prompt-scenario-art-assets.md` (раздел Part B). Не
делай её заодно, если явно не попрошу отдельно.
