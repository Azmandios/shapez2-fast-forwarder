# NOTES — FastForwarder для shapez 2 1.2

Рабочие заметки по порту мода с BepInEx на нативный загрузчик shapez 2.
Дополнять при следующих изменениях.

## Статус

- **2026-11-11:** порт собран, установлен, проверен пользователем в игре — работает.
- Игра: shapez 2 **1.2.0-rc3**, Unity **6000.3.19f1**, бэкенд **Mono** (не IL2CPP),
  macOS arm64 (universal binary, запускается нативно, не под Rosetta).
- Оригинал: **Fast Forwarder 1.0.6** (BepInEx) от yujis, форк TimeController от Urakka.

## Окружение

| Что | Значение |
|---|---|
| Игра | `/Applications/shapez 2.app` |
| Сборки игры | `.../Contents/Resources/Data/Managed` |
| Папка модов | `~/Library/Application Support/com.tobspr.shapez-2/mods` |
| Лог | `~/Library/Logs/tobspr Games/shapez 2/Player.log` |
| .NET SDK | 8.0.427, локально в `~/.dotnet` |
| Язык/таргет | C# 9, `netstandard2.1` |
| Декомпилятор | `ilspycmd 8.2.0.7535` в `~/.dotnet/tools` |

`ilspycmd` собран под .NET 6, поэтому запускать с роллфорвардом:

```bash
export DOTNET_ROOT=$HOME/.dotnet DOTNET_ROLL_FORWARD=Major
$HOME/.dotnet/tools/ilspycmd ...
```

Установка декомпилятора (если пропадёт):

```bash
$HOME/.dotnet/dotnet tool install -g ilspycmd --version 8.2.0.7535
```

## Что было в оригинале (1.0.6, BepInEx)

Классы:

- `Shapez2Mods.FastForwarder.FastForwarderPlugin : BaseUnityPlugin` — `[BepInPlugin]`,
  в `Awake()` делает `new Harmony(...).PatchAll()`.
- `Shapez2Mods.FastForwarder.Patches.GameStartPatches`:
  - postfix на конструкторы `GameStartOptionsStartNew` / `GameStartOptionsContinueExisting`
    (отслеживание `MenuMode`);
  - postfix на `GameSessionOrchestrator.Init` — инициализация мода.
- `Shapez2Mods.FastForwarder.UI.FastForwarderUI : MonoBehaviour` — IMGUI-панель,
  невидимый uGUI-блокер кликов, кнопки 0/1/5/10/25x.
- Скорость: `GameSessionOrchestrator.SimulationSpeed` (private) через `Traverse`,
  затем `SimulationSpeedManager.IsPaused` / `.Speed`.
- Дополнительно пытался поднять `SimulationConstants.MaxSimulatorIncrement` до 5 секунд.

Оригинальная DLL, README, декомпиляция и zip лежат в `reference/`.

## Почему оригинал не работает в 1.2

1. **BepInEx больше не используется.** В 1.2 нативный загрузчик:
   `mods/<Имя>/manifest.json` + сборки, среди типов которых ровно один реализует
   `Game.Core.Modding.IMod`. Просто положить DLL в `BepInEx/plugins` недостаточно.
2. **`GameSessionOrchestrator.Init` удалён.** Инициализация теперь в
   `GameSessionOrchestrator.Prepare(IGameStartOptions, GlobalsData, IGameData, IPlatform, IContent, ...)`
   (`async UniTask`). Старый Harmony-патч падал с
   `Could not find method for type GameSessionOrchestrator and name Init`.
3. **`Assembly-CSharp.dll` больше нет.** Код разнесён по `Game.*.dll` и `SPZGameAssembly.dll`.
   Многие типы (`SimulationSpeedManager`, `StaticGameCoreAccessor`, `GameStartOptions*`)
   находятся в `SPZGameAssembly.dll` в **глобальном** namespace.
4. Патчи конструкторов `GameStartOptions*` больше не нужны: сеанс можно отслеживать
   по `StaticGameCoreAccessor.G`.

