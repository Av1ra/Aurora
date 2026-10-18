# Aurora 0.2 — working checkpoint

Aurora 0.2 is in development. The owner has now explicitly authorized the test prerelease v0.2 with launcher, editor, PBR and independent FOV Changer. Keep it marked preliminary; subsequent releases still require owner approval. Keep the product version at 0.2 (assembly 0.2.0); identify work by commit SHA and CI run.

## Current task

- Bundle Aurora Launcher and Aurora Editor together: root Aurora Launcher.exe and Aurora Editor.exe, shared WinUI/.NET under app/, bundled editor under app/Editor. Exactly one installer ZIP. Avoid self-extracting executables and duplicated runtimes.
- Separate archive and tiny adjacent installer. Default location: SovietRepublic/Aurora. No payload duplicated inside installer.
- Narrow sidebar, functional game home, English window title, no repeated Aurora heading, plain language menu.
- Aurora Editor is always included in the one installer ZIP for 0.2. No editor feature redesign in this task.
- Compact alpha console opened only by tilde while this game's window is active.
- Archive known working downloads; leave personal and unrelated files intact.
- Consolidate documentation and workflows so remote development requires no local workspace access.

## Entry points

Start with README.md and PROJECT_MAP.md. Read docs/DEVELOPMENT.md for building and unsigned CI validation. Read docs/STATUS.md for exact verified results and open issues. User-facing English release text belongs to docs/RELEASE_DESCRIPTION.md. No credentials/private publisher key may enter Git.

