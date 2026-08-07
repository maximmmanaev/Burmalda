# Assets/Scripts — структура

Заготовка структуры C#-кода под архитектуру Burmalda. Папки пока пусты
(только `.gitkeep`, чтобы git их отслеживал) — реальные классы появятся
по мере выполнения соответствующих спринтов из [Roadmap](../../docs/wiki/roadmap.md).

Соответствие терминов PRD и C#-идентификаторов — в
[C#-глоссарии](../../docs/wiki/csharp-glossary.md).

## Папки → спринты

| Папка | Что здесь будет | Спринт |
|---|---|---|
| `Core/` | Общие утилиты, интерфейсы, используемые другими системами | — (инфраструктура) |
| `Movement/` | Grid-trace движение (тянешь палец по плитам) | [Спринт 2](https://github.com/maximmmanaev/Burmalda/milestone/2) |
| `Decay/` | Decay-система (обрушение плит позади игрока) | [Спринт 2](https://github.com/maximmmanaev/Burmalda/milestone/2) |
| `Traps/` | Статичные и динамические (мгновенные / подвижные с таймингом) ловушки | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
| `Currencies/` | `ManaCrystals`, `Keys`, `Coins`, `Crystals` | [Спринт 4](https://github.com/maximmmanaev/Burmalda/milestone/4) |
| `Artifacts/` | `Idol`, `Totem`, `Amulet`, `Talisman`, `Rune`, `Relic` и связанные `ArtifactCollection`/`ArtifactPool` | [Спринт 5](https://github.com/maximmmanaev/Burmalda/milestone/5) |
| `Altar/` | `Altar`, `Ritual`, `Chest` и подтипы сундуков, `SellArtifact` | [Спринт 6](https://github.com/maximmmanaev/Burmalda/milestone/6) |
| `Boss/` | Обязательный Босс (`Boss`): 2 Алтаря перед ним, автобой лучом энергии | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `D20/` | `D20Trial` и `D20Outcome` (Испытание Шахты) | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Camp/` | `Camp`, `CashOut`, `ReturnToCamp` | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Achievements/` | `Achievement` — условие → оповещение → разлок артефакта | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Monetization/` | `MonetizationOffer`, `ReviveOffer`, `ArtifactGachaPack` | [Спринт 11](https://github.com/maximmmanaev/Burmalda/milestone/11) |
| `DebugVisuals/` | **Debug-инфраструктура, не система из PRD.** Минимальный визуал сетки тоннеля (примитивы в рантайме, без .prefab) для ручного тестирования Core/Movement/Decay глазами | вне очереди спринтов ([issue #58](https://github.com/maximmmanaev/Burmalda/issues/58)) |

Активные способности Тотема (`TotemAbilityType`, раздел 12 PRD) и монетизационные
метрики (раздел 14) не выделены в отдельную папку — они реализуются внутри
`Artifacts/` (Тотем) и `Monetization/` соответственно. Рычаги (`Lever`, раздел 4.2
PRD) реализуются внутри `Traps/` — это не ловушка, но логически часть механики
навигации по опасным путям.
