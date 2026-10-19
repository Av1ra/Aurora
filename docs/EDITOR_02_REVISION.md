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
