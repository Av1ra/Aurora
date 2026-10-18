# Aurora 0.2 — current handoff

**Development only. No releases, drafts or release tags. Do not change 0.2 until the owner requests it.**

## Implemented

- Root contains Aurora.exe, Aurora.Editor.exe and app/. Shared .NET/WinUI files stay in app; the optional editor does not duplicate that runtime.
- Separate adjacent ZIP and native installer (~390 KB). Installer checks the archive's pinned SHA-256, validates paths and extracts it. Default target is SovietRepublic/Aurora, detected from the saved game path or Steam libraries. A different destination is supported.
- Main window title is Aurora. Sidebar is 200px. Home has game launch, game/mod settings, Workshop and direct Find mods. No repeated Aurora body heading, no Game folder / Mods folder buttons, no instruction to search in Settings.
- Editor card has only its name and Download/Open button. No editor feature redesign was done. The standalone editor entry delegates to the optional signed module.
- Language popup has plain items without a radio dot. Explicit footer items translate to English / Settings or Русский / Настройки; they do not use the OS-localized built-in Settings label.
- Version messages include 0.2, and alpha console hint is Open in game: ~ / Открыть в игре: ~, without a separator. The console is 520x260 and has no launcher Open console button. It starts hidden and toggles only for the newly launched game's foreground window (or the console itself). Replies remain In development.
- Download body copying has a bounded cancellation token. Missing public releases are handled without a fake successful editor installation.
- English player/release description is prepared in RELEASE_DESCRIPTION.md. Future changelogs belong there after publication is approved.

## Verified

- Windows launcher/editor/native build and package checks pass locally.
- Core tests pass. Earlier NMF/export coverage is documented in archive/VALIDATION-0.1.md; editor algorithms were not redesigned here.
- Signature/SHA-256 verification and extraction pass for main and editor ZIPs.
- Native custom-path, Unicode, file-preservation, exact-hash retirement and locked-file rollback fixtures pass.
- Adjacent archive extraction passes; missing or modified ZIP is rejected.
- A GitHub-hosted Windows build passed, including ephemeral development signing and artifact packaging. CI has contents:read and never publishes a release.
- Installed root layout verified: only two EXEs and app/. About 173 MB without optional editor. Player delivery: ZIP about 70 MB plus installer about 390 KB; optional editor ZIP about 29 MB.
- Final home UI inspected. English / Settings footer was inspected in the updated UI. Physical Ё/~ opened the compact console in an isolated test window, confirmed by the owner; the automation's synthetic character input did not reproduce a physical key press.

## Files and repository cleanup

- 25 known working Downloads items were moved to a local archive with MOVE-MAP.json. Personal files and unrelated programs were preserved. Downloads now has a single Aurora-0.2 folder containing the matching installer and main ZIP.
- The old LocalAppData application folder was archived after migration into the game folder; preferences and publisher key were preserved.
- Main is the only remote branch. Superseded PR #1 is closed. Old public releases and their tags were removed after local archival. Previous notes are under archive/; unused installer code is under ../legacy/ and is not shipped.

## Remaining boundaries

- No public release exists, so the Download editor button cannot download production 0.2 yet. This is intentional. The future approved release must include both signed metadata and addon. For local development, extract the matching addon Editor directory into app/; keep the public key from the same development build. Do not bypass signature checks.
- No full real-game/F9 smoke test was performed in this packaging/UI task. Physical tilde was validated in a test window, not by claiming a new renderer acceptance.
- No Authenticode certificate. Do not disable Defender or add exclusions. Package ECDSA signatures are not Windows publisher certificates.
- Bounded download handling is implemented; a full live upgrade through a future production release remains to be accepted once the owner authorizes publication.
- Private graphics-runtime internals are separate from Aurora and are not required to build launcher/editor source in this repository.

Start the next task from ../PROJECT_MAP.md, then DEVELOPMENT.md and the code for the relevant area. Work directly in this repository; local drive paths and old chat history are not prerequisites.

## Authorized preliminary release 0.2

The owner explicitly approved publishing test GitHub Release v0.2. Full package includes the editor plus separate PBR and FOV Changer modules; no texture packs. FOV is not embedded in the launcher: only generic schema-2 slider/toggle rendering is in the UI. The signed FOV module asks for Settings placement. Without that module, no FOV controls appear.

Fixed language root cause: translating the internal NavigationViewItem presenter TextBlock replaced its template binding. Navigation labels now remain owned by their content controls and are translated explicitly. Home image removed; Workshop moved to Game and modules; only Home searches for modules; Settings saves the game folder without searching; Application heading removed.

FOV uses the researched SovietCDK table but requires exact executable SHA-256 296644a9f207d609031fc2ae73fed2dcb34619a1d55a35d1c7b51965ce6841b8 and a unique matching read-only table. Offline current executable match and synthetic ambiguity/truncation tests pass. Real-game visual acceptance is pending and must remain stated in prerelease notes. Unsupported executables are not modified.

