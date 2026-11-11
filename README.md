# FastForwarder — управление скоростью симуляции для shapez 2

Мод для **shapez 2 1.2.0-rc3** (Mono, macOS): панель в правом верхнем углу с пятью
скоростями симуляции. Порт мода **Fast Forwarder 1.0.6** (BepInEx) от **yujis**
на нативный загрузчик модов shapez 2.

```
[ II ] [ > ] [ >> ] [ >>> ] [ >| ]
  0x    1x    5x     10x     25x
```

- Активная скорость подсвечивается оранжевым.
- Пауза синхронизирована со штатной паузой игры: если игра поставила паузу,
  подсветка переключится на `II`.
- При старте нового игрового сеанса скорость сбрасывается на `1x`.
- При выходе в меню панель скрывается.
- Мод не меняет сохранения и не требует BepInEx.

## Установка

Мод ставится в папку модов игры (Steam Workshop не нужен):

```
~/Library/Application Support/com.tobspr.shapez-2/mods/FastForwarder/
├── manifest.json
├── FastForwarder.dll
├── FastForwarder.pdb   (необязательно)
└── README.md
```

Сборка сразу пишет файлы в эту папку, так что достаточно:

```bash
./install.sh
```

Проверка после перезапуска игры:

```bash
grep -i FastForwarder "$HOME/Library/Logs/tobspr Games/shapez 2/Player.log"
```

Ожидаемые строки:

```
[FastForwarder] Fast Forwarder loaded.
[FastForwarder] UI created.
[FastForwarder] SimulationSpeedManager hooked.
```

## Сборка

Нужен .NET SDK 8 (используется локальная установка в `~/.dotnet`):

```bash
dotnet --list-sdks        # 8.0.427 [...]
./build.sh                # dotnet build -c Release → mods/FastForwarder
```

Пути можно переопределить:

```bash
ShapezPath="/Applications/shapez 2.app/Contents/Resources/Data/Managed" \
ModsDir="$HOME/Library/Application Support/com.tobspr.shapez-2/mods" \
./build.sh
```

Готовый архив для переноса: `dist/FastForwarder-1.2.zip` (внутри папка `FastForwarder`).

## Как это устроено

- Точка входа — `Shapez2Mods.FastForwarder.FastForwarderMod : Game.Core.Modding.IMod`.
  Игра сама создаёт один экземпляр через DI и передаёт `Core.Logging.ILogger`.
- UI создаётся на следующем рендер-кадре (`Application.onBeforeRender`), чтобы
  гарантированно попасть в главный поток Unity.
- Каждый `Update` панель проверяет `StaticGameCoreAccessor.G`: пока сеанса нет —
  панель скрыта, при старте сеанса берётся `G.SimulationSpeed` и применяется `1x`.
- Панель нарисована через IMGUI (`OnGUI`), под ней — невидимая uGUI-картинка,
  которая перехватывает клики, чтобы они не уходили в игру.

Подробные заметки: [docs/NOTES.md](docs/NOTES.md).

## Ограничения и заметки

| Что | Почему |
|---|---|
| Только shapez 2 1.2.x | Привязка к `SimulationSpeedManager` и `StaticGameCoreAccessor.G`; после обновления игры нужно пересобрать и проверить API. |
| `DisablesAchievements: false` | Достижения не блокируются, но ускорение времени может облегчить их получение. Поставь `true` в `manifest.json`, если хочешь честный режим. |
| IMGUI-панель | Просто и быстро, но внешний вид не нативный. При желании можно переписать на uGUI/TMP (см. NOTES). |
| Позиция `y=120` | Может пересечься с новым HUD игры. Меняется константой `PanelTop` в `FastForwarderUI.cs`. |

## Происхождение

- Оригинал: [Fast Forwarder 1.0.6](https://github.com/urakkaamyx/Shapez2Mods-TimeController) —
  форк TimeController от Urakka, автор yujis.
- Оригинальная BepInEx-сборка и её декомпиляция лежат в `reference/`.
- Порт на нативный загрузчик 1.2: этот репозиторий.

## Структура

```
src/                 исходники мода
docs/NOTES.md        инженерные заметки, API и чек-лист обновления
reference/           оригинальный мод 1.0.6 (DLL, README, декомпиляция, zip)
dist/                собранный архив для установки
manifest.json        манифест нативного мода
build.sh             сборка сразу в папку модов игры
install.sh           сборка + проверка установки
```
