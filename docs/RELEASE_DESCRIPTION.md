# Aurora Launcher and Aurora Editor

Aurora Launcher is the mod launcher for Workers & Resources: Soviet Republic. Aurora Editor is the bundled model and material editor. Both are part of one Windows package and use the same WinUI/.NET runtime.

- Detect registered modules in Steam Workshop and offer compatible updates.
- Configure supported graphics modules and experimental camera options.
- Launch Soviet Republic from Aurora Launcher.
- Open Aurora Editor for vehicle models, materials, PBR and glass, with multiple LOD support.
- Choose Russian or English.

## Installation (preliminary Aurora 0.2)

Keep **Aurora-Setup.exe** next to **Aurora-0.2-win-x64.zip**, then run Aurora-Setup.exe. Choose the game folder and the Aurora application folder. The installer installs **Aurora Launcher.exe**, **Aurora Editor.exe**, and a single shared **app/** directory with the signed editor module at **app/Editor/**.

There is only **one** ZIP. No second Full archive, editor ZIP or duplicate embedded runtime. No texture packs are included; PBR content is distributed separately through the registered Steam Workshop module. FOV Changer remains an independently signed optional module but its packaged fallback may be included under app/Modules without an extra ZIP.

This is a development/test build of 0.2, not a stable release. The FOV module requires an explicitly supported game executable; full visual acceptance in the actual game remains pending.
