# Owner cycle, 2026-10-20: history, model configuration, cargo, previews

Keep WinUI, version 0.2, one combined archive and no desktop/game automation.

- Ctrl+Z undo and Ctrl+Y redo must cover model edits, pivot draft/applied transforms, lamps, materials and script changes. One gizmo drag is one action. Text editing keeps its native undo. A new document clears history; history is bounded and retains original geometry references safely.
- Auto-centre pivot: X/Z bounds midpoint and **minimum Y**, not vertical midpoint.
- Add Model settings beside Materials. Adapt script.ini fields into native controls, preserving unrecognized/repeated/multiline data. Lights remain under Modifiers. Include name/localization, cost/currency, years, country, skills, transport/resource capacity and physical/LOD/sound options.
- Cargo visualization uses actual `$RESOURCE_VISUALIZATION` rotation/min/max position+scale records. Load an actual resource NMF+MTL from the game for preview, interpolate fill without mixing cargo into exported vehicle geometry, and persist configuration in script.ini/project.
- Automatic main/side previews for every selected material variant. Verify native preview dimensions; use explicit configurable FOV with 20 degrees as the owner's suggested default, not an unverified claim about every vanilla model. Restore the editor camera/tool state after rendering.

Local read-only examples: bus_ikr_66/script.ini, dlc3/vehicles/_covered_amo15/script.ini, vehicles/forklift_h80/script.ini and media_soviet/resources models. ModelViewer.exe exists; source code is not in the repository. Exact native-viewer framing must not be claimed verified from binary strings alone.

Implementation: `EditorHistory.cs` captures immutable native geometry references and bounded before/after states (48 actions, conservative 192 MiB budget). Native text controls retain text undo; the embedded viewport forwards Ctrl+Z/Y. Draft dragging commits once at release. Opening a document resets history; undo/redo restores material, script, cargo and preview settings too.

`VehicleScript.cs` preserves repeated directives and unknown data. `EditorModelSettings.cs` reads a local native script catalog and uses numeric controls, native enum choices, flags and editable section bodies. Country/localization IDs stay explicit rather than guessed. Main cargo areas use native radians and interpolate position/scale between empty/full values; unsupported joint layouts remain preserved and block unsafe pivot transforms. Cargo preview resource geometry never enters vehicle export. Optional cargo assets/settings and camera settings travel inside `.aur`.

`EditorPreviews.cs` and `Assets/previews.js` render transparent per-variant DDS previews during Save NMF, hide selection/gizmos/cargo and restore the prior view. Settings are under Model. The native viewer source is unavailable, so FOV20 and chosen default angles are configurable approximations, not verified exact native camera constants.

Verification: Core tests include history branches/bounds, lower pivot, repeated script fields, cargo roundtrip and rigid-transform composition. Export tests verify numbered preview names/dimensions. The actual viewport-script harness checks camera restoration, transparent capture dimensions, drag transactions and shortcut forwarding. Both Editor hosts compile. Live dialog use, visual framing, all native vehicle type semantics and in-game exported cargo remain owner acceptance checks.