Preview verification: Home now shows both signed modules without a screenshot; the editor is already available in the full package. Settings has no Application heading or duplicate search, and Workshop is in Game and modules. Forward English/Settings was visually inspected, and returning Русский/Настройки was observed in the same running window. Numeric controls remain generic; only a discovered settings-placement module creates them. Pending: actual in-game visual FOV acceptance.

## Latest handoff: compact rows and Workshop refresh

Version remains 0.2. Launch game exists only on Home; removed from Graphics. Feature cards use smaller padding, and Graphics/beta lists have tighter spacing.

The Home action checks module updates by rescanning signed acode.lnchr manifests in Steam Workshop content/784150 across detected Steam libraries. Steam itself downloads subscriptions; Aurora does not impersonate the Steam updater. Workshop wins an equal-version tie against the bundled fallback; newer valid versions win normally. Before launching, Aurora repeats this scan and installs changed verified payloads while preserving INI settings. Signature and SHA checks remain mandatory. FOV remains a separate module.

Continue from PROJECT_MAP.md. Source changes are pushed to main for the next chat. Do not confuse earlier local package builds with this latest source revision; rebuild before publishing a binary. The owner authorized a preliminary v0.2 release, but no claim of publication is made by these notes.

## Installer browse folder correction

The launcher installation Browse button now interprets a selected directory as the parent and automatically appends `\\Aurora`. Selecting an existing directory named Aurora (case-insensitive) does not produce `Aurora\\Aurora`. The game-folder picker continues to set the selected game directory and suggests its Aurora subfolder for the launcher. The Windows native installer test includes path suffix and duplication checks. Keep version 0.2; rebuild and validate the GitHub Actions artifact for this commit before sending a new installer.

## Workshop ID discovery and FOV UI update

Version remains 0.2. The independent signed FOV module now uses the visible name `Изменение угла обзора`, a separate `Включить` switch, the original angle slider and a signed metadata `Stage=alpha` badge. All settings-only modules support an optional stage badge.

Workshop IDs can now be registered directly in `Aurora.Core/SteamDiscovery.cs`. ID `3822550289` (PBR) declares `mods/m.dll`, `mods/pbr.dll`, `mods/glass_default.dds`. On launch, Aurora checks both published Steam content directories and the shown `media_soviet/workshop_wip/<ID>` creator layout, reports complete or incomplete detection on Home and keeps signed Aurora module discovery separate. Raw Workshop DLLs are not installed or launched without the existing signature verification. Added tests cover the registered ID, missing files, published-source preference and unknown IDs. Rebuild on Windows and validate the CI package; no real-game PBR verification is claimed.

## Registered Workshop release updates

Aurora 0.2 now supports optional release descriptor `aurora-module.json` inside a registered Steam ID folder. Until that descriptor and all required files exist, no update popup appears. On launcher startup (after the window loads) and on manual module refresh, metadata is checked in Steam content and `media_soviet/workshop_wip`, and compared with the per-game receipt under `mods/.aurora-workshop/<SteamId>.json`. The confirmation dialog shows current version/date and Workshop version/date; first install, upgrades and changed bytes are offered, but downgrades are suppressed. Copies are restricted to the ID's hardcoded file allowlist, verified with computed SHA-256, backed up and rollback-protected; the game must be closed. User INIs are untouched. No manifest hashes need to be typed by the mod author. The editable file format and expected paths are documented in `docs/WORKSHOP_UPDATES.md` and `docs/examples/aurora-module.json`. CI compilation and real-game visual acceptance should be checked before issuing a binary.

## Workshop updater cleanup policy

The registered Workshop updater no longer creates permanent `mods/aurora-backups/workshop-...` directories. It stages the entire new payload and minimal old-file snapshots only in a disposable transaction directory under `mods/.aurora-workshop`, then cleans that directory after success or handled failure. The only permanent metadata is the small installed receipt. A newly published `aurora-module.json` may omit obsolete items from its `Files` array. Aurora retires an omitted file only if it was listed in the installed receipt, is in the registered allowlist and still matches the hash previously recorded by the updater. Modified and unrelated files are left untouched. New file lists are supported without deleting old registry IDs. Tests check replacement, selective cleanup and lack of leftover transaction backups. Legacy signed-module installers and preexisting archive folders are not silently cleaned.

## Minimal Workshop check (current scope)

Only complete registered Workshop releases with `aurora-module.json` cause an update comparison. Missing file(s) during Steam download are silently ignored. Installed vs available version and release date are displayed in the confirmation dialog; a date-only release change also prompts and is persisted. The Home screen no longer contains permanent empty Steam ID entries; it shows a Workshop update card only while an offer is available. The registered ID remains `3822550289`. Additional local-user module imports and GitHub module delivery are **planned, not implemented**; see `docs/FUTURE_MODULE_SOURCES.md`. CI needs to pass before distributing the new build.

## Combined Aurora Launcher + Aurora Editor distribution

