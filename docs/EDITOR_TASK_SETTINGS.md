# Task-based vehicle settings and PSD launcher rows, 2026-10-20

Owner corrections replace automatic script-field forms. Keep WinUI, version 0.2, shared package, no release publication and no desktop/game automation. Owner tests real UI/game.

## Corrections

- Country blocs now use an explicit ID classification, not observed native vehicle purchase currency. That currency had incorrectly admitted western countries into the socialist list and vice versa. Opening an existing model initializes the bloc from its country. Native names remain localized BTF entries.
- Sound selection has two levels: class then deduplicated presets within that class. It starts at the model's class and preserves current sound settings until a preset is chosen. Owner explicitly chose **INI import only**, not WAV creation/registration. Imported native TEXT/FLOAT sound INIs are frozen in editor session files, copied into model exports with relative references and embedded/remapped inside portable .aur projects.
- Remove automatic unknown/all-directive editing, General, native description/family lists and bare mechanism coordinates from the settings form. Preserve those script records untouched. Show named, directly useful movement fields, class skill values, and cargo settings. This reverses the previous interpretation that every native header should become a UI field.
- Cargo choice, Configure cargo and maximum capacity are one adjacent block. Configure enters visual editing immediately rather than opening another selection dialog. A right-side palette exposes resource names plus area selection/add/remove. Switching resources preserves the camera and layout.
- Actual cargo failure: resource models commonly use *_vehicle.nmf with a base-name .mtl, and numbered waste models share an unnumbered material. Binary material names can also differ from MTL submaterial names. ResourceMaterial resolves the native aliases; ResourceMaps uses exact names when possible, otherwise the validated material order. Tests now parse every offered native resource AND resolve its textures/material binding, replacing the insufficient file-existence-only check.
- Movement exhaust is a native particle point edited with the positional XYZ gizmo, like lamps. Effect IDs/other emitters and lamps are preserved. Smoke tool selection participates in undo. This positions the actual native effect; it does not simulate particle rendering in the editor.
- LOD section uses + and two controls per row: model picker and distance. New NMFs are parsed before apply; models/script/distance changes share the undo transaction. Existing unresolved LOD references block apply instead of disappearing. Unrelated joint LOD directives are preserved. Model files must have distinct exported basenames.
- Pivot/lamp/cargo/exhaust coordinates display two decimals through a NumberBox formatter/rounder. Native geometry and stored source coordinates are not rounded just for display.

## Launcher PSD

Re-read the owner's layered PSD including hidden descendants. The relevant visible path is `Панельки консоли и фова / После / После`, containing `фов` and `Консоль`. Apply two single rows: label, compact switch without On/Off text, rightmost separate Alpha badge. Rename console Developer console; remove its description and the launcher FOV slider from these rows. Existing F9 camera controls/config and signed independent FOV module are retained.

## Source map and checks

Core: VehicleCatalog, VehicleLods, VehicleSmoke, VehicleSoundFiles. Editor: EditorModelSettings, EditorLods, EditorSmoke, EditorCargo, EditorGizmos, MaterialDocuments. Launcher: SettingsModuleCard/FeatureCard in App.cs.

Core/native tests cover bloc separation, LOD pairing and hidden-field preservation, positional smoke edits, custom sound export/archive portability, and every offered cargo's parsed geometry/resolved material maps. The viewport harness covers stable vehicle framing when cargo changes as well as real move/rotate/scale handlers and history. Both WinUI hosts/package checks are required. Live form navigation, custom sounds in game, exact resource appearance and localized layout remain owner acceptance.

The game's developer documents native SOUND_PARAMS_WORKSHOP and sound parameter layering here: https://steamcommunity.com/app/784150/discussions/6/2963895252182905806/ . Adding WAVs would additionally require Workshop library registration; that is outside the owner's chosen INI-only scope.


## Follow-up: OBJ LOD lifecycle and plain numeric fields

LOD model buttons accept OBJ and NMF for both add and replacement. OBJ is converted through the existing parser into in-memory NMF, applies the document pivot transform, and requires the main model's material identifiers. Source files remain untouched. A delete action removes the draft row; apply removes its loaded model/reference, compacts numeric LOD slots and preserves legacy/joint LOD fields. Dialog cancellation leaves the document untouched; applied edits use existing undo/project/export state. Source: Aurora.Core/LodModelImport.cs, VehicleLods.Compact and EditorLods.cs. Export tests cover actual OBJ conversion, replacement, native reload and deleted-file exclusion.

Owner requested no numeric stepper arrows anywhere in Editor. All explicit NumberBox spin placement is now Hidden, including price, capacity, skills, coordinates, preview parameters and LOD distances. Production-year selection remains an editable year dropdown, per the earlier request. Rename unverified coefficient wording to Fuel consumption. Actual native evidence: vehicles/bust_skd_14Tr/script.ini has LIFESPAN 35; airplanes/an2_cargo/script.ini has MOVEMENT_CONSPUMPTION 0. These prove native key existence, not an experimentally established unit/coefficient interpretation.
