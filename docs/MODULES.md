# Модули Авроры: протокол 1

Папка мода в Workshop либо `mods/library/<id>` содержит `acode.lnchr` и подписанный набор `Payload`. Пример **до подписания**:

```json
{
  "Schema": 1,
  "Id": "aurora.camera",
  "Name": "Камера",
  "Category": "camera",
  "Version": "0.2.0",
  "MinimumLauncher": "0.2.0",
  "Config": "camera.ini",
  "Files": [
    { "Path": "Payload/mods/library/aurora.camera/fov.dll", "Sha256": "64 hexadecimal digits" }
  ],
  "Options": [
    { "Id": "fov", "Label": "Поле зрения", "Section": "features", "Key": "enabled", "Beta": false, "Default": true }
  ]
}
```

Этот пример не является готовым FOV-модом. DLL реализуется отдельно; её опции должны читать указанные INI-ключи. Текущий протокол описывает булевы переключатели. Разделы/ключи INI ограничены буквами, цифрами и `_`; файл конфигурации — простой `.ini` внутри `mods`. Поле `Category` задаёт категорию; `Name` — пункт меню. Дубликаты ID разрешаются в пользу новой совместимой версии, при равных версиях — найденного позднее Workshop-источника.

Установщик разрешает только точные текущие runtime-пути `mods/pbr.dll`, `mods/m.dll`, `mods/glass_default.dds`, `pbr.exe` (совместимость), а для будущих модулей — `mods/library/...`. Каждому файлу соответствует SHA-256 в подписанном документе. После проверки мод устанавливается с резервными копиями; при ошибке копирования применённые изменения откатываются. Пользовательская INI не перезаписывается. При запуске проверяются и источники, и установленные DLL; bootstrap повторно сверяет SHA до загрузки в новый процесс игры. Разные хэши одного runtime-пути у найденных модов означают конфликт.

## Подписание

Закрытый ключ хранится **вне репозитория и release**: `%LOCALAPPDATA%/Aurora/publisher/publisher.pk8`, ACL только текущего пользователя. Его необходимо отдельно сохранять в защищённой резервной копии; это не файл для отправки в чат. Открытый ключ находится в `Aurora.Launcher/Assets/publisher.pem`.

Команда `Aurora.Publisher public <pem>` создаёт ключ только при его отсутствии и экспортирует открытый. При подписании отсутствующий закрытый ключ вызывает ошибку: нельзя незаметно сменить издателя.

`Aurora.Publisher manifest <manifest.json> <acode.lnchr>` подписывает **точные байты UTF-8** документа. Конверт JSON содержит `Payload` (base64 исходных байтов) и `Signature` (base64 ECDSA/SHA-256, формат .NET IEEE-P1363). Парсинг содержимого происходит только после успешной подписи. Алгоритм и ключ закреплены в лаунчере; мод не может выбрать другой доверенный ключ или отключить проверку.

`Aurora.Publisher release <version> <zip> <aurora-update.json>` подписывает строку `version + LF + assetName + LF + lowercaseSha256`. Release содержит ZIP `Aurora-<version>-win-x64.zip`, `aurora-update.json` и `Aurora-Setup.exe`. Все обновления приложения распространяются через публичный репозиторий. Токены GitHub игрокам не нужны и в приложение не встраиваются.

При несовместимом изменении протокола/новом моде: поднять версию лаунчера, выпустить подписанный релиз, затем поставить его версию в `MinimumLauncher` и заново подписать мод. Технически совместимое обновление мода не требует искусственно ломать старый лаунчер; если требуется обязательное обновление, издатель поднимает `MinimumLauncher`.

## Порядок транспорта

В прежнем VFS подменённые папки перечислялись раньше штатных. Объединённый список теперь сортируется по именам файлов/папок без учёта регистра; приоритет содержимого подмен сохранён, дубликаты подавлены. Это устраняет отдельный первый блок из `mods`. Имена папок не равны локализованным названиям машин: сортировка самого магазина по отображаемому имени требует отдельной проверки игры и здесь не объявляется выполненной.

