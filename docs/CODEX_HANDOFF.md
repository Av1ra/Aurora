# Codex handoff: Aurora Launcher + Aurora Editor

Checkpoint recorded: 2026-10-19. Canonical repository: `Av1ra/Aurora`, branch `main`. Product version: **0.2**. Identify all test builds by full commit SHA and Actions run ID, not a new public version number.

## Read this first

Latest model workflow: [guided purchase/class/sound settings and visual cargo](EDITOR_CARGO_GUIDED_MODEL.md). This supersedes generic script-field editing as the primary flow; cargo is inside Model settings with live Move/Rotate/Scale and proportional/per-axis scaling. See STATUS for installed checkpoint.

Current owner cycle (2026-10-20): [history, model settings, cargo and automatic previews](EDITOR_HISTORY_MODEL_PREVIEWS.md). Ctrl+Z/Y covers editor actions; auto-centre uses minimum Y. Model settings are beside Materials; lights stay in Modifiers. Cargo is a viewport-only resource model driven by native script records; export generates per-variant main/side DDS previews (256x256/256x128), default configurable FOV20. Exact native camera matching and live WinUI acceptance remain owner checks. See newest STATUS entry for build/install evidence.

Latest scoped editor work (2026-10-19): OBJ/textures followed by rigid pivot/axes and native vehicle lights. Consult the newest entries in STATUS.md and [EDITOR_VEHICLES.md](EDITOR_VEHICLES.md) before older checkpoints below. Owner performs live UI/game tests; do not use desktop automation. Preserve version 0.2 and the one combined archive.

Owner revision 2026-10-20 supersedes the old modal-only pivot and single-material workflow: [EDITOR_02_REVISION.md](EDITOR_02_REVISION.md). Native/PBR variants, interactive gizmos/four light slots, object split/rename, two save commands and encrypted portable .aur projects are the new editor contract. Check STATUS.md for actual build/install evidence.

