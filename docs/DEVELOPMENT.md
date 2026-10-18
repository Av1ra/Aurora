# Remote development

## First files to read

Read AGENTS.md, PROJECT_MAP.md and docs/STATUS.md. Main is the single working branch. Keep version 0.2 until the owner says otherwise. Commit and push completed checkpoints. Do not create releases, drafts, tags or public binary distributions during this development phase.

## Build on Windows

Prerequisites: Windows 10 1809 or later, .NET SDK 10, Visual Studio C++ x64 tools and Windows SDK, Python 3.12 with numpy, Pillow and PyInstaller. A game installation is not required to compile or run core/native packaging tests.

```powershell
python -m pip install numpy Pillow pyinstaller
./build.ps1 -Development
```

The output directory is written to artifacts/latest-build.txt. Each build uses a new directory so repeated improvements remain version 0.2. Output contains **one** Aurora-0.2-win-x64.zip with both Aurora Launcher and Aurora Editor, adjacent Aurora-Setup.exe, signed aurora-update.json and SHA256SUMS.txt. No Full ZIP, separate Editor archive, separate module ZIPs or duplicate runtime are generated. Do not publish them as a release yet.

Development mode generates an ephemeral ECDSA key, replaces only the staged public key and removes the private key after packaging. It does not modify the pinned publisher key in source. These packages trust only that one development build and are not compatible with production Workshop signatures. No developer private key is uploaded as a CI artifact.

For a future approved production build, omit -Development and provide the existing publisher key through AURORA_SIGNING_KEY or the existing local publisher store. Never commit or transmit that key. Production publishing and Windows Authenticode signing are separate explicit steps. The updater and optional editor remain signature-checked; do not bypass checks in order to test an unsigned addon.

## Layout

```text
Aurora/
  Aurora Launcher.exe         launcher entry
  Aurora Editor.exe           direct editor entry
  app/
    Aurora.Launcher.exe        shared WinUI host
    Aurora.Bootstrap.exe       game startup helper
    Aurora.Installer.exe       update helper
    Assets/                   public signing key and assets
    Editor/                   bundled signed editor + viewport + backend
    Modules/                  independently signed module fallbacks
    ...                       shared .NET / WinUI libraries
```

The runtime is not packed into a self-extracting single EXE. This keeps native WinUI resources resolvable and avoids extracting libraries on every launch. Only the two named entry-point EXEs and app folder are visible at the root. The editor shares the runtime with Aurora Launcher, so no additional editor archive is shipped.

## Checks

```powershell
dotnet run --project Aurora.Tests -c Release
python Packaging/test-native.py <build>/package/app/Aurora.Installer.exe
python Packaging/test-package.py <build>/output
dotnet run --project Aurora.UpdateTests -c Release -- <build>/package/app/Assets/publisher.pem <build>/output
```

The local update test verifies signed packages without relying on a published release. Optional NMF corpus and export tests require legally obtained game model fixtures, not committed game assets. Current CI builds on windows-latest and uploads temporary Actions artifacts. It has no release-write permission and contains no release step.

## Included Aurora Editor

Aurora Editor is always included in the same signed application ZIP, under app/Editor. Aurora Launcher checks the bundled editor's signed manifest before opening it. There is no separate editor download for Aurora 0.2. GitHub updater replaces the complete Launcher + Editor bundle, verifying the signature, ZIP digest and the editor module before handing it to the native updater.

## UI reference

Use a narrow sidebar and an actionable game page (launch, modules, folders, Workshop), inspired by the game-hub pattern shown in the official Xbox PC app: https://www.xbox.com/en-us/apps/xbox-app-on-pc. Do not copy branding or add invented performance settings.


Owner-authorized Aurora 0.2 preview is now distributed as one combined Launcher + Editor archive with its adjacent Setup executable. Packaging CI verifies one ZIP, signed embedded Editor and matching Setup payload digest. Future releases still require separate owner approval; do not publish releases automatically.

## Optional Steam entry substitution

During a normal install with a selected valid game folder, Setup preserves the game's original `SETUPAPPLICATION SOVIET.exe` under the exact suffix `SETUPAPPLICATION SOVIET.exe_orig` (only once) and puts Aurora's small `app/Aurora.Steam.exe` bridge at the original game-root name. This bridge delegates to Aurora Launcher while allowing Steam to track the running process. It prefers the normal game-local `Aurora/app` installation, and supports a saved custom Aurora location for the same configured game. If the launcher is missing, the preserved original is started instead.

Do not overwrite or modify `_orig` during upgrades. An unexpected game EXE with a saved `_orig` causes setup to refuse the substitution rather than erase Steam's replacement. Restore through `Aurora-Setup.exe --restore-steam` from the corresponding installed setup (uses saved paths), with the game closed; it verifies the current Aurora bridge and moves `_orig` back into place. The Setup's `--steam-hook-test <app> <game>` and `--steam-restore-test <app> <game>` test fixtures do not change global preferences.

Only the adjacent single Launcher + Editor archive is necessary. `Aurora.Steam.exe` lives under `app/` inside that archive, and is not a third user-facing application. Steam integration requires a real Steam test: it depends on Steam actually launching `SETUPAPPLICATION SOVIET.exe` for the selected game edition, and cannot be guaranteed from a headless CI run.
