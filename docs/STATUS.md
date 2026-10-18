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
