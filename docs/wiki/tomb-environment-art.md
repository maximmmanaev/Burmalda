# Модульный арт гробницы

Набор пола и стен построен по приложенному к задаче референсу
`burmalda_tomb_reference.png`: крупная квадратная сетка плит, тёмные швы,
красно-коричневый песчаник, выраженные фаски, сколы и крупные читаемые
трещины. Оранжевый свет референса используется как направление освещения и
не запечён в Base Color.

## Состав и размеры

Все размеры заданы в метрах Unity:

- `Floor_Intact` — целая плита 1×1 м;
- `Floor_Worn` — потёртая плита 1×1 м со сколотыми углами;
- `Floor_Cracked` — разрушенная плита 1×1 м из разошедшихся фрагментов;
- `Wall_Straight` — прямой сегмент 1×0,18×1,1 м из крупных блоков;
- `Wall_Corner` — внешний L-образный угол 1×1×1,1 м.

Pivot плит находится по центру верхней поверхности, геометрия уходит вниз на
0,12 м. Pivot стен находится внизу. Соседние модули имеют точные границы
метровой сетки; фаски и межблочные углубления дают видимый шов без щели между
ячейками.

## Исходники и генерация

- Blender-скрипт: `Tools/Blender/Tomb/generate_tomb_modular.py`;
- редактируемый исходник: `Tools/Blender/Tomb/burmalda-tomb-modular.blend`;
- Unity FBX: `Assets/Art/Tomb/Models/burmalda-tomb-modular.fbx`;
- общие Base Color, Normal и URP Mask Map: `Assets/Art/Tomb/Textures/`, 256×256;
- Unity-материал: `Assets/Art/Tomb/Materials/TombSandstone.mat`;
- runtime-каталог: `Assets/Resources/Art/TombEnvironmentCatalog.asset`.

Воспроизводимая генерация:

```sh
blender --background --python Tools/Blender/Tomb/generate_tomb_modular.py
```

После генерации Unity import settings, материал и каталог обновляются командой:

```sh
/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -executeMethod Burmalda.EditorTools.TombEnvironmentIntegrationSetup.Run \
  -quit
```

## Интеграция

`DebugVisuals.TunnelDebugVisual` загружает набор через существующий механизм
`Resources`, не создавая новую систему построения уровня. Целая и потёртая
плиты выбираются стабильным хэшем координаты, разрушенная геометрия включается
только после `Tile.IsDestroyed`. Непрерывный crack-overlay по
`Tile.DecayProgress01`, terminal collapse, `TileVisualMarker` и `BoxCollider`
сохранены. Стены создаются один раз на ряд существующим обработчиком
материализации и не имеют коллайдеров.

Сцены и prefab не изменяются. Для ручной проверки есть меню
`Burmalda → Art → Create Tomb Module Preview (Unsaved)`: оно создаёт во
временной текущей сцене соседние варианты плит, прямые стены и угол. Сцену
после проверки сохранять не требуется.

`TombEnvironmentAssetTests` проверяет импорт, ссылки, размеры, pivots, UV,
нормали, URP-материал, Android-лимиты, стыковку и runtime-коллайдеры.