## Что сделано при порте

- Точка входа переписана на `IMod`:

  ```csharp
  public sealed class FastForwarderMod : IMod
  {
      public FastForwarderMod(Core.Logging.ILogger logger) { ... }
      public void Dispose() { ... }
  }
  ```

- Harmony и все патчи **удалены** — они больше не нужны.
- Доступ к сессии: `StaticGameCoreAccessor.G` (`IGameSessionManagers`), поле/свойство
  `SimulationSpeed` даёт `SimulationSpeedManager`.
- UI создаётся не в конструкторе, а на `Application.onBeforeRender` — гарантия
  главного потока Unity (конструктор мода может вызываться в неудобный момент).
- `FastForwarderUI.Update()` опрашивает `StaticGameCoreAccessor.G`:
  - `null` → сеанса нет, панель скрыта;
  - новый объект сессии → берём `SimulationSpeed`, ставим `1x`;
  - каждый кадр синхронизируем подсветку с `SimulationSpeedManager.Speed`
    (чтобы работала штатная пауза игры).
- Панель и блокер кликов переживают смену сцен (`DontDestroyOnLoad`), блокер
  пересоздаётся, если его уничтожили.
- Убрано поднятие `SimulationConstants.MaxSimulatorIncrement`: в 1.2 `Tick()` и так
  обрабатывает «лишнее» время при `Speed > 1`
  (`ticks = min(accumulated, MaxSimulatorIncrement)`, `ticks2 = accumulated - ticks`).

## Карта API: было → стало

| Задача | 1.0.6 (BepInEx) | 1.2 (native) |
|---|---|---|
| Точка входа | `BaseUnityPlugin.Awake` | `IMod` + конструктор |
| Патчи | Harmony `PatchAll` | не нужны |
| Старт сеанса | postfix `GameSessionOrchestrator.Init` | `StaticGameCoreAccessor.G != null` |
| Конец сеанса | не отслеживался | `StaticGameCoreAccessor.G == null` (ставится в `Dispose`) |
| Доступ к скорости | `Traverse` по private-полю `SimulationSpeed` | `G.SimulationSpeed` (public) |
| Пауза | `IsPaused` | `IsPaused` (без изменений) |
| Скорость | `Speed` (float) | `Speed` (float, без изменений) |
| `GameStartOptions*.MenuMode` | патчи конструкторов | не требуется |

### Точные типы (проверено в 1.2.0-rc3)

```csharp
// Game.Core.Modding.dll, namespace Game.Core.Modding
public interface IMod : IDisposable { }

// SPZGameAssembly.dll, глобальный namespace
[Obsolete] public static class StaticGameCoreAccessor
{
    public static IGameSessionManagers G { get; }
    public static bool HasInstance();
}

[Obsolete] public interface IGameSessionManagers
{
    SimulationSpeedManager SimulationSpeed { get; }
    // ... остальные менеджеры
}

public class SimulationSpeedManager : ISimulationSpeedReader, IDisposable
{
    public float Speed { get; set; }     // setter: Shader.SetGlobalFloat + событие
    public bool IsPaused { get; set; }   // true → Speed = 0, false → Speed = 1
    public bool DoStep { get; set; }
    public IEvent<float> OnSpeedChanged { get; }
    public void PerformTick(Ticks dt);
}
```

- `G` присваивается в конце `GameSessionOrchestrator.Prepare` и сбрасывается в `null`
  в начале `GameSessionOrchestrator.Dispose`.
- `GameSessionOrchestrator.Tick(float)` каждый кадр делает
  `AccumulatedDeltaTicks += Ticks.FromSeconds(realDeltaTime * SimulationSpeed.Speed)`.
- В 1.2 уже есть **штатная пауза**: `SystemButtonsModel.TogglePause()` +
  `HUDPauseIndicator`. Мод добавляет только скорости > 1x и свою кнопку паузы.

## Нативный загрузчик: что важно знать

Манифест (`SerializableModManifest`) — поля JSON:

