# Editor refactoring: stage 1, 2026-10-23

Owner explicitly authorized implementation after the audit. This supersedes the analysis-only instruction for the work below; the complete audit is not declared resolved.

Installed source `8ed0d628f1ff9e535e57a0fdd5a33e31b6e8eeb1`, clean production `build-7f251e6985e24c64a6d59ac1b4c3fb1d`, version 0.2. Aurora was closed; transactional install completed, all 581 installed hashes match. Backup `%LOCALAPPDATA%/Aurora/backups/603182265`. Delivery Downloads/Aurora-0.2-ToolContext-2026-10-23. Ignored receipt artifacts/tool-context-install.json.

## Implemented

- Layer and viewport object selection route through one animation target resolver. Prefer the current clip when it already contains the object. Track and clip selection synchronize the object list and contour. When no track exists for a selected object, no other mesh gizmo is displayed; a message instructs adding that object with +. Existing tracks retain their authored pivots.
- ObjectPivots.Track is the shared local-origin factory for added tracks; first clips use the same local-origin source with an explicit mechanism-axis override. The + route no longer inserts a world-zero pivot. New tracks become active immediately.
- previewAnimation resets previously posed meshes absent from the new clip. stopAnimation() pauses; stopAnimation(true) restores source poses. Closing the timeline resets. Curve selection refresh is guarded to avoid committing interpolation just by switching tracks. Ordinary pivot button stays hidden during animation.
- EditorSnapshot now captures animation clip/mesh/frame/axis context in addition to the mesh tool context. This is incremental consolidation, not a completed replacement of all mode flags with a ToolContext type.
- EditorDensity applies pinned-SDK native theme resources and implicit compact styles to the Editor host: buttons/toggles and list rows target 26 DIP, text/number/combo controls 28 DIP, control font 12, reduced padding. Native templates/input/accessibility retained. Toolbar, diagnostic buttons and coordinate fields also reduced. No dependency on an unverified Compact.xaml path. Bundled module explicitly includes the new file.
- Timeline is two short Grid rows plus frame slider, no full-row horizontal ScrollViewer. It uses viewport width instead of a 208-DIP left margin. Advanced fields are vertical, graph is separate.

## Verification

420 Core checks; JS regression tests; isolated real WebGL PASS. The previously failing target-switch scenario now verifies restoration rather than persistence of the old pose. Production signed WinUI construction includes actual target resolver tests with body/wheel/missing target; native timeline Measure/Arrange at 640, 800 and 1200 DIP with bounds assertions; native compact button desired height <=28.1 DIP. Package/tamper/signature checks and installed hidden WinUI checks PASS. Live user GUI/game acceptance not claimed.

Initial packaging stopped at compile because the explicitly listed bundled-module sources omitted EditorDensity; fixed before final packaging. The incomplete package was not installed.

## Remaining audit work

Explicit object/custom/mechanism pivot binding and pose-preserving rebind of old clips; full replacement of mode flags and stable object IDs; full dock layout and 100/125/150/200% DPI acceptance; remaining bespoke forms/templates (some still have explicit larger sizes); NAME/NAME_STR modes; shared sound/effect drafts; update-domain/performance instrumentation; broader scenario coverage and DCC/game acceptance. This first package does not claim a complete AuaUI toolkit or completion of all audit findings.
