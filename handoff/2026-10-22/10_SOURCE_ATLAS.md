# Атлас исходников

Все пути ниже относительно корня Aurora. Машинный [source-index.json](source-index.json) содержит перечень файлов исходного дерева, размеры и SHA256: UTF-8 текст приведён к LF для независимости от Git autocrlf, бинарные файлы проверяются побайтово. Сама handoff-папка сохраняется без преобразования окончаний строк. Это не копия build cache. Для изменения конкретного сценария читать обе стороны: WinUI orchestration и viewport/Core, затем экспорт.

## Процессы и доставка

| Область | Вход и ответственность |
| --- | --- |
| Корневые EXE | `Aurora.Setup.Native/Entry.cpp`: запускает общий host с нужным режимом |
| Launcher | `Aurora.Launcher/App.cs`, `App.xaml`: окно, модули, settings, запуск игры |
| Editor host | `Aurora.Launcher/EditorHost.cs`: проверка signed payload, загрузка Module, ошибки Editor, startup-check |
| Console | `GameConsoleHost.cs`, `ConsoleWindow.cs`: горячие клавиши/контекст игры, F9 и tilde |
| Обновления | `UpdateService.cs`, `InstallationPaths.cs`; Core `WorkshopUpdates.cs`, `ModCatalog.cs`, `ModuleInstaller.cs`, `ModuleRuntimeCompatibility.cs` |
| Подписи | `Aurora.Publisher/`: выпуск manifest/release, проверяемый public pin в `Aurora.Launcher/Assets/publisher.pem` |
| Native bootstrap/Steam | `Aurora.Bootstrap/`, `Native/`, `build-native.cmd`: загрузка runtime, bridge/оригинал setup |
| Установка | `Aurora.Setup.Native/`, `build-setup.ps1`, `Packaging/legacy-files.tsv`: adjacent ZIP, транзакции, известные legacy hashes |
| Сборка | `build.ps1`, `Prepare-Fov.ps1`, `Prepare-Graphics.ps1`, `.github/workflows/build.yml` |
| FOV | `Modules/FovChanger/`: resolver/совместимость, startup-only DLL |

`Aurora.Editor.Module` компилирует/связывает Editor с host. Наличие каталога `Aurora.Editor` не означает отдельный независимый runtime в готовой поставке. `legacy` — исторический код; не выбирать его как основной entry point.

## Editor: UI и управление состоянием

| Файлы | Сценарий |
| --- | --- |
| `Aurora.Editor/App.cs` | Window, WebView2, initialization, message dispatch, busy/state, UI resources |
| `EditorMenus.cs`, `EditorModelSettings.cs` | пять верхних меню, guided Scripts, применимые поля |
| `EditorImport.cs`, `EditorLods.cs` | открыть NMF/OBJ, добавить/заменить/удалить LOD |
| `EditorObjects.cs`, `EditorMeshPicker.cs` | выбор/переименование/разделение, подтверждение/отмена viewport pick |
| `EditorMaterials.cs`, `EditorPartMaterials.cs` | native/PBR карты, настройки и материалы дополнительных частей |
| `EditorGizmos.cs`, `EditorVehicle.cs` | режимы pivot, orientation, light points, symmetry |
| `EditorCargo.cs`, `EditorSmoke.cs`, `EditorEffects.cs` | typed cargo, placement/scale, дым, другие эффекты |
| `EditorTransport.cs`, `EditorMechanisms.cs`, `EditorMechanismNames.cs` | редактор оборудования по классам и секциям |
| `EditorAnimation.cs` | timeline, ключи, FPS, preview и native bindings |
| `EditorSounds.cs` | подходящие звуковые INI и импорт своего INI |
| `EditorPreviews.cs` | две камеры, режим редактирования, Shot settings и захват при экспорте |
| `EditorHistory.cs`, `EditorProject.cs`, `ProjectDocument.cs` | снимки, Undo/Redo, draft, .aur |
| `EditorWorkspace.cs`, `EditorTransportExport.cs`, `EditorSkinExport.cs` | staged export, выбор вариантов, workspace/skin dependencies |
| `MaterialDocuments.cs`, `MaterialSettings.cs` | чтение MTL в редактор, представление material settings |
| `ModelExport.cs`, `MaterialExport.cs`, `VariantExport.cs` | нижний слой записи файлов и пар материалов |

Редактор разбит на partial `EditorWindow`, поэтому функции разных файлов разделяют поля. Перед переносом функции проверить вызывающие места и историю/сериализацию, а не только её визуальный контрол.

## Viewport и backend

`Aurora.Editor/Assets/viewport.html` — WebGL scene, загрузка model payload, материалы, camera navigation, wheel/effect/animation rendering, bridge сообщений. `gizmo.js` — отображение и drag осей/плоскостей/колец, snapping и transforms. `previews.js` — камеры/композиция/захват превью. Плавная камера и UI числового ввода не должны конфликтовать с gizmo drag и mesh picking.

