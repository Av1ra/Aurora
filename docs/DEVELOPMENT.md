# Remote development

## First files to read

Read AGENTS.md, PROJECT_MAP.md and docs/STATUS.md. Main is the single working branch. Keep version 0.2 until the owner says otherwise. Commit and push completed checkpoints. Do not create releases, drafts, tags or public binary distributions during this development phase.

## Build on Windows

Prerequisites: Windows 10 1809 or later, .NET SDK 10, Visual Studio C++ x64 tools and Windows SDK, Python 3.12 with numpy, Pillow and PyInstaller. A game installation is not required to compile or run core/native packaging tests.

```powershell
python -m pip install numpy Pillow pyinstaller
./build.ps1 -Development
```

The output directory is written to artifacts/latest-build.txt. Each build uses a new directory so repeated improvements remain version 0.2. Output contains adjacent Aurora-Setup.exe and Aurora-0.2-win-x64.zip, optional Aurora-Editor-0.2-win-x64.zip, metadata and SHA256SUMS.txt. Do not publish them as a release yet.

Development mode generates an ephemeral ECDSA key, replaces only the staged public key and removes the private key after packaging. It does not modify the pinned publisher key in source. These packages trust only that one development build and are not compatible with production Workshop signatures. No developer private key is uploaded as a CI artifact.

For a future approved production build, omit -Development and provide the existing publisher key through AURORA_SIGNING_KEY or the existing local publisher store. Never commit or transmit that key. Production publishing and Windows Authenticode signing are separate explicit steps. The updater and optional editor remain signature-checked; do not bypass checks in order to test an unsigned addon.

## Layout

```text
Aurora/
  Aurora.exe                  launcher entry
  Aurora.Editor.exe           editor entry; downloads optional editor if absent
  app/
    Aurora.Launcher.exe        WinUI host
    Aurora.Bootstrap.exe       game startup helper
    Aurora.Installer.exe       update helper
    Assets/                   image and public verification key
    docs/                     player readme
    Editor/                   optional signed editor module + viewport + backend
    ...                       shared .NET, WinUI and localization resources
```

The runtime is not packed into a self-extracting single EXE. This keeps native WinUI resources resolvable and avoids extracting libraries on every launch. Only the two entry-point EXEs and app folder are visible at the root. The editor shares the runtime, so its optional package is not a second full copy of the launcher.

## Checks

```powershell
dotnet run --project Aurora.Tests -c Release
python Packaging/test-native.py <build>/package/app/Aurora.Installer.exe
dotnet run --project Aurora.UpdateTests -c Release -- <build>/package/app/Assets/publisher.pem <build>/output
```

The local update test verifies signed packages without relying on a published release. Optional NMF corpus and export tests require legally obtained game model fixtures, not committed game assets. Current CI builds on windows-latest and uploads temporary Actions artifacts. It has no release-write permission and contains no release step.

## Future editor download

After the owner approves a public release, publish the signed addon and aurora-editor.json together. Download editor in Aurora downloads, verifies SHA-256 and the pinned signature, stages files on the target volume, then atomically installs app/Editor. With no approved release, the app explains that the editor is not yet published. Keep extraction/copying away from user model files and never disable antivirus protection.

## UI reference

Use a narrow sidebar and an actionable game page (launch, modules, folders, Workshop), inspired by the game-hub pattern shown in the official Xbox PC app: https://www.xbox.com/en-us/apps/xbox-app-on-pc. Do not copy branding or add invented performance settings.