```
Description, Author, Version,
SavedModVersionCompabilityRangeWithSelf,   // именно с опечаткой "Compability"
GameVersionSupportRange, AffectsSaveGames,
Dependencies, Assemblies, Conflicts, Replaces, DisablesAchievements
```

- `Assemblies` загружаются по порядку через `Assembly.LoadFrom`.
- После загрузки загрузчик ищет **ровно один** тип с `IMod`:
  `GetLoadableTypes(assemblies).SingleOrDefault(t => typeof(IMod).IsAssignableFrom(t))`.
  Если `IMod`-типов нет — мод пропускается с warning, сборки всё равно загружены.
  Если типов больше одного — `SingleOrDefault` бросит исключение.
- Экземпляр создаётся через `DependencyContainer.Create(bootstrap)`; в контейнер
  заранее забинден `ILogger` (prefixed logger мода). Поэтому конструктор может
  принимать `Core.Logging.ILogger`.
- `Version` — SemVer. `GameVersionSupportRange` — диапазон SemVer; `"*"` работает,
  но для публикации лучше указывать конкретно, например `>=1.2.0 <1.3.0`.
- Один мод = одна папка в `mods/`; ID локального мода — имя папки.

## UI: детали реализации

Файл `src/UI/FastForwarderUI.cs`.

- IMGUI (`OnGUI`), координаты в пикселях экрана.
- Константы: `PanelWidth=200`, `PanelTop=120`, `PanelRightMargin=10`;
  кнопки по 35px с шагом 38px, высота 30px.
- `Speeds = {0, 1, 5, 10, 25}`, `Labels = {"II", ">", ">>", ">>>", ">|"}`,
  `Tooltips` — подсказки при наведении.
- Активная кнопка: фон `(1, 0.55, 0.1)`, остальные `(0.3, 0.3, 0.3)`.
- Невидимая uGUI-картинка (`alpha=0.001`) на Canvas `sortingOrder=30000` лежит
  ровно под панелью и не даёт кликам уходить в игру; `OnGUI` дополнительно
  вызывает `Event.current.Use()` для мышиных событий.
- Панель скрыта, пока `SimulationSpeedManager == null`.

Подводные камни:

- `GUI.skin.button` использует встроенный шрифт Unity (`LegacyRuntime.ttf`);
  в Unity 6 он на месте, но если текст пропадёт — искать причину здесь.
- При Active Input Handling = Input System (New) IMGUI всё равно получает события;
  проверено в 1.2.
- Не создавать `GameObject` в конструкторе мода: только через
  `Application.onBeforeRender` (главный поток).
- При смене сцен объекты без `DontDestroyOnLoad` исчезают.

## Сборка и установка

```bash
./build.sh      # сборка → mods/FastForwarder
./install.sh    # сборка + проверка manifest.json/FastForwarder.dll
```

`src/FastForwarder.csproj` сам кладёт вывод в `$(ModsDir)/FastForwarder/`
и копирует туда `manifest.json` и `README.md`.

Переопределение путей:

```bash
ShapezPath="/Applications/shapez 2.app/Contents/Resources/Data/Managed" \
ModsDir="$HOME/Library/Application Support/com.tobspr.shapez-2/mods" \
./build.sh
```

В мод попадают `FastForwarder.dll`, `FastForwarder.pdb`, `manifest.json`, `README.md`.
Игровые DLL и NuGet-пакеты не копируются (`<Private>false</Private>`).

## Отладка

```bash
tail -n 100 "$HOME/Library/Logs/tobspr Games/shapez 2/Player.log"
grep -iE "FastForwarder|mod|error|exception" \
  "$HOME/Library/Logs/tobspr Games/shapez 2/Player.log" | tail -50
```

Признаки успешной загрузки:

```
Loading .../mods/FastForwarder/FastForwarder.dll
[FastForwarder] Fast Forwarder loaded.
[FastForwarder] UI created.
[FastForwarder] SimulationSpeedManager hooked.
```

Типичные ошибки:

