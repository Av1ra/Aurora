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
