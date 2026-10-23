# Codex handoff: Aurora Launcher + Aurora Editor

**Latest installed stable selection outline:** source `2942b98`, production `build-faba25c654fa4d95af9478999f156762`. GPU external silhouette replaces flashing triangle edges; hover retains selection, interiors stay unchanged, repeated selection does not upload geometry. Owner closed Aurora; all 581 installed hashes and hidden WinUI startup/layout pass. Backup `%LOCALAPPDATA%/Aurora/backups/584233421`. Real WebGL pixel checks pass; live owner acceptance remains separate. See the end of [selection revision](EDITOR_SELECTION_REVISION_2026-10-23.md).

**Latest installed compact selection button:** source `f6c7b8b`, production `build-83be9c5a93a043b99b61917dfbd15c40`. Main-toolbar Window/Crossing icon toggle replaces the extra dropdown row; Shift+O and mode tooltips. Owner closed Aurora; all 581 hashes and installed WinUI startup/layout verified. Backup `%LOCALAPPDATA%/Aurora/backups/583555953`. See the end of [selection revision](EDITOR_SELECTION_REVISION_2026-10-23.md).

**Latest installed selection revision:** [connected-element editing and responsive controls](EDITOR_SELECTION_REVISION_2026-10-23.md). Source `264176f`, clean production `build-d71b30125f2b4bdcabf73c6ebb18ea6e`; 581 installed hashes verified. LMB selection, Alt+LMB orbit, MMB pan, window/crossing regions, selected-element detach, common scrolling dock, mirror action moved, phased beacons and 20 km/h wheel play. 414 Core checks, JS/WebGL, actual WinUI construction/layout, bundle/native/signature/brand checks pass. Live GUI/game acceptance remains pending. Workspace vehicles are labelled Project vehicles and explained separately from mesh objects.

**Latest installed menu grouping:** source `544df8f`, production `build-6d111e407f1e4e9992b4cd1134b27966`. Owner closed Aurora; transactional install and all 580 hashes verified. WinUI construction, package and signatures passed. Main menu groups: NMF/project open/save; OBJ/FBX export/import; undo/redo; settings. Delivery: Downloads/Aurora-0.2-Menu-2026-10-23. Evidence: ignored artifacts/menu-group-install.json. Other functionality remains at the workflow revision below; interactive acceptance remains pending.

**2026-10-23 workflow revision:** [eight-point Editor changes](EDITOR_WORKFLOW_REVISION_2026-10-23.md). Source 47c85bc is installed: production build-dfb3b834fd6d4baf87cc26a22d22af9c, 580 hashes verified, installed hidden WinUI startup and FBX converter checks passed. Adds compact controls, layers, incremental viewport updates and FBX/OBJ scene exchange. DCC/live GUI/game acceptance remains pending.

