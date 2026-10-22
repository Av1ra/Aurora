# Aurora 0.2: Codex continuation after ChatGPT UI/PBR review

**Date:** 2026-10-22, evening. **Owner is moving work to Codex.**

This is an **incremental supplement** to the existing `handoff/2026-10-22` baseline, not a replacement for `01_CURRENT_STATE.md`, `08_DECISIONS_FROM_CHAT.md`, `06_DESIGN.md`, `12_NEXT_AGENT_PROMPT.md`, or the original package manifest. Start by reading `AGENTS.md`, this report, and the established handoff. Current owner instruction: the application is fundamentally working; make targeted improvements, preserve features, avoid rebuilding from scratch.

## Repository identity and exact checkpoints

- Canonical app repository: **`Av1ra/Aurora`**, base branch `main`, observed SHA `b62678e6c2fd93bb12b414d98b66bb9c806bf0d8`.
- Last **production-signed, owner-installed base** described in original handoff: source `c45db8149f0b91f82688cdb7c0897bee0de75ce7`, build `build-c111dafe03994c7d9de2d17e0adcb5c6`, version **0.2**. Do not treat later documentation commits as installed runtime.
- Native PBR/Graphics canonical source repository: `Av1ra/GameData`, path `SovietRepublic/MODS/PBR.dll/SRC/Product`. Historical pinned source in initial handoff is `c72d027dd4449cda05da10b56850657f74d10561`; check its **current main** separately before rebasing.
- The `RESAURO.zip` used in the conversation is an owner-supplied offline handoff archive. Its content includes Aurora and private Graphics source. Do **not** upload that archive or private sources into public locations.
- Do not change Aurora public product version from **0.2** without owner permission.
- Owner now wants **only ready executable packages** as user-facing deliverables: Launcher.exe, Editor.exe, relevant compiled modules, installation script/installer and dependencies, not a source-code ZIP. Store source and report on GitHub as usual.

## PRs created during this conversation (all unmerged / draft at report time)

