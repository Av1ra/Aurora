# Current WinUI audit

Product version remains **0.2**. Current verified implementation checkpoint: **84d417f**. Windows CI run **39176604509** passed. The owner asked for continuing autonomous UI work until stopped; keep native WinUI, one combined Launcher + Editor archive, permanent publisher-key compatibility, and no release publication during this UI pass.

Canonical design standard: https://github.com/Av1ra/GameData/blob/main/WORKFLOW/UI_DESIGN.md . The existing embedded model viewport remains WebView2; no replacement interface framework was added.

## Implemented

- Context-preserving/coalesced module refresh, clickable module rows with catalog versions, compact responsive action groups and wrapping titles/badges.
- Honest update-check states and native nonmodal InfoBar feedback. Translation updates owned values without breaking WinUI template bindings.
- Shared toggle/slider rendering, hidden Beta sections, signed declarative prerequisites, persisted-value restoration on failed writes, and accessible control names/help.
- Launch re-entry/running-process protection and a fixed game-path/module snapshot for each launch operation.
- Signed-runtime/Workshop compatibility checks prevent silent fallback downgrades, resurrection of retired files, or an install offer promising incompatible runtime bytes.
- Full descriptor snapshots detect same-version module refreshes before UI writes or installation.
- Editor empty guidance, selection-aware commands, serialized operations, real processing feedback, source-filename/MTL display, safe picker cancellation and unsaved-change confirmations.
- NMF file-argument/drop routes, native keyboard accelerators, compact PBR scalar rows and collapsible map/advanced sections with filenames and numeric values.
- Explicit viewport selection, object-to-material selection, DPI-aware picking, scene/map request ownership and GPU texture disposal.
- Logical initial window sizes fit monitor work area; alpha console input/send share a row and its output takes remaining space. It remains a tilde-only game feature with In development replies.

## Verified

- Both UI projects compile without warnings.
- Core tests cover signatures, same-version snapshots, module dependencies, install preservation/rollback, Workshop compatibility/retirement and window-size calculations.
- Actual viewport-script boundary tests cover asynchronous texture races, disposal, coordinate conversion and deselection. CI runs them too.
- Combined archive signatures/SHA/editor/modules pass. Adjacent Setup accepts its correct ZIP and rejects missing/tampered ZIPs. Native Steam fixtures passed separately.
- Actual game snow_AMO15 model opened read-only with three NMFs; textured vehicle appeared, LOD2 switched and the PBR dialog opened. The later explicit-selection UI leaves glass disabled until an object is selected.
- FOV switch disabled/re-enabled its angle control while retaining 45; original enabled state restored.
- Invalid game path was rejected without changing saved path. InfoBar initially exposed a binding bug; the corrected build displayed its full error and translated RU -> EN -> RU correctly. Russian preference restored.
- Main window tested at 693px captured width: action buttons stacked and module rows stayed usable. Narrow Editor and multiple real DPI scales are not yet accepted.
- Production-key build 84d417f installed locally; all 570 staged application files matched installed hashes. Matching single ZIP + Setup replaced the old Downloads/Aurora-0.2 delivery, with previous files archived. No public release published.

## Open checks and next useful work

1. WebView2 currently creates Aurora.Launcher.exe.WebView2 beside installed app files. Confirmed on disk. Move its user data to LocalAppData/Aurora rather than polluting install directories; preserve old data until ownership/locks are checked.
2. Check live language consistency between already open Launcher/Editor instances, preferably on activation without a polling timer.
3. Consider native NavigationView Auto compact behavior at constrained widths; full manual pane still occupies 200px in the 693px test.
4. Complete the corrected wider PBR dialog screenshot, expanded Maps/Advanced, keyboard/focus, close-with-unsaved-work and cancellation matrix.
5. System PickerHost is not a selectable target in the current computer-use inventory. Do not bypass its input guard. File-argument import was used as a real supported alternative; drag/drop remains separately unverified.
6. Real Steam Play, physical tilde in the actual game, visual FOV response, real multi-DPI dragging and export acceptance remain pending. Never label CI/fixtures as those passes.
7. Legacy module manifest.json is an unsigned Aurora descriptor in the owner's current Workshop item, not necessarily Steam metadata. Avoid filename-only ownership assumptions and keep creator/game metadata intact.

## Checkpoint discipline

Start from main and AGENTS.md. Keep verified vs source-reviewed vs pending results explicit. Rebuild before delivering changed player binaries; docs-only commits do not change the validated code checkpoint. Preserve original game/model files. Continue useful independent UI work rather than treating game-only pending checks as a global blocker.