## Компактная упаковка приложения

Установщик Win32 содержит основной ZIP и не дублирует .NET. Пользователь выбирает отдельно папку приложения и папку игры; выбранное место приложения записывается в LocalAppData/Aurora/install-root.txt, игры в game-path.txt. Обновление проверяет сохранённый путь. Требуется Windows с системным tar.exe.

Редактор распространяется отдельным Aurora-Editor-<version>-win-x64.zip и подписанным aurora-editor.json. В Editor находится собственная подписанная acode.lnchr, Aurora.Editor.Module.dll, viewport и backend. Общие WinUI/.NET/WebView2 библиотеки берутся из лаунчера. При поиске игровых модов загрузки кода нет; EditorHost отдельно проверяет официальный подписанный addon и только после проверки загружает его сборку. Метаданные редактора задают MinimumLauncher.

Packaging/legacy-files.tsv содержит хеши старого пакета. Установщик переносит в резервную копию только совпадающие лишние файлы, отсутствующие в новом пакете. Пользовательские изменения остаются. Подписанный новый Editor не удаляется при обновлении приложения.

## Schema 2 settings modules

Schema 2 extends ModOption with Kind (toggle/slider), Minimum, Maximum and DefaultValue. ModManifest Placement can be navigation (default) or settings. The launcher renders these controls generically and does not execute module code. The FOV Changer implementation and executable compatibility guard live entirely in Modules/FovChanger/fov.dll, packaged by Prepare-Fov.ps1. Its Config is fov.ini; options are camera.enabled and camera.degrees. Settings apply at next launch. Payload destinations remain subject to ModuleInstaller's allowlist.

The complete preview embeds signed module catalogs under app/Modules. They are discoverable alongside Workshop libraries. Launch verifies installed payloads and installs a missing/different signed payload before starting the game's own bootstrap. Existing user INI values are preserved.

## Workshop ID registry (discovery only)

Register Steam Workshop items in `Aurora.Core/SteamDiscovery.cs` under `SteamDiscovery.RegisteredMods`. Example: `new("3822550289", "PBR", ["mods/m.dll", "mods/pbr.dll", "mods/glass_default.dds"])`.

Aurora checks both `steamapps/workshop/content/784150/<SteamId>/` across detected Steam libraries and the creator staging layout `SovietRepublic/media_soviet/workshop_wip/<SteamId>/`. The files shown in the Workshop item, such as `manifest.json` and `workshopconfig.ini`, are Steam metadata, not signed Aurora manifests. A source is reported as found only when its declared files are present; incomplete sources list missing files. If multiple locations exist, a complete subscribed copy is preferred to an incomplete staging copy.

**Important:** Adding an ID lets the launcher discover and report the Workshop item. It does not grant permission to execute, inject, copy or auto-update its unsigned DLLs. Actual Aurora runtime modules still require a signed `acode.lnchr` and SHA-256 verification, including before launching the game. Register additional IDs and explicit expected files in source rather than trusting arbitrary folder contents.

## Signed module UI stage

Optional `Stage` in `ModManifest` may be `alpha`, `beta` or omitted (stable/default). The Settings renderer shows the corresponding badge next to the signed module name. Unknown values are rejected by manifest verification. The independent FOV module declares `Stage: alpha` and its UI title is `Изменение угла обзора`.

### Control dependencies

An option may include optional `Requires: ["other-option-id"]`. Dependencies refer to toggle IDs in the same signed manifest. IDs must be unique, prerequisites must exist, and cycles are rejected. Missing Requires retains legacy behavior. The launcher disables dependent controls while any prerequisite is off, including transitive prerequisites, without overwriting their saved values. Keep dependencies in the signed package metadata rather than embedding feature-specific DLL logic in the launcher. Graphics uses its master for all effects and vehicles for glass; FOV's degrees control depends on enabled. Additional shadows depend on master PBR, not exclusively on roads, since vehicle self-shadows are a separate use.