The owner clarified that Launcher and Editor always ship together, with no duplicate archives. Packaging now emits one `Aurora-0.2-win-x64.zip` containing root `Aurora Launcher.exe`, `Aurora Editor.exe` and shared `app/`, with signed editor material under `app/Editor/`. The sole adjacent `Aurora-Setup.exe` pins the SHA-256 of that same ZIP. `aurora-update.json` signs the same one archive. Removed generation of separate Full, Editor and module ZIPs. Main launcher window and editor window are titled `Aurora Launcher` and `Aurora Editor` and the editor button verifies the bundled module before opening it, with no download flow. Windows CI should verify one ZIP, the two app entries and signed embedded module before distributing. Existing earlier installations may still retain legacy root executables, which are not silently deleted if ownership cannot be proven. Product version remains 0.2.

## Steam launch through game setup executable

Installer now preserves the official `SovietRepublic/SETUPAPPLICATION SOVIET.exe` as `SETUPAPPLICATION SOVIET.exe_orig` and installs a small Aurora Steam bridge under the original filename. The bridge starts the real bundled `Aurora Launcher` host from `SovietRepublic/Aurora/app/` or, for custom installations, the saved `install-root.txt` location when the saved game path matches. It waits while the launcher runs so Steam can observe that process. If Aurora is missing, the bridge falls back to the saved original executable. Reinstall only replaces its own bridge after verifying a local hash marker; it never overwrites `_orig`, nor does it overwrite an unexpected Steam-restored executable. The app updater refreshes the bridge where previously installed. `Aurora-Setup.exe --restore-steam` explicitly restores the original after validating the recorded bridge hash. Tests cover initial backup, repeat setup, unexpected file modification and restore. This is a native Windows/Steam entry substitution, not an injection method. Actual Steam client integration and Steam overlay require on-PC acceptance testing.

Packaging remains **one** `Aurora-0.2-win-x64.zip` containing `Aurora Launcher.exe`, `Aurora Editor.exe`, the shared WinUI runtime and internal bridge, plus adjacent `Aurora-Setup.exe`. No Full/Editor/Module duplicate archive is generated.

## Codex handoff checkpoint: 2026-10-19

Verified repository state before this documentation update: `main` HEAD `ef780f8a41f129d2443f79e2099e25bb8672f8cf`. Windows CI run `39171012901` concluded `success`. Uploaded Actions artifact `12152597565` was available at the checkpoint (7-day retention, expiring 2026-10-25). Tests passed for native Steam bridge fixture (initial preservation, re-install, unexpected replacement refusal and explicit restoration), package integrity, and one bundled Launcher + Editor ZIP. There is **no verified real-game Steam Play acceptance or in-game FOV visual acceptance yet**.

Created `docs/CODEX_HANDOFF.md` as the primary continuation brief and linked it in `AGENTS.md`, `README.md` and `PROJECT_MAP.md`. The UI redesign standard is `Av1ra/GameData/WORKFLOW/UI_DESIGN.md`. Do not split Aurora Launcher and Aurora Editor into separate installers, create duplicate ZIPs, or introduce a version into product display names. Next work: owner-led testing of Steam substitution and functional UI audit of both apps with PASS/FAIL evidence.

## Local continuation: publisher compatibility and deterministic tests (2026-10-19)

Fetched main through 86c4de5. The installed Actions artifact used DevelopmentSigning=true and its ephemeral public key rejected the production-signed Workshop item 3822550289. The item's current acode.lnchr signature and all three Payload/mods file hashes verify against the repository's pinned production public key. Do not bypass signature checks or re-sign Steam files with a CI key. Production build now fails before packaging if the existing private signing identity differs from the pinned public identity; no private material is emitted.

Workshop fixtures previously searched the workstation's real Steam libraries, causing a local failure hidden on a clean CI runner. Discovery and update scanning now accept optional explicit root collections; normal runtime discovery is unchanged. Tests supply isolated fixture roots and all core checks pass locally. These optional roots are a test seam, not a new UI/source permission.

The legacy signed Workshop layout remains Payload/mods and is compatible with the constant publisher key. The newer registered update workflow separately expects mods/ plus aurora-module.json. Do not claim an actual registered update was tested against the current item without that descriptor. Steam bridge and visual FOV acceptance remain pending.

Verification and local installation: production package built from e65ca5d with DevelopmentSigning=false and clean source metadata. Core tests, native installer/Steam bridge fixtures, combined archive signature/SHA/editor/module checks, and adjacent ZIP tamper/missing-file tests all passed. Installed the full package into the existing game/Aurora directory using the transactional installer copy path; this did not alter the Steam entry bridge. All 570 staged application files matched installed hashes. Opened Aurora Launcher: Graphics and FOV were discovered and the previous Invalid publisher signature message is absent. Opened bundled Aurora Editor successfully; viewport and model/LOD/material controls appeared. No real-game model edit, Steam Play launch or visual FOV acceptance is claimed. No public release was published.
