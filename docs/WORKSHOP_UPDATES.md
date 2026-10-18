# Workshop module release file

Place `aurora-module.json` at the root of the registered Steam Workshop item, next to Steam's existing `manifest.json` and `workshopconfig.ini`. Do not replace Steam's files.

Example for registered Steam ID `3822550289`:

```text
3822550289/
  aurora-module.json
  manifest.json
  workshopconfig.ini
  mods/
    m.dll
    pbr.dll
    glass_default.dds
```

Copy `docs/examples/aurora-module.json` into that directory. For each upload, set `Version` and `ReleaseDate` (ISO date YYYY-MM-DD), and put your new DLLs in `mods/`. The `Files` list must contain the exact source paths registered in `Aurora.Core/SteamDiscovery.cs`. To register another Steam ID, edit that C# allowlist and compile the launcher.

**Startup behavior:** No `aurora-module.json`, no update prompt. With a valid file and all required files present, the launcher automatically compares the Steam source to `SovietRepublic/mods/.aurora-workshop/<SteamId>.json`. On first installation it offers Install; for a newer release or different source DLL bytes it offers Update, showing installed and available versions and dates. Declining does not copy anything. Steam is responsible for downloading subscribed items, and a creator can test from `SovietRepublic/media_soviet/workshop_wip/<SteamId>`.

**Installation:** Aurora copies only registered files into `SovietRepublic/mods/` when approved. It refuses to modify a running game, checks source files again, creates backups and rolls back overwritten files on errors. Normal INI/user files remain untouched. SHA-256 values are calculated internally for change detection and copy checks; they do not appear in the editable JSON and do not replace digital signatures. Workshop ID by itself is not cryptographic proof of the uploaded DLL's origin: only use IDs you control or explicitly trust. Separate Aurora signed modules still use `acode.lnchr` verification.

If you upload the same version and release date but change a DLL, Aurora will still offer an update. Raise the version and release date to make the comparison understandable to players. Downgrades are not offered.