`Backend/mask_backend.py` и соседние модули — CLI conversion/texture work; упаковываются PyInstaller в `app/Editor/Backend`. Запуск backend — процесс, не универсальная замена Core parser. Ошибки обрабатываются на WinUI стороне, данные возвращаются через подготовленный workspace/WebView payload.

## Core: форматы и транспорт

| Файлы `Aurora.Core/` | Назначение |
| --- | --- |
| `NativeModel.cs`, `NmfExport.cs` | двоичный NMF reader/writer, структуры meshes/material slots |
| `ObjImporter.cs`, `ObjTextureMap.cs`, `LodModelImport.cs` | OBJ, mtllib/maps, импорт LOD |
| `ModelEditing.cs`, `ModelOrientation.cs`, `MechanismAxis.cs` | split/rename/transforms, pivot orientation и ось механизма |
| `WheelDiagnostics.cs`, `ViewportNavigation.cs` | компоненты колёс/центры/маски, camera math |
| `AnimatedNmf.cs`, `AnimationTimeline.cs`, `AnimationBindings.cs` | NAF/animated NMF writer, interpolation, action mapping |
| `VehicleScript.cs` | токены/поля/повторения script.ini, адресное изменение вместо полного разрушительного rewrite |
| `VehicleCatalog.cs`, `VehicleProfiles.cs`, `Data/TransportProfiles.json` | страны/классы/локализация из игры, 88 встроенных профилей |
| `TransportParameters.cs`, `VehicleRoleParameters.cs` | аргументы оборудования и поля роли/применимость |
| `VehicleLights.cs`, `VehicleSmoke.cs`, `VehicleCargoBox.cs` | точки света/дыма, визуализация груза |
| `VehicleParts.cs`, `VehicleConsist.cs`, `VehicleLods.cs` | отдельные части, состав/доступность, LOD records |
| `VehicleSoundFiles.cs`, `VehicleAssets.cs`, `VehicleSharedMaterials.cs` | пути звуков, game-root assets, shared materials |
| `NativeMaterialText.cs`, `MaterialFiles.cs`, `DdsImage.cs` | стандартный MTL, companion naming, DDS |
| `TransportWorkspace.cs`, `TransportPackage.cs`, `ModelFileNames.cs` | перенос зависимостей, безопасные имена, export workspace |
| `AuroraProjectArchive.cs`, `EditHistory.cs` | .aur AES-GCM archive, snapshots/history |

[transport-parameters.json](contracts/transport-parameters.json) получен из скомпилированного Core: параметры, аргументы, применимость, actions, RU/EN profiles. Это снимок текущего API, не утверждение о проверке каждого параметра игрой. [signed-modules.json](contracts/signed-modules.json) — публичный декодированный контракт поставки.

## Проверки и исследования

`Aurora.Tests`, `Aurora.ExportTests`, `Aurora.UpdateTests` — консольные автоматические проверки. `Packaging/test-viewport.mjs`, `test-viewport-webgl.mjs` — JS и реальный WebGL. `test-editor-startup.py` — настоящий WinUI constructor. `test-native.py`, `test-package.py`, `test-brand.py` — installer, единый ZIP, artwork/backend. Не переписывать их в фиктивные проверки наличия строки.

`Packaging/NmfAudit`, `MaterialAudit`, `CapabilityAudit`, `audit-game-capabilities.py`, `audit-script-semantics.py`, `audit-transport-details.py` — read-only корпусные исследования. Данные игры не становятся свободно распространяемыми из-за участия в тесте.

Исторические отчёты: [основные требования](../../docs/EDITOR_TRANSPORT_AUTHORING_REQUIREMENTS.md), [дополнительные 22](../../docs/EDITOR_TRANSPORT_ADDITIONAL_22.md), [глубокий аудит](../../docs/EDITOR_TRANSPORT_DEEP_AUDIT_2026-10-21.md), [чеклист](../../docs/EDITOR_TRANSPORT_CHECKLIST_2026-10-21.md), [последний отчёт](../../docs/TRANSPORT_SESSION_REPORT_2026-10-22.md), [последняя startup-регрессия](../../docs/EDITOR_STARTUP_REGRESSION.md). Читать их даты: старый план не отменяет позднее решение.

## Graphics отдельно

В GameData `SovietRepublic/MODS/PBR.dll/SRC/Product`: `Bridge64.cpp` — hook/render integration, `Runtime.cpp` — lifecycle, `PbrParser.cpp` и `SovietCDK/MaterialConfig.*` — правила PBR, `VfsOverlay.cpp` — overlays, `ModelReplacement*`/`NativeTextureIO*`/cooker — asset replacement, `PbrDebugControls*` — настройки, MinHook — vendor dependency. Build scripts рядом. Не переносить произвольно приватную подсистему в Aurora public repo. Связь: exported native material identity → numbered PBR companion → renderer configuration, а не номер файла без контекста.