| Item | Repo and URL | Head commit | Scope | Verification / caveat |
| --- | --- | --- | --- | --- |
| Editor UX and tools | [Aurora PR #3](https://github.com/Av1ra/Aurora/pull/3) | `7ac62f7befc160558045e9e4aa01ef245774e54e` | Editor/UI adjustments below | [Windows CI run 39828777119](https://github.com/Av1ra/Aurora/actions/runs/39828777119) **SUCCESS**; no in-game/live GUI acceptance |
| App robustness and updater | [Aurora PR #2](https://github.com/Av1ra/Aurora/pull/2) | `e3327ddff37156df97fb7b7c77078d587ec751fd` | WebView2 reporting, Workshop cleanup, signed BuildId update metadata, native Setup cleanup/timeout | [Windows CI run 39820918691](https://github.com/Av1ra/Aurora/actions/runs/39820918691) **SUCCESS**; separate branch, not automatically included in PR #3 |
| Native road PBR | [GameData PR #11](https://github.com/Av1ra/GameData/pull/11) | `fc53e25811c6c4c5b74af0f7a4b88ab86c6dca3b` | Normal/roughness UV agreement and safe R/AO/height packing detection | [Graphics CI run 39821071468](https://github.com/Av1ra/GameData/actions/runs/39821071468): **native compile and tests succeeded**, overall run **FAILURE** only at artifact upload due to GitHub Actions storage quota; no publishable DLL delivered |

**Do not describe these PR changes as merged into `main` or production-signed.** PR #3 and PR #2 were developed independently from the same Aurora baseline; merging both requires resolving overlap in `Aurora.Editor/App.cs` and re-running validation.

## Owner's Editor UI defects, requested behaviors and actual work

Owner supplied screenshots of: an oversized Animation bottom panel covering a dump truck, incorrect/missing single keyframe loading, jerky rotation gizmo, duplicate cargo allowances/class controls, missing dimensions, wheel test defaulting to one wheel, horse/driver meshes absent from preview, frequent model reload on moving controls, partial texture loading, wireframe obscured by shaded model, and the exception banner `Sequence contains more than one matching element`. Owner additionally clarified **four distinct blinkers**: front-left, front-right, rear-left and rear-right (not merely two left/right indicators).

Changes present in **PR #3** (review actual code before further modifications):

1. `Aurora.Editor/EditorAnimation.cs`: smaller bottom timeline, collapsed advanced settings and curve graph; initialize editor selection from a track's single existing keyframe. CI passing does not prove this solves all native NAF/animation import or playback cases.
2. `Aurora.Editor/Assets/gizmo.js`, `EditorGizmos.cs`, `App.cs`: keep local drag authoritative during pointer interaction, accumulate rotational angle continuously across angular wrap, reduce full `Display()` calls for ordinary transport point edits. Verify smooth rotation, undo, selection synchronization and model persistence under mouse drag.
3. `Aurora.Editor/EditorGizmos.cs`: four separate indicator-point commands and automatic placement of missing four emitters; preserve `LIGHT_BLINKER` authoring format. **Important**: code-level placement is not proof that game engine blinkers are independently recognized as front/rear. Validate left/right blink cycles and model script output for real vehicles. Pay attention to coordinate convention and forward axis.
4. `Aurora.Editor/EditorEffects.cs`: wheel testing defaults to `Все колёса` and allows steering if any axle steers.
5. `Aurora.Editor/EditorModelSettings.cs`: add model dimensions in metres, clarify vehicle category versus game role, hide redundant resource selection and rename cargo allowance control. Check `TYPE`/role mapping and preservation of all native script fields.
6. `Aurora.Editor/EditorMenus.cs` and `Assets/viewport.html`: top-level `Настройки` menu, toggled wireframe, selectable RGB wireframe colour, hide solid triangle pass while wireframe-only preview is active. Check correct behavior with glass, attached components and picking.
7. `Aurora.Editor/App.cs`: independent try/catch per preview texture; native texture fallback if authored PBR asset is missing; do not abort remaining map loads. It cannot supply a truly missing game file.

**Items still open / not actually implemented in PR #3**:

- **Horse and driver visible geometry**. There is currently no bundled horse or driver model asset for the Editor preview. Script points alone cannot produce 3D meshes. Locate the actual files and material dependencies in the owner's installed game (possibly DLC content); only request narrow file/package excerpts if remote access is unavailable. Keep native game file originals untouched and honor licenses.
- **`Sequence contains more than one matching element`** seen with an untextured blue vehicle: determine exact `Single()` call/duplicate identity and reproduce with the triggering `main.nmf`, `material.mtl`, `script.ini`; this crash/error was NOT fixed in this batch.
- **All missing textures**: fallback helps recover available maps, but missing assets, unsupported DDS, incorrect MTL references and nonmatching material names still need targeted triage.
- **Model reloading for every edited property**: partial optimization covers gizmo and ordinary point editing; evaluate all settings, animation and material update paths before claiming complete elimination.
- **Visual timeline interaction and animation key playback**: source improvements only; owner screenshots are from older build and no new owner screenshots were captured after changes.
- **PBR normal/roughness road triangle artifact**: GameData PR #11 is source-only, does not prove fix in owner screenshot, and does not automatically supply missing authored road DDS maps. Do not change road native colour/terrain assets gratuitously.
- **Noisy UpdateService/Workshop/Setup fixes** are in PR #2 rather than Editor PR #3.

## Binary deliverables and what was actually packaged

- Original signed production Aurora package is retained at `handoff/2026-10-22/package/` with `Aurora-0.2-win-x64.zip` and adjacent `Aurora-Setup.exe`. Validate LFS and package SHA256. This is the **stable baseline**.
- Windows development build of PR #3: [Actions run 39828777119](https://github.com/Av1ra/Aurora/actions/runs/39828777119), artifact ID **12447624189**, name `Aurora-0.2-Launcher-and-Editor-2813570eb78e8f402abe7f4724cb586f013e19f2` (GitHub Actions retention 7 days; may expire). Contents compiled Launcher/Editor and dependencies; **development signing**, not the production publisher signature.
- In the chat, a binary-only archive named **`Aurora_Editor_Updated_Complete.zip`** (about **93.1 MiB**) was assembled from PR #3 CI output plus unchanged original production-signed Graphics and FOV module contents. It included a `УСТАНОВИТЬ.cmd` copier with backup and a README. Tests during assembly: ZIP CRC, EXE MZ headers, shader module manifest SHA256 and compiled JS markers; available Windows CI tests passed. This particular combined archive was delivered as a **conversation attachment**, **NOT** committed, uploaded as a GitHub Release, or installed on the owner's Windows PC by the assistant. It is **not a newly production-signed full Aurora release**.
- `УСТАНОВИТЬ.cmd` in this conversation bundle copies files to `SovietRepublic/Aurora` with a backup; it does not itself prove actual on-PC acceptance. For a next official release, use the canonical Setup packaging/signing workflow and verify Steam bridge.
- A separate original `Aurora_0.2_Complete_Installer.zip` was also delivered earlier in this chat, containing production-signed stable binary package and adjacent Setup. That one predates the PR #3 editor changes.

## Required next actions for Codex

1. Start from **Aurora main** and inspect **draft PR #3** (do not overwrite it blindly). Prioritize reproducing screenshots from the owner and verifying compact timeline, existing single-frame animation, gizmo rotation, all-wheel selection, four blinkers and settings menu.
2. Check whether PR #3 plus PR #2 can be safely integrated (both edit App.cs). Keep each candidate reversible. Re-run full Windows .NET build, JS viewport tests, package/signing validation and transport roundtrips before promoting.
3. Locate native driver/horse game models/materials and resolve 3D preview dependencies rather than generating stand-ins. Investigate `Sequence contains more than one matching element` with the user's triggering model. Verify normal/roughness/texture fallback on specific real vehicle assets.
4. Revisit [GameData PR #11](https://github.com/Av1ra/GameData/pull/11) and missing road-map assets; provide a compiled Graphics module **only through authorized signing/release mechanisms**. Never claim CI upload-quota failure is a native compile failure.
5. If shipping a new binary, provide **one complete Launcher + Editor bundle with EXE and required modules, plus small installer**. No source archives to the owner. Preserve backup/rollback, game saves, executable naming/Steam bridge and version `0.2`. Do not publish a new production release or use publisher secret without authorization.
6. Record evidence in a PASS/FAIL matrix. Distinguish **source present**, **CI passed**, **user installed**, and **visually confirmed in game**. The user is the curator: expects a ready installable build rather than acting as the build engineer. New live-game evidence has to come from actual tests.

## Owner design constraints / invariants

- One project per chat; owner moving to Codex now. GitHub remains canonical history; existing chat output should not be considered committed unless linked.
- Strict professional compact UI, no neon/glassmorphism/huge rounded boxes; use `WORKFLOW/UI_DESIGN.md` from GameData and original PSD/layer reference in the initial handoff.
- Do not use the middle-dot text separator in UI prose.
- No new arbitrary version numbers; 0.2 stays constant while build SHA/ID changes.
- Keep a shared app runtime, no duplicate Launcher/Editor packages or duplicated install payload; preserve PBR/Graphics FOV and existing game installation.
- Do not silently substitute photorealistic proxies for real native equipment assets.
- Avoid claiming validated frame rates, full compatibility or bug closure without actual owner/game confirmation.

## Fast bootstrap prompt for Codex

> Continue existing Aurora 0.2 (not a new project). Read `AGENTS.md`, `handoff/2026-10-22/README.md`, and **`handoff/2026-10-22/13_CODEX_CONTINUATION_2026-10-22.md`**. Inspect draft [Aurora PR #3](https://github.com/Av1ra/Aurora/pull/3), [Aurora PR #2](https://github.com/Av1ra/Aurora/pull/2), and [GameData PR #11](https://github.com/Av1ra/GameData/pull/11). The stable installed package is c45db814, not these unmerged PRs. Editor fixes compiled under Windows CI run 39828777119, but no live game acceptance. User wants ready executable packages only, not source ZIPs. Prioritize reproducing Editor UX and fixing remaining real-model issues: driver/horse preview, texture/MTL load, duplicate Single() exception, and animated gizmo semantics. Maintain backups, version 0.2, separate signing and handoff discipline.
