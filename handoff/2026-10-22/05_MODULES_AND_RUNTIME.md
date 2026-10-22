# Launcher, модули, Graphics и FOV

## Модульный контракт

`acode.lnchr` — JSON-конверт: base64 exact UTF-8 Payload + ECDSA SHA256 Signature (IEEE-P1363). Внутри manifest schema, ID/version/minimum launcher, config, stage, files и options. Сначала подпись, потом parsing и safe-path/hash checks. `Payload/` — имя корня содержимого подписанного модуля, а `mods/` — destination игры; это разные уровни. Название Payload не является требованием самой игры.

| Модуль | Пакет / назначение |
| --- | --- |
| aurora.editor | Всегда bundled `app/Editor`, после проверки manifest загружается DLL в общий WinUI host |
| aurora.fov | Независимая fov.dll, `[camera] enabled/degrees`, settings row Alpha; fallback подписывается отдельно |
| aurora.graphics | Подписанные pbr.dll/m.dll/glass_default.dds; master/category options с Requires |

Точные files/hashes установленной сборки: [signed-modules.json](contracts/signed-modules.json). Публичный ключ в source; закрытый только отдельно. Изменённая DLL без переподписания manifest даёт Invalid publisher signature/hash failure; нельзя «починить» отключением проверки.

Toggle dependencies не уничтожают сохранённые намерения пользователя. Manifest Stage alpha/beta — отдельный badge, а не часть названия переключателя. Schema2 добавляет slider bounds. Config сохраняется, не перетирается при замене runtime.

## Workshop и обновления

- AppID 784150, зарегистрированный Workshop item 3822550289. Поиск подписки в Steam libraries и workshop_wip — discovery.
- Наличие workshopconfig.ini/manifest.json, правильного Steam ID или файлов само по себе не разрешает исполнение DLL.
- Registered update offer сверяется с signed runtime, destination collisions и receipts. ModuleRuntimeCompatibility не должен незаметно откатывать подтверждённый Workshop update старым bundled fallback.
- ModuleInstaller проверяет разрешённые runtime destinations, SHA, делает backup/rollback. Пользовательские INI сохраняются.
- GitHub application updater проверяет release metadata/signature/ZIP. **Текущий Version-only comparison не различает новые сборки с той же 0.2**; это известный пробел, не исправленное свойство.
- Future arbitrary local module import, generic GitHub catalog и Dev access — проектные идеи, не гарантированная реализация.

## Graphics source и build

Repo `https://github.com/Av1ra/GameData`, pinned `c72d027dd4449cda05da10b56850657f74d10561`.

Путь `SovietRepublic/MODS/PBR.dll/SRC/Product/`: `Bridge64.cpp` D3D11/material adapters и shaders; `Runtime.cpp` runtime lifecycle; `PbrParser.cpp` config/material parsing; `SovietCDK/MaterialConfig.*` native/PBR matching; `VfsOverlay.cpp` m.dll; `ModelReplacement.cpp`, `NativeTextureIO.cpp`, asset cooker; `PbrDebugControls.cpp` F9/native control bridge; vendor MinHook. Точная роль функции проверяется в текущем source, поскольку старые README описывают прежние stages.

`build-runtime.cmd` создаёт `dist/pbr.dll`; `build-vfs.cmd` создаёт `build-vfs/m.dll` и запускает VFS test, затем output нужно осмысленно передать в NativeDist. Не путать собственный runtime launcher этого repo с современным Aurora host. Aurora собирается без private Graphics source; `-NativeDist` добавляет готовый signed graphics fallback.

PBR companions подбираются по native material identity. Отдельные режимы vehicle/models/roads/shadows/glass имеют engine-specific contracts и fallback на native. Ранее найденные halo/terrain filtering и engine shader вопросы не объявлены решёнными только из-за Editor export PASS.

## Горячие клавиши и фактический FOV

F9: native graphics helper в PbrDebugControls запускает WinUI console host, общается через сообщения и native settings. Отдельная tilde alpha developer console принадлежит GameConsoleHost и работает только для запущенной игры/его окна. Это не редакторская camera control.

**Пользователь хотел:** маленький F9 список модов, сохранение последней страницы при скрытии/повторном открытии и live FOV. **Подтверждённый gap:** текущий `Modules/FovChanger/FovChanger.cpp` читает INI в worker один раз при DLL_PROCESS_ATTACH, записывает float и возвращается. Нет polling/message loop для live FOV. README «следующий запуск» соответствует этому коду. Не записывать запрос владельца как готовую функцию.

FOV принимает только pin SOVIET64.exe SHA `296644a9f207d609031fc2ae73fed2dcb34619a1d55a35d1c7b51965ce6841b8`, уникальную таблицу в read-only non-code PE sections и 20–100 градусов. Не поддерживает все будущие builds. Editor preview FOV 5–120 — другой диапазон, другой объект и другая задача.

## Установка / Steam

Root Editor.exe передаёт `--editor` в app/Aurora.Launcher.exe. Ошибка создания Editor теперь даёт Editor error window и журнал, а не MainWindow fallback. Скрытый construction check обязателен в build.

Native Setup сохраняет original `SETUPAPPLICATION SOVIET.exe_orig`, ставит Aurora Steam bridge с контролем владельца/хеша. Не заменять Steam-modified неизвестный файл и не трогать `_orig`. Restore явный. Настоящий Steam Play acceptance и live FOV нуждаются в отдельной проверке.
