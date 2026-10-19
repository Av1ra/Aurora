# Owner layout and next implementation cycles (2026-10-19)

Version remains 0.2 and WinUI is mandatory. Owner tests the real game; no desktop automation. Work in 2–3 related corrections, then a short report and choice of the next cycle.

The supplied layered PSD was parsed including hidden descendants. Its original file remains owner-owned outside the repository. This is an implementation plan, not delivered feature claims.

## In-game F9 menu

- First F9 opens a small module list. Graphics opens the PBR settings and hides the list.
- F9 hides the active menu/settings window without destroying its controls; the next F9 restores the last page and its state.
- Include a way back to the list. Closing the game disposes its windows and listeners.
- Camera/FOV appears here and applies immediately in the running game, as explicitly selected by the owner. Keep fov.dll independent: it owns game-memory changes, with the existing executable compatibility gate and bounded angle validation.
- Existing tilde developer console remains separate from F9.

## Compact launcher rows

The PSD replaces tall FOV/console cards with single compact rows: label, right-aligned switch, separate Alpha badge. Rename console to Developer console. Keep the camera module discoverable and its preferences accessible without a running game.

## Editor layout and hidden menus

- Top command strip: menu, Select object, Modifiers, Materials. Maximise viewport space. Bottom-left geometry counts refer to the active model/LOD.
- Menu: Import OBJ, Export NMF, Import textures. Texture submenu: Diffuse, Normal, Specular, AO, Roughness, Metallic.
- Modifiers: Glass, Light, Fix orientation. Glass submenu: Assign object, Alpha channel. Light submenu: Assign headlights, Assign rear lights.
- Material editing and LOD selection must survive the move from the current sidebar. Keep original-file protection, unsaved-change handling and RU/EN.
- Owner explicitly requested all new commands in this work, not placeholder buttons. The owner clarified that Fix orientation corrects the pivot/axes so the model faces correctly, not inverted surface normals. Headlights/rear lights must be actual game effects. Native vehicle script.ini files use $LIGHT_FORWARD / $LIGHT_BACKWARD positions (+Z front, -Z rear in the inspected VZ2109), not emissive material substitutes. OBJ/NMF round-trip and all LODs need meaningful geometry tests.
- The blank right region in the PSD is labelled as under development; do not fill it with invented feature cards or invent unapproved controls.

Future visual UI-authoring plugin is a recorded idea only. Do not implement it in these cycles.

## Editor cycle 1

Implemented compact top menus for File, Select object/LOD, Modifiers/Glass and Materials. Existing operations share their original state/handlers; no duplicated hidden sidebar controls. Ctrl+O / Ctrl+S live at the window root. Statistics show objects, triangles and vertices for the active LOD, not a sum of all LODs.

Added independent NMF export: all loaded LODs go into a new folder, originals stay untouched, glass object assignments must cover every LOD. NMF-only export does not clear unsaved material changes; alpha-mask projects must use the existing complete mod export. Actual three-LOD fixture tests passed for byte preservation, glass assignments, source hashes and duplicate-name rejection. No owner-screen validation yet.

Still pending: OBJ importer, texture import submenus, pivot correction and game-light assignment/export. Do not show these as working placeholder commands or claim the whole PSD implementation is complete. F9/live-FOV is the separate agreed cycle.

## Graphics correction checkpoint

Runtime source: private Av1ra/GameData commit 368d73a. Built pbr.dll SHA256 111bea5f21d8c4ac20378e9dd13a07edec3fdcac7801124a4761722cc5e7429f.

Adds exact native AMO caster exclusion alongside the prior glass-edited workaround, restores previous asphalt scalar defaults and restricts generic automatic material replacement/vehicle-only sliders. Native caster, model and ground-road offscreen checks passed; owner live acceptance remains pending. This restores no colour/normal texture packs; authored road normal maps remain absent from the effects-only distribution.
