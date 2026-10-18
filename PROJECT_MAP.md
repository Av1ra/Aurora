# Project map

| Area | Source / entry point |
| --- | --- |
| Launcher UI and game home | Aurora.Launcher/App.cs |
| Language | Aurora.Launcher/UiLanguage.cs |
| Compact alpha console / tilde host | Aurora.Launcher/GameConsoleHost.cs |
| In-game PBR panel | Aurora.Launcher/ConsoleWindow.cs |
| Optional signed editor host | Aurora.Launcher/EditorHost.cs |
| Download, verification, editor installation | Aurora.Launcher/UpdateService.cs |
| Manifest, safe paths, module installation, NMF | Aurora.Core/ |
| Registered Workshop release detection and transactional updates | Aurora.Core/WorkshopUpdates.cs, docs/WORKSHOP_UPDATES.md |
| Existing Model Editor (no redesign requested) | Aurora.Editor/ and Aurora.Editor.Module/ |
| Alpha mask backend | Backend/ |
| Independent FOV Changer DLL | Modules/FovChanger/, Prepare-Fov.ps1 |
| PBR payload packaging | Prepare-Graphics.ps1 |
| Root EXE entry points | Aurora.Setup.Native/Entry.cpp |
| Adjacent archive installer / updater | Aurora.Setup.Native/Setup.cpp, build-setup.ps1 |
| Start own game with verified modules | Aurora.Bootstrap/, Native/ |
| Reproducible package | build.ps1, build-native.cmd |
| Module signing contract | docs/MODULES.md |
| Core/export/update/native tests | Aurora.Tests/, Aurora.ExportTests/, Aurora.UpdateTests/, Packaging/test-native.py |
| Cloud build (never publishes releases) | .github/workflows/build.yml |
| Build and signing | docs/DEVELOPMENT.md |
| Current verified state and remaining work | docs/STATUS.md |
| Codex resume checkpoint, acceptance matrix and owner constraints | docs/CODEX_HANDOFF.md |
| English player description for future release | docs/RELEASE_DESCRIPTION.md |
| Prior notes and audit (historical, not current instructions) | docs/archive/ |

All launcher/editor sources are in this repository. Private PBR renderer internals are a separate game module and are not required to build Aurora. Do not depend on a local drive path or a private game install for the basic CI build.



| Code-defined Steam Workshop ID registry and location checks | Aurora.Core/SteamDiscovery.cs |

| Steam launch bridge and original executable restoration | Aurora.Setup.Native/Entry.cpp (AURORA_STEAM_ENTRY), Aurora.Setup.Native/Setup.cpp |
