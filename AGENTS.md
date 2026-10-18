# Aurora 0.2 — working checkpoint

Aurora 0.2 is in development. The owner has now explicitly authorized the test prerelease v0.2 with launcher, editor, PBR and independent FOV Changer. Keep it marked preliminary; subsequent releases still require owner approval. Keep the product version at 0.2 (assembly 0.2.0); identify work by commit SHA and CI run.

## Codex continuation

**Before changing code, read [docs/CODEX_HANDOFF.md](docs/CODEX_HANDOFF.md).** It contains the current verified commit and CI run, architecture, Steam bridge behavior, unfinished checks, the next UI work, a manual PASS/FAIL matrix, and permanent owner constraints.

UI changes must follow the [GameData UI design standard](https://github.com/Av1ra/GameData/blob/main/WORKFLOW/UI_DESIGN.md). Do not use the middle-dot separator character in UI prose. Real Steam game acceptance is pending; a CI pass does not establish actual Steam Play success. Keep exactly one combined Launcher + Editor ZIP.

## Current task

- Bundle Aurora Launcher and Aurora Editor together: root Aurora Launcher.exe and Aurora Editor.exe, shared WinUI/.NET under app/, bundled editor under app/Editor. Exactly one installer ZIP. Avoid self-extracting executables and duplicated runtimes.
- Separate archive and tiny adjacent installer. Default location: SovietRepublic/Aurora. No payload duplicated inside installer.
- Next work after Steam acceptance: audit and repair Aurora Launcher and Aurora Editor UIs without breaking working flows. Maintain the compact sidebar, professional hierarchy and actual controls. No generic UI templates.
- Aurora Editor is always included in the one installer ZIP for 0.2. No editor feature redesign in this task.
- Compact alpha console opened only by tilde while this game's window is active.
- Archive known working downloads; leave personal and unrelated files intact.
- Consolidate documentation and workflows so remote development requires no local workspace access.

## Entry points

Start with docs/CODEX_HANDOFF.md, README.md and PROJECT_MAP.md. Read docs/DEVELOPMENT.md for building and unsigned CI validation. Read docs/STATUS.md for exact verified results and open issues. User-facing English release text belongs to docs/RELEASE_DESCRIPTION.md. No credentials/private publisher key may enter Git.


Current autonomous WinUI checklist: [docs/UI_AUDIT.md](docs/UI_AUDIT.md). The owner approved ongoing UI analysis/fixes until stopped; keep game/Steam-only acceptance pending separately rather than blocking independent UI work. This UI pass saves code/checkpoints and verified local builds without publishing a release.
