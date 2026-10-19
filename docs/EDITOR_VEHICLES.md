# Editor vehicle axes and game lights

Version 0.2, rigid NMF/OBJ documents. Native WinUI menus and dialogs, Russian/English. No source asset is overwritten.

## Commands

- Modifiers > Fix orientation: specify the model's **current** forward and up axes. The game convention inspected in native vehicles is +Z forward, +Y up. Optional horizontal centring moves the pivot to the X/Z bounds centre; ground placement moves the lowest Y to zero. Every loaded LOD receives the **same** transform calculated from the primary model. LODs added subsequently receive the accumulated correction too.
- The rigid bake retains node names, topology, UVs and material groups, writes the established 0x108 stream and normalizes node matrices. Old tangents/face caches are not copied into changed coordinates. Animated, skinned and external nodes are unsupported. Full mod export rebuilds bbox.bin from corrected geometry. Native normal preview/bake uses inverse transpose for scaled source parts.
- Modifiers > Lights > Assign headlights / Assign rear lights: select a lamp object first. Its bounds centre proposes X/Y/Z; adjust these explicitly, optionally add the mirror on X. Assignment adds actual native `$LIGHT_FORWARD` / `$LIGHT_BACKWARD` entries. It does not change material emission or create emissive polygons.
- Edit lights changes coordinates/removes individual emitters, including lights read from an existing script. The disabled native blinker sentinel at Y <= -999 is retained during orientation changes.
- A sibling script.ini is loaded with an NMF document. For a new OBJ, or another vehicle configuration, use Choose script.ini. A real vehicle script is required: the editor does not invent class, capacity, movement or physics settings. Template LOD names must match the exported LOD filenames; missing referenced LODs reject export.
- Save mod writes the edited script.ini before its native `end` terminator and rewrites LOD paths to exported local filenames. NMF-only export cannot save modified vehicle configuration and directs the user to full mod export.

## Boundaries and acceptance

Orientation also transforms supported single-line light/driver/particle positions. Unrecognized script directives, including multi-line `$RESOURCE_VISUALIZATION` cargo geometry, **block** a non-identity correction atomically. No partial model/light rotation is committed. Extend that parser with native fixtures before allowing additional spatial directives; never silently retain a stale cargo box. Assigning lights alone still preserves these unrelated sections.

There is no viewport simulation of game headlights. Automated tests establish stream integrity, axes, bounds, source preservation and native script serialization; actual lamp rendering, procedural wheel behaviour and appearance remain owner game tests. Do not call these live-validated.

Sources: `Aurora.Core/ModelOrientation.cs`, `Aurora.Core/VehicleLights.cs`, `Aurora.Editor/EditorVehicle.cs`, `Aurora.Editor/ModelExport.cs`. Checks: `Aurora.Tests/VehicleChecks.cs`; optional native corpus argument in `Aurora.Tests/Program.cs`. Read-only local references: native personal_vz2109/script.ini (+/- X front emitters at positive Z, rear at negative Z), dlc3/vehicles/_covered_amo15/script.ini (front emitters and disabled blinker).