| Симптом | Причина / что смотреть |
|---|---|
| Мод есть в списке, но UI нет | не сработал hook: `G` не появился или `SimulationSpeed` null |
| `Skipping loading ... could not find a single IMod implementation` | DLL не содержит `IMod` (не тот AssemblyName/старая сборка) |
| `AssemblyLoadFail` | битая/несовместимая DLL, не хватает зависимости |
| `GameVersionNotSupportedByMod` | `GameVersionSupportRange` не включает версию игры |
| Панель есть, клики не работают | IMGUI/Input System или блокер перекрыт другим Canvas |
| Панель налезает на HUD | двигать `PanelTop` / `PanelRightMargin` |

## Как исследовать игру

```bash
export DOTNET_ROOT=$HOME/.dotnet DOTNET_ROLL_FORWARD=Major
M="/Applications/shapez 2.app/Contents/Resources/Data/Managed"

# список типов
$HOME/.dotnet/tools/ilspycmd -l c "$M/SPZGameAssembly.dll" | grep -i speed

# декомпиляция одного типа
$HOME/.dotnet/tools/ilspycmd -t SimulationSpeedManager "$M/SPZGameAssembly.dll"

# дамп всей сборки в файл (большой)
$HOME/.dotnet/tools/ilspycmd "$M/Game.Orchestration.dll" > /tmp/orch.cs
```

Полезно помнить: `Assembly-CSharp.dll` отсутствует; ищите тип по всем `Game.*.dll`
и `SPZGameAssembly.dll`. `ilspycmd -t` требует полное имя с namespace
(кроме глобального namespace).

## Чек-лист обновления под новую версию игры

1. Обновить игру, проверить версию:
   `defaults read "/Applications/shapez 2.app/Contents/Info" CFBundleShortVersionString`.
2. Проверить, что не поменялись ключевые API:
   - `Game.Core.Modding.IMod` и его загрузка;
   - `StaticGameCoreAccessor.G` и `IGameSessionManagers.SimulationSpeed`;
   - `SimulationSpeedManager.Speed/IsPaused`;
   - сигнатура `GameSessionOrchestrator.Prepare`.
3. При изменениях — поправить `FastForwarderUI.Update()` / ссылки в csproj.
4. `./install.sh`.
5. Перезапустить игру, проверить лог (см. выше).
6. При необходимости обновить `GameVersionSupportRange` в `manifest.json`
   и `Version` (SemVer).
7. Пересобрать `dist/FastForwarder-1.2.zip`:

   ```bash
   MODS="$HOME/Library/Application Support/com.tobspr.shapez-2/mods/FastForwarder"
   rm -rf /tmp/ffpkg && mkdir -p /tmp/ffpkg/FastForwarder
   cp "$MODS/manifest.json" "$MODS/README.md" \
      "$MODS/FastForwarder.dll" "$MODS/FastForwarder.pdb" /tmp/ffpkg/FastForwarder/
   (cd /tmp/ffpkg && ditto -c -k --sequesterRsrc --keepParent FastForwarder \
     "$HOME/Work/mods/shapez2-fast-forwarder/dist/FastForwarder-1.2.zip")
   ```

   `./build.sh` кладёт файлы сразу в папку модов, поэтому архив собирается
   из неё.

## Рецепты будущих модификаций

### Изменить набор скоростей

`src/UI/FastForwarderUI.cs`:

```csharp
private static readonly float[] Speeds = { 0f, 1f, 5f, 10f, 25f };
private static readonly string[] Labels = { "II", ">", ">>", ">>>", ">|" };
private static readonly string[] Tooltips = { ... };
private const float PanelWidth = 200f; // подогнать под число кнопок
```

Шаг кнопок (38px) и ширина (35px) заданы в `OnGUI`. Проверить, что
`_selectedIndex = 1` остаётся индексом «1x».

### Сдвинуть/переставить панель

`PanelTop`, `PanelRightMargin` (и размер блокера в `CreateRaycastBlocker`,
он использует те же константы).

### Горячие клавиши

