# Owner revision, 2026-10-20

Keep WinUI and version 0.2. No desktop/game automation. Implement and checkpoint the following scope; no release publication.

1. Top toolbar flyouts prefer below their own buttons.
2. Remove duplicate File > Import textures; material settings own texture assignment.
3. Separate native and PBR material inputs; hydrate native/OBJ/PBR textures into the settings, not just viewport.
4. Material settings replaces Configure PBR, with separate native/PBR load buttons inside. Support native numbered skin variants and submaterials.
5. Create/assign materials and export their definitions. No raw pbr.mtl filename in Materials menu.
6. Interactive XYZ model pivot, move arrows, rotate rings, bottom-centre numeric XYZ, bottom-right Edit/Fix and automatic centring.
7. Remove viewport help and persistent material filename under statistics.
8. One vehicle light command, four lamp slots, editable gizmos and symmetry about the main pivot with explicit reflection axis.
9. Two save commands: Save NMF (material/numbered-variant selection) and Save project.
10. Pair material.mtl with material_pbr.mtl, material_1.mtl with material_pbr_1.mtl, etc. Renderer resolves the actual selected native material's paired file, preserving legacy pbr.mtl compatibility where appropriate.
11. Split disconnected components and rename objects, including native wheel presets without descriptions. Preserve LODs/material groups.
12. Versioned encrypted .aur archive includes geometry/assets/settings/document state. Owner chose no password; embedded application encryption is format protection, not a guarantee against reverse engineering. Validate archive paths/sizes before extraction.
13. LogoAE.png in game root is owner artwork. Embed in executable resources; don't ship an external .ico. Windows Explorer executable icons require native icon resources, which may be generated during build.

Working checkpoints and acceptance evidence belong to STATUS.md. Live gizmo usability, native skin selection, lamp and wheel behaviour remain owner checks.

## Implementation checkpoint

- Toolbar flyouts explicitly prefer BottomEdgeAlignedLeft and constrain themselves to the XAML root. Import-textures submenu, filename label and viewport help are removed. Materials contains variant/submaterial selectors and material creation/assignment; native/PBR file loading lives inside Material settings.
- Native MTL slots 0/1/2 and OBJ usemtl/mtllib maps hydrate editable settings. Native diffuse/normal and PBR overrides are separate. Numbered variants have independent settings. Additional authored PBR properties are retained; preview is an approximation of game rendering.
- Save NMF presents single/multiple material mode and numbered variant checkboxes. Output contains one common NMF/LOD set and material.mtl/material_pbr.mtl, material_1.mtl/material_pbr_1.mtl, etc., with independent portable texture references. Existing internal legacy exporter remains for tests/backend use, not as a separate save button.
- Move arrows and rotation rings are drawn in the viewport with perspective-correct world projection. Numeric XYZ follows the selected tool. Editing the pivot is a draft; Fix pivot applies its inverse frame to every LOD, native light positions and supported script coordinates. Auto centre uses primary-model bounds. Unknown spatial script sections still block unsafe correction.
- Four lamp buttons edit native positions through Move/XYZ. Symmetry reflects in the selected axis of the **main pivot frame**. Owner confirmed lamps have coordinates only, with direction determined by main pivot; no separate lamp-angle command is promised.
- Object rename and connected-component split operate across matching LODs. Parts are ordered by spatial centres; different part counts reject atomically. Presets include native inspected tire_leftShape, tire_rightShape, tire2Shape and tireShape. Names alone cannot guarantee every custom vehicle's steering behaviour; game acceptance is pending.
- Passwordless .aur is a versioned AES-GCM encrypted ZIP containing native streams, all referenced textures, per-variant settings, glass selections/mask, vehicle configuration, document transform and pivot state. Header authentication, entry path validation, duplicate checks and aggregate size limits are enforced. It is portable format protection, not publisher signing or protection against reverse engineering.
- LogoAE.png is tracked as owner artwork. Build embeds a PNG icon frame and native group-icon resources into Aurora executables, including adjacent Setup and Python backend, without distributing an ICO. Backend overlay is preserved and its CLI execution is checked after branding.

Automated checks cover material hydration/separation, paired naming, Wavefront maps, material creation/assignment, disconnected components, encrypted archive conservation/tamper/traversal, multi-variant export, actual gizmo mouse handlers/projection/handedness and native companion priority. Visual layout, cursor feel, game skins, steering and actual lights still require owner testing.
