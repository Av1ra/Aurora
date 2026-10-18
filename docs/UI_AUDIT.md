# WinUI audit: 2026-10-19

Canonical design standard: https://github.com/Av1ra/GameData/blob/main/WORKFLOW/UI_DESIGN.md . Native WinUI is retained; the existing model viewport remains embedded WebView2.

## Repaired in 1874bcb

- Editor empty viewport now states the next action rather than presenting a blank work area.
- Model-dependent commands and selectors are disabled until prerequisites exist. Export additionally requires MTL; reset requires glass or a mask for the current model.
- Main commands are serialized, with a visible progress indicator. PBR dialog map buttons remain independently usable while that dialog owns the main operation.
- LOD labels show filenames, while real source paths stay in item tags and tooltips. Adding a LOD selects the new model.
- Cancelling material, map or alpha-mask selection preserves the old value.
- Editor controls and PBR labels share the launcher's RU/EN dictionary. This is not a claim that every dynamic error and viewport hint is localized.
- Launcher starts with checking-update state, reports failed checks, and enables installation only when an update was found.
- Saving a changed game folder refreshes module discovery. Settings slider width adapts to its column.
- Both UI projects compile with zero warnings. No renderer/export algorithms were redesigned.

## Verified

- Core fixtures pass with isolated Workshop roots.
- The production-signed combined archive, editor and optional modules pass SHA/signature checks; no duplicate distribution ZIP or private key is shipped.
- Adjacent setup accepts the correct archive and rejects a modified or missing archive.
- Installed current package transactionally into game/Aurora.
- Actual editor screenshot and accessibility tree show the empty prompt, disabled model/LOD/material/processing/export controls, and enabled Open NMF command.
- Clicking Open NMF displays the system file picker and the main command progress indicator.

## Pending, not passed

- System PickerHost modal cannot be selected by the current computer-use helper inventory. It is visible in the captured owned-window screenshot, but input is rejected as a different process. Do not bypass that guard. Therefore actual model import, LOD switching, PBR dialog interaction and cancellation preservation are source-reviewed, not accepted by this GUI smoke test.
- Constrained widths, multiple DPI settings, live RU/EN editor dialog inspection, keyboard traversal and export acceptance still need runtime evidence.
- Existing model rendering/export corpus tests and Steam/FOV game acceptance are distinct from this UI pass.
- The existing viewport hint and some dynamic editor status/error strings still need localization.

## Next useful pass

Complete the model-loaded GUI matrix, then address actual clipping and workflow problems shown there. Keep regular and narrow screenshot evidence, preserve source models, avoid mock controls or decorative content. Product version remains 0.2. Keep one Launcher + Editor ZIP and permanent publisher-key compatibility.