**2026-10-23 integration checkpoint:** [Editor PR #3 candidate](EDITOR_PR3_INTEGRATION_2026-10-23.md). Source 468d034, production-signed package installed at the owner's explicit request; 572 installed hashes and hidden WinUI construction verified. Includes cargo-panel construction fix and new English labels. PR #2 and Graphics PR #11 are not included. Earlier unmerged-PR statements below describe the prior handoff.

**LATEST CONTINUATION (2026-10-22 evening): [Editor/PBR changes and Codex transfer](../handoff/2026-10-22/13_CODEX_CONTINUATION_2026-10-22.md).** Read this before acting on historical work backlog. PR #3 compiled and passed CI but is not merged or in the original production-signed installed package. User requires executable packages, not source archives.

**Portable consolidated handoff, 2026-10-22: [start here](../handoff/2026-10-22/README.md).** Exact installed package, PSD hidden layers, chat decisions, architecture, build instructions and current gaps. Chronological notes below remain historical evidence; earlier next/current statements do not override later owner decisions.

Checkpoint recorded: 2026-10-19. Canonical repository: `Av1ra/Aurora`, branch `main`. Product version: **0.2**. Identify all test builds by full commit SHA and Actions run ID, not a new public version number.

## Read this first

Editor startup regression fixed and installed: [details](EDITOR_STARTUP_REGRESSION.md), source `c45db8149f0b91f82688cdb7c0897bee0de75ce7`, production `build-c111dafe03994c7d9de2d17e0adcb5c6`. Null WinUI Setter caused constructor failure, previously hidden by Launcher fallback. Removed null Setter; startup errors now have an Editor error window. New real signed WinUI construction/toolbar/opaque presenter check runs during every package build and passed again against installed files without activation. All 572 installed hashes match, bundle checks passed. After owner closed all windows, four zero-window startup-failure orphan processes still held locks; only those exact installed-path PIDs were stopped. No Aurora processes remained after the installed hidden check. This is construction/visual-content validation, not interactive viewport acceptance. Receipt: artifacts/editor-startup-fix-install.json (ignored).

Installed revision 06: source `652fd54881de7b94dfb2239efe2672229ba8e509`, production `build-38301a9813f24ce6af09aaaebe68d237`. Owner confirmed apps closed; installer completed and all 572 installed hashes match. Explicit opaque popup styles disable menu Acrylic; per-wheel component hubs/masks and individual wheel selection replace rotation around mesh origin. Core/JS/real WebGL/WinUI and bundle checks passed before packaging. Downloads also updated. Receipt: ignored artifacts/editor-revision-06-install.json. Live UI/game acceptance remains unperformed.

Toolbar regression hotfix installed: source `96f3c93ad44a650cfa9c33dbeb12144e043ec6a2`, production `build-dcf1ffea299241a08126cfba6888915d`. Revision 05 accidentally omitted adding the toolbar ScrollViewer to the root grid; controls existed but had no visual parent. Restored the missing attachment. Build and native/package/signature/brand checks passed, all 572 installed hashes matched with apps closed. Downloads updated with backup. No live GUI acceptance claimed; the compile-only check had failed to detect this visual-tree omission. Receipt: ignored artifacts/editor-toolbar-fix-install.json.

Latest installed UI correction: [revision 05](EDITOR_REVISION_05.md), source `0e087f5193960a28a090d074e7c4b4e335915cf0`, production `build-1984dd181f694283bfa4d91024be6786`. Fixes busy viewport picking (animation wait), one Mechanisms entry with timeline, equipment sections, separate effects, left/right indicators and correct native positive-X left convention. Water test removed. Installed with apps closed; all 572 hashes matched. Core/JS/real WebGL/WinUI and four bundle checks passed; no live UI acceptance. Downloads updated, previous package backed up. Earlier engine limitations remain.

Latest small UI fix installed: source `11c74eaf700d2a60740af737638ac592b0f1c9ab`, production `build-2912eea89ba84a1798b9cfc4b5d608da`. Editor menu/flyout backgrounds use fully opaque theme brushes, including nested menus and high contrast. Owner closed Aurora; installer passed and all 572 installed hashes matched. WinUI compile, native/package/signature/brand checks passed. No live UI acceptance claimed. Downloads updated with rollback backup; install receipt: ignored artifacts/editor-opaque-menus-install.json. Earlier revision 04 engine limitations remain.

Revision 04 source 106454a36e430f31787404ab32ab67c16752e3c3 is installed, build-81ebaf885f8145b2ba23412af3fd8c5f, all 572 hashes verified. Native/package/signature/brand checks passed. See EDITOR_REVISION_04.md for exact delivered changes and unresolved native dumper/nozzle, attached-part animation and live acceptance gaps. This supersedes older installation statements below; it does not declare all 13 owner requests complete.

Active owner request: [revision 04](EDITOR_REVISION_04.md), 13 editor corrections plus explicitly authorized keyframe animation authoring. Owner answered: native transport actions only, no custom launch conditions. This supersedes the former animation deferral and stopping instruction. Work in progress, not installed; continue implementation and tests without claiming preview-only clips are game-bound animations. Current installed package remains 4af678b. See exact outstanding work and current evidence in revision 04.

Latest installation: owner explicitly requested installation after the report. Production build-fc5ed9b9f2c54f248260172c7c010439 (4af678b3cd0f8c1830f650434823660884d7a68f) is now installed in game/Aurora; all 572 installed file hashes match. Installer completed successfully with apps closed. No launch/game acceptance was performed. This supersedes older not-installed statuses below.

Owner requested winding down and a report. Work is stopped after final delivery; do not resume autonomous development without a new request. Read [the final report](TRANSPORT_SESSION_REPORT_2026-10-22.md). Delivered production build-fc5ed9b9f2c54f248260172c7c010439 is clean source 4af678b3cd0f8c1830f650434823660884d7a68f, Version0.2, verified native/package/signature/brand tests. Downloads/Aurora-0.2 contains the final ZIP and Setup; installed files were not replaced. Documentation commits after that source do not change the package.

Latest continuation (2026-10-22): native material audit/repair, UTF-8 NMF names, active MTL terminators and exact TEXTURE/TEXTURE_MTL roots. See the newest section of TRANSPORT_IMPLEMENTATION_03.md and reproducible Packaging/MaterialAudit. Corpus: 1,073 objects, 3,854 variants; 124 partial objects remain explicitly unconfigured, 18 objects have genuinely missing textures, nine MAIN use unsupported streams. Tests and WinUI module pass. Current source is not installed; owner's two installed Launchers remain open. Continue transport work and package verification autonomously.

Current continuation includes donor-free class/role profiles (88 classes, 22 roles), cabin working frames, diagnostic dumper/mixer poses and shared materials of all linked parts. All seven types roundtrip own OBJ -> portable workspace -> local export without a game donor. Latest delivered, separately verified production is 5278f095c46654d028a2cd9e522182a707b6bf35, build-68282d0ccbb2449eab186edc7912cceb; installed files remain older while the owner's Launcher processes are open. New role/import fixes follow that delivery. Continue broad native material/import checking and self-tests; do not stop or request owner testing at an intermediate package.

Latest source continuation: [multi-object portable transport projects](TRANSPORT_IMPLEMENTATION_03.md), including strict project decoding, self-authored trainsets, native/PBR materials of linked parts and seven-class export checks. Production receipts remain separate; do not claim this source is installed. Continue the owner's autonomous task without stopping at intermediate builds.

Owner explicitly corrected premature stopping: **continue implementation and self-verification autonomously until the requested result; do not end after a package or ask the owner to test.** Economy means targeted work, not stopping. [Batch 02 working state and remaining tasks](TRANSPORT_IMPLEMENTATION_02.md) is the current continuation point. UI input was detected in the launched standalone Editor; no further GUI input was sent after refresh. Core/file/export work continues. Current new source is not yet a newly installed package.

Current implementation: [transport batch 01](TRANSPORT_IMPLEMENTATION_01.md). Owner rejected launcher proposals; do not implement LAUNCHER_REQUIRED_TOOLS.md. Transport code now fixes terminators, cabin discovery/identity, repeated cargo restrictions, multi-argument skill fields, native country/availability preservation and mesh mechanism references. Scope is still incomplete; animations deferred, economical related batches requested. Tests include 1,073 native script roundtrips and bundled WinUI compilation; installed receipt is separate in STATUS.md.

Batch 01 is now installed: source 33631f028e3d1b9f78838b1e5f4bbf17b4c5ca1c, production build-e7ee2cc7907b4f1b9b13ea588919cd50, all 572 installed files verified. Both Downloads/Aurora-0.2 delivery files match. Current packages remain version0.2; no live visual/game acceptance, no Release. Next implementation focus is linked parts/local frames and portable dependencies, not launcher proposals or animation authoring. Preserve the owner's latest token economy request.

Owner approved all [22 additional transport requirements](EDITOR_TRANSPORT_ADDITIONAL_22.md) as important, keeping research caveats and deferred animations. Current discussion moved to [necessary launcher tools](LAUNCHER_REQUIRED_TOOLS.md): twelve source-grounded proposals, not implementation authorization. Existing signature/install/update/conflict checks must be reused. Notable issue: UpdateService compares product Version only, so it cannot distinguish successive 0.2 builds through that contract.

Latest research extension: [22 further transport requirements](EDITOR_TRANSPORT_ADDITIONAL_22.md), distinguishing confirmed source limitations from untested risks/research. Owner approved the previous seven additions as important. Notable new findings: missing cabins catalog root, missing cabin subtype identity, OBJ/MTL data loss paths, mesh rename script references, LOD topology assumptions and skin export. The earlier bbox risk is narrowed: normal pivot baking already triggers bounds rebuilding. No runtime changes.

Latest owner priority: [complete transport authoring requirements](EDITOR_TRANSPORT_AUTHORING_REQUIREMENTS.md). Recent convenience brainstorming, PBR extensions and the in-game sandbox are parked. Animation authoring/playback is a later stage by explicit owner clarification. Current analysis focuses on native transport configuration, linked parts and export, with optional donors. The older 98-item checklist includes deferred work and is not the immediate scope in full.

Owner scope correction: current analysis concerns **transport only**. Start with [the deeper transport audit](EDITOR_TRANSPORT_DEEP_AUDIT_2026-10-21.md) and [98 evidence-linked criteria](EDITOR_TRANSPORT_CHECKLIST_2026-10-21.md). Buildings/maps/scenarios in the older broad report are outside the present task. These are research findings and implementation criteria, not newly delivered features.

Read-only capability audit (2026-10-21): [installed game/editor gaps and prioritized plan](EDITOR_CAPABILITY_AUDIT_2026-10-21.md), with reproducible tools and measured corpus data under `audit-data/`. Findings include unsupported primary NMF files, `$END` insertion, multi-argument skills, native material/DDS gaps and misleading field semantics. The audit does not mean these issues were fixed or a new runtime was installed.

Current owner correction: [task-based vehicle settings, cargo material fix and PSD launcher rows](EDITOR_TASK_SETTINGS.md). This supersedes generic native-field forms and currency-derived country lists. Custom sounds scope is INI import only. Check newest STATUS entry for verified package/install evidence.

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
