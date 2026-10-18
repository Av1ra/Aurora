# Aurora 0.2 — working checkpoint

Aurora 0.2 is in development. Do not create or publish a GitHub release until the owner explicitly requests it. Keep the product version at 0.2 (assembly 0.2.0); identify work by commit SHA and CI run.

## Current task

- Tidy distribution: Aurora.exe and Aurora.Editor.exe at root; shared WinUI/.NET and editor files under app/. Avoid self-extracting executables and duplicated runtimes.
- Separate archive and tiny adjacent installer. Default location: SovietRepublic/Aurora. No payload duplicated inside installer.
- Narrow sidebar, functional game home, English window title, no repeated Aurora heading, plain language menu.
- Model editor remains optional; download/install from future signed GitHub releases. No editor feature redesign in this task.
- Compact alpha console opened only by tilde while this game's window is active.
- Archive known working downloads; leave personal and unrelated files intact.
- Consolidate documentation and workflows so remote development requires no local workspace access.

## Entry points

Start with README.md and PROJECT_MAP.md. Read docs/DEVELOPMENT.md for building and unsigned CI validation. Read docs/STATUS.md for exact verified results and open issues. User-facing English release text belongs to docs/RELEASE_DESCRIPTION.md. No credentials/private publisher key may enter Git.
