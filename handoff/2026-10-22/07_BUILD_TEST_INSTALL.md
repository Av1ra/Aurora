# Сборка, проверки и установка на другом ПК

## Среда

Windows x64, .NET SDK 10, Visual Studio Build Tools с C++ и Windows SDK (`vswhere`), Python 3.12 с numpy, Pillow и PyInstaller. WebView2 нужен viewport. Node и Playwright/Edge нужны для JS/WebGL-проверок, но не для запуска готового комплекта. Установки зависимостей зафиксированы в [build workflow](../../.github/workflows/build.yml) и [DEVELOPMENT](../../docs/DEVELOPMENT.md); сверять версии с ними, а не с чужим глобальным Python.

На исходном ПК Python: `C:\Users\AVR\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`; вспомогательные зависимости — `D:\PBR-Development\Aurora-build-deps`. Это локальные пути, не переносимая зависимость проекта. Кэши SDK и Python в архив не включены: их восстанавливают на целевой машине.

## Исходники и локальная сборка

```powershell
git lfs install
git clone https://github.com/Av1ra/Aurora.git
cd Aurora
git lfs pull
dotnet run --project Aurora.Tests
dotnet run --project Aurora.ExportTests
dotnet run --project Aurora.UpdateTests
node Packaging/test-viewport.mjs
./build.ps1 -Development -Python python
```

`-Development` создаёт временный ключ и отдельный public pin только в собранном staging; исходный production pin не подменяет. Ключ удаляется после сборки. Такой комплект не является production-подписанным издателем: не смешивать модули с разными ключами. Результат в `artifacts/build-<id>/output`, путь последнего build в `artifacts/latest-build.txt`.

Для восстановления точного runtime source использовать InstalledSource из [snapshot](snapshot.json), а не HEAD новой документации. [Restore-Workspace.ps1](scripts/Restore-Workspace.ps1) умеет создать отдельный checkout этого SHA; `-DryRun` ничего не меняет. Checkout старого SHA не содержит эту новую handoff-папку: сохранить её рядом либо читать в основном clone.

## Графические компоненты

Отдельный clone `Av1ra/GameData` требует доступа владельца. Зафиксированный SHA — `c72d027dd4449cda05da10b56850657f74d10561`. Канонический код: `SovietRepublic/MODS/PBR.dll/SRC/Product`.

`build-runtime.cmd` собирает PBR; `build-vfs.cmd` — VFS. Их outputs различаются: runtime пишет в корневой `dist`, VFS — в `build-vfs`. Не считать любую папку `dist` готовым согласованным NativeDist. Состав/контракт сборки Aurora проверяет [Prepare-Graphics.ps1](../../Prepare-Graphics.ps1). После подготовки передать `./build.ps1 -Development -NativeDist <папка>`. Файлы игры для этого не заменяются.

## Production-подпись

Без `-Development` build использует существующий PKCS#8: `AURORA_SIGNING_KEY` либо `%LOCALAPPDATA%\Aurora\publisher\publisher.pk8`. Он должен соответствовать `Aurora.Launcher/Assets/publisher.pem`. Новый произвольный ключ вместо потерянного старого не подходит. Закрытый ключ не включён в Git, RESAURO и логи. Передавать его отдельно защищённым способом, только если действительно нужен выпуск от прежнего издателя.

Во время packaging не менять исходники. `app/build-info.json` записывает SHA, ModifiedSources и DevelopmentSigning. Чтобы получить чистое происхождение, сначала commit, затем сборка неизменённого дерева.

## Матрица проверок

| Проверка | Команда / область |
| --- | --- |
| Core, форматы, поля, история | `dotnet run --project Aurora.Tests` |
| Экспорт и переносимость | `dotnet run --project Aurora.ExportTests` |
| Обновление и подписи | `dotnet run --project Aurora.UpdateTests` |
| JS-трансформы, камера, эффекты | `node Packaging/test-viewport.mjs` |
| Реальный WebGL | `node Packaging/test-viewport-webgl.mjs` с установленным Playwright и браузером |
| Настоящий скрытый WinUI Editor | `python Packaging/test-editor-startup.py <stage>/app/Aurora.Launcher.exe` |
| Native installer/rollback | `python Packaging/test-native.py <stage>/app/Aurora.Installer.exe` |
| Один ZIP и парный Setup | `python Packaging/test-package.py <build>/output` |
| Логотипы PE/backend | `python Packaging/test-brand.py <stage> <output>/Aurora-Setup.exe` |
| Корпус NMF, read-only | `dotnet run --project Packaging/NmfAudit -- <media_soviet> <report.json>` |
| Материалы корпуса, read-only | `dotnet run --project Packaging/MaterialAudit -- <media_soviet> <report.json>` |

Startup test не вызывает `Activate`: создаёт настоящее подписанное окно, проверяет присоединение toolbar и непрозрачный presenter, записывает отчёт и закрывается. Он ловит реальные ошибки конструктора, но не заменяет интерактивную приёмку GUI. При запуске corpus-аудита игра только читается. Не распространять её ресурсы вместе с отчётом.

Для этой передачи проекта проверять отдельно `python handoff/2026-10-22/scripts/Verify-Handoff.py`: ссылки, хеши, оригинальные картинки, PSD и полноту manifest. Не пересобирать product только ради новой документации.

## Установка

[package](package/README.md) содержит ровно ту сборку, которая уже была установлена владельцу. Setup должен лежать рядом с соответствующим ZIP. Это не новый Release и не новый runtime build. `RESAURO.zip` — архив разработки, его нельзя подставить вместо `Aurora-0.2-win-x64.zip`.

Закрыть Launcher и Editor перед заменой. Installer сохраняет backup и receipt; неизвестные пользовательские файлы не удаляет произвольно. Не завершать приложения владельца принудительно и не заменять активный renderer игры. Проверить receipt и хеши после установки, отдельно получить результат GUI/Steam/игровых сценариев.

CI development-artifacts имеют ограниченное хранение (в workflow 7 дней), поэтому handoff хранит установленный ZIP в Git LFS и локальном RESAURO. Не создавать tag/Release и не менять версию автоматически.
