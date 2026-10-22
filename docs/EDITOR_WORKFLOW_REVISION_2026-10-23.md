# Editor workflow revision, 2026-10-23

Version stays 0.2. Implements the owner's eight-point follow-up after the PR #3 installation. Graphics DLLs and game files are outside this revision.

## Delivered source

1. Settings are a single modal window reached from the main menu. Wireframe colour uses RGB/hex draft controls; dragging no longer causes scene updates or a ColorPicker feedback loop. Apply changes the wireframe without rebuilding geometry.
2. Animation controls occupy two compact rows. Parameters and curves open in opaque flyouts, and transform controls sit above the timeline. Existing keyframes, interpolation, FPS and native-action binding remain available.
3. Middle mouse pans in camera space. Left mouse retains orbit/picking. Right mouse does not change the normal viewport or preview camera. Existing camera smoothing remains.
4. Attached-part exhaust leaves the generic Effects section. The vehicle Exhaust entry chooses primary or attached-part smoke; its existing viewport placement tools are reused.
5. New FBX import/export commands, plus OBJ export with a JSON companion containing pivots, helpers and hierarchy. FBX bridge uses pinned MIT-licensed ufbx/ufbx-write sources and an isolated, bounded process. Imported helper metadata persists in project/workspace/history. Exported light and primary-smoke dummy positions can be edited externally and restored on reimport. General unnamed third-party helpers are retained; semantic script bindings are not guessed.
6. Right light palette lists headlights, rear lights, four indicators and beacons. Show all previews the added points. Mirror pair reflects about the main pivot's local X plane; front/rear indicators are paired separately. Native lights remain positional.
7. Object menu contains LOD and Meshes. Left object list supports multiple selection and context Edit/Detach/Rename/Delete. Edit exposes material groups; selected groups can detach, or selected objects can split into disconnected pieces. Mesh rebuilds preserve original world frames rather than flattening their pivots. The rename dialog offers custom names and explained native presets. Existing workspace assembly remains under Scripts.
8. Ordinary Display updates reuse VAOs, unchanged vertex/index/UV buffers, material textures and camera framing. Only changed geometry uploads; new topology gets new buffers. Decoded images have a 96 MiB cache. Cargo and animation keep their incremental update paths. Full scene creation remains intentional for a different document and temporary export scenes; caches are invalidated around those exports.

## Interchange limits and evidence

OBJ alone has no pivot/helper hierarchy. Keep its `.obj.aur-scene.json` companion and use the bundled `Editor/Exchange` scripts when working with pivot in Blender or 3ds Max. See [exchange instructions](../Exchange/README.md). An ordinary OBJ import reads geometry only.

FBX supports rigid/static meshes, named empty nodes, hierarchy, materials/UV/normals and transforms in metres with axis conversion. Skinned FBX, arbitrary FBX animation tracks and sheared node transforms are explicitly unsupported. Native vertex/name limits still apply. FBX/OBJ are scene exchange, not replacements for the existing NMF/NAF/mod export.

Automated checks cover transformed/nonuniform-scale geometry, Unicode file/object/material names, hierarchy, dummy/metadata retention, OBJ companions, disconnected-piece pivot retention, malformed hierarchy rejection, incremental GPU resources and mouse-button behavior. Core suite, module compilation, JS and actual isolated WebGL checks pass. Bundle validation and installed-build evidence are recorded below once produced.

Blender/3ds Max are not installed here. Python syntax is checked, but interactive DCC compatibility and owner GUI/game acceptance remain pending. Do not describe these as live-tested. Export does not claim to create arbitrary game animation triggers.

Production packaging uses the existing publisher identity and preserves the single combined Launcher + Editor ZIP. No public Release/tag is part of this work. Historical handoff bundles and RESAURO remain untouched.


## Installed package

Source `47c85bc02f276738f4544621a95c203e3646103d`, production-signed clean build `build-dfb3b834fd6d4baf87cc26a22d22af9c` (not a public Release). Owner closed Aurora before installation. Transactional install updated `SovietRepublic/Aurora`; all **580 installable file hashes match**. Backup: `%LOCALAPPDATA%/Aurora/backups/542255921`. The retirement table is installer input, not an installed file.

PASS: 408 Core checks including the installed FBX converter, JS viewport/resource/mouse tests, actual isolated WebGL, actual signed WinUI window construction/attached toolbar/opaque menu without activation, native installer/rollback/Steam fixtures, adjacent ZIP tamper rejection, module/update signatures, nine branded executables and backend execution. Additional read-only FBX roundtrips of native Ikarus 66, T138 dumper and T138 mixer preserved object names/counts, triangle counts, frames and bounds. This is not interactive Blender/Max/game acceptance.

Delivery: `C:/Users/AVR/Downloads/Aurora-0.2-Workflow-2026-10-23`.

- Combined ZIP SHA256: `b8be1109acc86608b566f125196eef06016d77a0b4ac6f2ab361fee60f120090`.
- Setup SHA256: `ecd6f38b5897571e9767fd4c5d6fd7ccf92f1556faad66c33a41194d2b9f71b5`.
- Evidence (ignored): `artifacts/workflow-build.log`, `workflow-installed-core.log`, `workflow-real-models.log`, `workflow-install.json`; viewport logs `revision-viewport-tests.log` and `revision-webgl-tests.log`.

No active owner application/game was interrupted. No original game assets or graphics payload DLLs changed. PR #2 and Graphics PR #11 remain outside this package.