Игра использует Input System. Варианты:

- `UnityEngine.InputSystem.Keyboard.current` — добавить ссылку на
  `Unity.InputSystem.dll`, проверять `wasPressedThisFrame`; обязательно
  игнорировать ввод, когда открыт чат/поле ввода.
- Аккуратнее — зарегистрировать игровой keybinding через API `Keybindings`
  (см. `Game.Core.Settings`/`Keybindings` в `SPZGameAssembly`), чтобы клавиша
  была переназначаемой.

### Сохранять выбранную скорость между сеансами

Простейший путь — свой JSON в `Application.persistentDataPath` (файл рядом с
сейвами) или игровые настройки. Нативно у игры есть `ModSaveData<T>`
(см. документацию shapez 2 / соседний проект `shapez2-rate-display`), но он
завязан на сейв, а скорость — сессионная настройка.

### Сделать нативный HUD вместо IMGUI

Ориентиры в 1.2:

- `SystemButtonsModel` / `HUDSystemButtonsView` — верхняя правая группа кнопок;
- `HUDPauseIndicator` — индикатор паузы (`HUDPart`, `CanvasGroup`);
- `HUDTheme`, шрифты `Font-Medium SDF`, TMP-компоненты.

Рабочие примеры работы с HUD/TMP есть в соседнем проекте
`../shapez2-rate-display` (патчи `HUDPinnedShape`, `HUDMilestoneSummaryDisplay`,
создание TMP-лейблов, `HUDTheme`). Это заметно больше работы, чем IMGUI, но
даст нативный вид и корректный ввод.

### Добавить Harmony-патчи

Сейчас Harmony не нужен. Если понадобится:

1. Взять `0Harmony.dll` версии **2.4.3** (net472) — он уже проверен в игре
   (см. `../shapez2-rate-display/libs/0Harmony.dll`).
2. Положить в свой `libs/`, добавить `<Reference>` + `<None ... CopyToOutputDirectory>`.
3. В `manifest.json` указать `"Assemblies": ["0Harmony.dll", "FastForwarder.dll"]`
   (порядок важен).
4. Не тащить другую версию Harmony: PinnedRate уже грузит 2.4.3, конфликт
   идентичных имён сборок может сломать загрузку.

### Локализация

Строки панели сейчас захардкожены. Для русского/английского можно:
- простой словарь в моде по `LocalizationManager`/языку игры;
- или использовать игровую систему переводов (см. `Core.Localization`,
  `ILocalizationManager`), если нужен нативный механизм.

## Известные риски

- Привязка к `StaticGameCoreAccessor` помечена `[Obsolete]` — API могут убрать
  в следующих версиях. При падении искать замену через DI
  (`IGameSessionManagersProvider`) или патч `GameSessionOrchestrator.Prepare`.
- `GameVersionSupportRange: "*"` удобно локально, но при обновлении игры мод
  может загрузиться и упасть. Для распространения сузить диапазон.
- Ускорение времени субъективно «читерит»: при желании выставить
  `"DisablesAchievements": true`.
- IMGUI не масштабируется под все разрешения/DPI идеально; при проблемах
  переходить на uGUI/TMP.
- Мод не сохраняет выбранную скорость: каждый новый сеанс — `1x`.

## Файлы

```
manifest.json                              манифест мода (Version 1.0.7)
build.sh / install.sh                      сборка и установка
src/FastForwarder.csproj                   проект, вывод сразу в mods/FastForwarder
src/FastForwarderMod.cs                    IMod, создание UI, логгер, Dispose
src/UI/FastForwarderUI.cs                  IMGUI-панель, блокер, синхронизация паузы
docs/NOTES.md                              этот файл
reference/FastForwarder-1.0.6.dll          оригинал (BepInEx)
reference/FastForwarder-1.0.6.decompiled.cs декомпиляция оригинала
reference/README-original.md               README оригинала
reference/1.0.6-...zip                     исходный архив
dist/FastForwarder-1.2.zip                 готовый пакет для установки
```