Read [AGENTS.md](../AGENTS.md), [PROJECT_MAP.md](../PROJECT_MAP.md), [docs/STATUS.md](STATUS.md), [docs/DEVELOPMENT.md](DEVELOPMENT.md), [docs/WORKSHOP_UPDATES.md](WORKSHOP_UPDATES.md), and this document. For the next UI work, apply the active [UI design standard in Av1ra/GameData](https://github.com/Av1ra/GameData/blob/main/WORKFLOW/UI_DESIGN.md). That is the owner's canonical UI standard based on 17 visual references; do not substitute generic template UI.

**Last implementation checkpoint**: `ef780f8a41f129d2443f79e2099e25bb8672f8cf` ("Add reversible Steam launch bridge for Aurora Launcher and preserve game executable"). Windows GitHub Actions [run 39171012901](https://github.com/Av1ra/Aurora/actions/runs/39171012901) finished **success**, including core checks, package build, native/Steam entry fixture tests, signed bundle checks, and artifact upload. Temporary build artifact ID `12152597565`, containing `Aurora-Setup.exe` and its adjacent ZIP, is approximately 96.7 MB as a combined Actions artifact and expires 2026-10-25. Link: https://github.com/Av1ra/Aurora/actions/runs/39171012901/artifacts/12152597565 . This proves automated checks, **not** that the bridge has been launched and accepted by a real Steam client.

## User decisions that MUST survive future edits

- Names: **Aurora Launcher** and **Aurora Editor**. No version suffix in either application's UI title/name; product version remains 0.2 internally.
- Ship Launcher and Editor **together**, in exactly **one distribution ZIP** `Aurora-0.2-win-x64.zip`, plus a small adjacent `Aurora-Setup.exe`. Never reintroduce duplicate "Full" ZIP, separate Editor ZIP, duplicate .NET runtime, or extra standalone module ZIPs. One bundled Editor at `app/Editor`.
- Default destination `SovietRepublic/Aurora`. Installer's folder picker appends `Aurora` exactly once; game directory stays selectable. Launcher may be placed elsewhere, provided the saved game and install paths are consistent.
- Retain game-file ownership boundaries: avoid removing unknown or manually changed user/game files. Do not silently delete the original game entry.
- User expressly disallows the middle-dot separator character in prose or UI. Keep functional, strict, compact WinUI, separate stage badges for Alpha/Beta, and avoid empty cards, giant spacing, neon, glassmorphism, generic "AI slop". Improve underlying workflows, not just colors.
- Preserve FOV module as a separate independently signed module, shown in Settings as **Изменение угла обзора** with an **Альфа** badge, enabled control and angle slider. Do not imply FOV is known to work with all game executable builds.
- GitHub is the canonical durable store: push code, test changes and checkpoint docs to `main`; do not rely on local Windows paths or a chat summary alone. Do not publish Releases or modify the public version without the owner's approval.

## Implemented subsystems and exact files

### One bundled Launcher + Editor

- `build.ps1`: publishes shared WinUI/.NET app under `app/`, builds the Editor module and its Python backend, signs the Editor manifest, copies the Editor into `app/Editor`, and creates **one** `Aurora-0.2-win-x64.zip`. Adds `Aurora Launcher.exe` and `Aurora Editor.exe` as separate root entry points. Generates signed `aurora-update.json`, `SHA256SUMS.txt`, and adjacent `Aurora-Setup.exe`. Development CI generates a temporary publisher key and never uploads the private key.
- `Aurora.Setup.Native/Entry.cpp`: native Launcher, Editor, and Steam bridge entry points. `build-native.cmd` compiles the native entries. They share `app/Aurora.Launcher.exe` and WinUI runtime.
- `Aurora.Launcher/EditorHost.cs` verifies `app/Editor/acode.lnchr`; Editor is already bundled. No separate Editor download for the current combined 0.2 package.
- `Packaging/test-package.py` and `Aurora.UpdateTests/Program.cs` ensure exactly one zip, matching Setup hash, root entry points, bundled signed Editor, and no redundant package copies or publisher secret.

### Steam Play entry bridge

- When the user installs Aurora and selects a valid `SovietRepublic` game folder, `Aurora.Setup.Native/Setup.cpp` **once** preserves `SETUPAPPLICATION SOVIET.exe` at the exact filename `SETUPAPPLICATION SOVIET.exe_orig` and installs the small bridge as `SETUPAPPLICATION SOVIET.exe`. The packaged bridge is `app/Aurora.Steam.exe`.
- The bridge delegates to Aurora's actual WinUI executable under `Aurora/app/`, or a previously saved custom Aurora path if configured for the same game; if Aurora is absent, it attempts the preserved original. It waits on the spawned process.
- The installed bridge's SHA-256 is recorded at `Aurora/steam-entry.sha256` to protect the preserved original and refuse to overwrite an unexpected Steam-restored/changed executable. A repeat install updates the bridge without duplicating or overwriting `_orig`.
- Explicit restoration: `Aurora-Setup.exe --restore-steam` from the matching current Setup with saved install/game paths; close game/Steam first. The native fixture helpers `--steam-hook-test <app> <game>` and `--steam-restore-test <app> <game>` exist for CI and should never operate on a real game in tests.
- **Not yet verified:** that Steam actually starts `SETUPAPPLICATION SOVIET.exe` for the user's installation and correctly presents Steam state, overlay and game launch. This is a manual test prerequisite, not proof from CI. Do not claim it works in-game until observed. Steam's file validation may restore its managed executable: ensure recovery is supported.

### Workshop module detection and updates

- Hardcoded allowed Steam IDs: `Aurora.Core/SteamDiscovery.cs`; current PBR ID `3822550289`. Allowlisted payload paths: `mods/m.dll`, `mods/pbr.dll`, `mods/glass_default.dds`. Steam library target game AppID `784150`. Also scan creator layout `media_soviet/workshop_wip/<SteamID>`.
- A ready Workshop payload has `aurora-module.json` at the Workshop ID root, with fields `Schema`, `SteamId`, `Name`, `Version`, `ReleaseDate`, `Files`. Author manually edits **version/date**, never hashes. Example: `docs/examples/aurora-module.json`.
- `Aurora.Core/WorkshopUpdates.cs` checks during Launcher startup and explicit rescan. Missing descriptor or incomplete file set produces no update popup. When complete, compares installed receipt `game/mods/.aurora-workshop/<SteamID>.json` against Steam version, date and payload bytes. A newer version, date-only change, changed file list or changed bytes can prompt the UI comparison. Do not offer a downgrade.
- The dialog and conditional main-screen offer are in `Aurora.Launcher/App.cs`. Installation is confirmed by the user; refuse if game is running. Installs into game `mods`, removes only obsolete files tracked as Aurora-managed **and unchanged**, keeps user INIs and unknown files. Transaction snapshots are **temporary** and cleaned up after success or handled failure. No permanent module backups are desired. This is different from the older independently signed module installer.
- Registered Steam ID filters discovery; it is **not** cryptographic verification of Workshop DLL origin. The independent signed `acode.lnchr` module pipeline is separate. Do not silently trust arbitrary executable payloads.

### Known limitations and deferred proposals

- Native proprietary PBR renderer DLL content is not in this repo's developer sources by default. Do not assert the graphics renderer or 3D material fidelity is verified in game; `-NativeDist` is a separate opt-in build input.
- FOV end-to-end visual acceptance with `SOVIET64.exe` is still pending.
- `dev.code`, per-build gated GitHub dev channel, user-imported custom modules, GitHub module marketplace, and a universal module manager are **ideas only**, not delivered. Plan: `docs/FUTURE_MODULE_SOURCES.md`. In any future architecture do **not** silently split the currently bundled Aurora Editor from the main installation.
- GitHub Releases publishing is not part of the normal CI. Actions artifacts are temporary and not stable release URLs.

## Reproducible Windows build

Prerequisites: Windows + .NET 10 SDK + Visual Studio C++ x64 build tools and Windows SDK + Python 3.12, NumPy, Pillow, PyInstaller.

```powershell
python -m pip install numpy Pillow pyinstaller
./build.ps1 -Development

# Read output directory from artifacts/latest-build.txt
dotnet run --project Aurora.Tests -c Release
python Packaging/test-native.py "<BUILD>/package/app/Aurora.Installer.exe"
python Packaging/test-package.py "<BUILD>/output"
dotnet run --project Aurora.UpdateTests -c Release -- "<BUILD>/package/app/Assets/publisher.pem" "<BUILD>/output"
```

CI workflow: `.github/workflows/build.yml`. For a new commit, require PASS for all four checks, then use **that commit's** uploaded artifact. Do not hand out the prior build as if it contained new changes. Never commit private signing keys.

## Manual acceptance matrix to obtain from the owner

| Case | Expected PASS | Evidence to request |
| --- | --- | --- |
| Setup choosing custom installation path | `Aurora` appended once, no duplicate folders | Screenshot of both path fields |
| Single combined archive | One ZIP plus adjacent Setup, Launcher and Editor both open | Extracted file tree + screenshots |
| Steam-original preservation | `SETUPAPPLICATION SOVIET.exe_orig` exists and stays unchanged on reinstallation | Screenshot/file hashes |
| Steam Play button | Opens **Aurora Launcher** when the bridge is installed | Steam launch screenshot or short recording |
| Launcher "Launch game" | Starts `SOVIET64.exe` successfully, including after Steam bridge use | Process list/game screenshot, diagnostics if failure |
| Steam original restore | Explicit restore returns original executable without losing it | Before/after hashes |
| Empty registered Workshop folder | No update dialog or permanent empty ID card | Home screenshot |
| Workshop update present | Dialog shows local and new version **and date**; install once, no second prompt | Before/after screenshots and release JSON |
| Bundled Editor | `Aurora Editor.exe` starts and uses embedded editor assets | Editor screenshot |
| FOV | Settings badge + slider, and actual game FOV response when supported | Settings and game screenshots |

Tests that have not been performed by a human are **PENDING**, not PASS. Record exact failing steps, Windows/Steam game version, paths, messages, and logs. Protect private Steam account credentials and other personal data.

## Next developer task: UI defects

After Steam entry acceptance, audit Aurora Launcher and Aurora Editor workflows with the owner-provided screenshots and the [GameData UI design standard](https://github.com/Av1ra/GameData/blob/main/WORKFLOW/UI_DESIGN.md). Start with functional/navigation/spacing/label problems and a real screenshot inventory. Do not invent benchmark numbers or feature cards. Preserve current module discovery, installer, FOV controls, language behavior and update dialogs. Design and test purposeful clickable controls, active/disabled/loading/error states and keyboard navigation; then build in WinUI, gather side-by-side before/after screenshots and update CI tests. Maintain compact Russian/English labels. Do **not** use the forbidden middle-dot character in UI text or documentation.

## Checkpoint discipline

This file is a factual handoff, not a claim of a successful real-world Steam test. Before any further work, re-read `main` HEAD, confirm all code paths and the most recent Actions run, then work on top of the latest commit. If a previous session supplied a temporary Actions link, verify it is not expired. Append verified results and remaining limitations to `docs/STATUS.md`, update this handoff only when architecture/tests change, and push the work to `main`.

Current functional WinUI audit and its verified/pending checks: [UI_AUDIT.md](UI_AUDIT.md). Latest implemented UI pass is 1874bcb; permanent-key packaging checks and empty-editor smoke passed. Continue from its pending model-loaded and constrained-width matrix; do not treat source review as GUI acceptance.

## Autonomous UI checkpoint, 2026-10-19

Implementation 84d417f passed Windows CI run 39176604509, local core/viewport checks and production package checks; its production-key build is installed locally. Read UI_AUDIT.md for the current compact implementation/verification/pending matrix. The user explicitly authorized continuing autonomous WinUI improvements until stopped, so pending real Steam/game acceptance does not block independent UI work. Product remains 0.2, one combined archive, no new release publication. Next concrete findings: WebView user-data folder currently pollutes app/, language changes across already open instances, and native pane compact behavior at constrained widths. Preserve all signature and Workshop compatibility guards.
