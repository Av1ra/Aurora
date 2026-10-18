# Aurora 0.2

**Aurora Launcher** is a Windows mod launcher for Workers & Resources: Soviet Republic. **Aurora Editor** is the bundled model and material editor. They are always packaged together in **one** Aurora application ZIP with a shared WinUI/.NET runtime.

**Preliminary test build.** The matching `Aurora-Setup.exe` is distributed alongside `Aurora-0.2-win-x64.zip`. There is no second Full ZIP or separate Editor ZIP. PBR Workshop content is distributed separately and does not become part of the Aurora installer by registering a Steam ID.

Current functionality includes signed module discovery, registered Workshop ID `3822550289` detection with optional `aurora-module.json` update metadata, FOV Alpha controls for compatible game builds, language selection, a bundled Editor, and a reversible Steam launch bridge that preserves the game's original `SETUPAPPLICATION SOVIET.exe` as `SETUPAPPLICATION SOVIET.exe_orig`. **Real-game Steam launch and FOV visual acceptance remain pending.**

**For Codex and other contributors:** [Codex handoff](docs/CODEX_HANDOFF.md), [Project map](PROJECT_MAP.md), [Development/builds](docs/DEVELOPMENT.md), [Current status](docs/STATUS.md), [Workshop updates](docs/WORKSHOP_UPDATES.md), and [Player description](docs/RELEASE_DESCRIPTION.md).

Version stays at 0.2 until the owner requests a change. Work is identified by commit SHA and CI run. Public Releases require explicit owner approval.
