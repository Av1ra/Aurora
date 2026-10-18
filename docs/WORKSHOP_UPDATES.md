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

Copy `docs/examples/aurora-module.json` into that directory. For each upload, set `Version` and `ReleaseDate` (ISO date YYYY-MM-DD), and put your new DLLs in `mods/`. The `Files` list can include any nonempty subset of the exact paths registered in `Aurora.Core/SteamDiscovery.cs`. These registry paths form a hardcoded allowlist. Remove a path from the JSON when that file is no longer shipped: Aurora will remove the previously installed, unmodified copy from the game on the next confirmed update. Keep the retired path in the registry allowlist until users have migrated, so Aurora can recognize it as its own old file. To register another Steam ID, edit that C# allowlist and compile the launcher.

**Startup behavior:** No `aurora-module.json`, no update prompt. With a valid file and all required files present, the launcher automatically compares the Steam source to `SovietRepublic/mods/.aurora-workshop/<SteamId>.json`. On first installation it offers Install; for a newer release or different source DLL bytes it offers Update, showing installed and available versions and dates. Declining does not copy anything. Steam is responsible for downloading subscribed items, and a creator can test from `SovietRepublic/media_soviet/workshop_wip/<SteamId>`.

**Installation:** Aurora copies only registered files into `SovietRepublic/mods/` when approved. It refuses to modify a running game, checks source files again, then replaces only the registered and listed files. When a new release omits a formerly installed file, Aurora deletes that old file only if it is recorded in its own receipt and still matches its last installed SHA-256; modified files are preserved. Replacements use temporary transaction staging for error rollback, and it is deleted after completion. **No permanent DLL backups are created by this Workshop updater.** Normal INI/user files remain untouched. SHA-256 values are calculated internally for change detection and copy checks; they do not appear in the editable JSON and do not replace digital signatures. Workshop ID by itself is not cryptographic proof of the uploaded DLL's origin: only use IDs you control or explicitly trust. Separate Aurora signed modules still use `acode.lnchr` verification.

If you upload the same version and release date but change a DLL, Aurora will still offer an update. Raise the version and release date to make the comparison understandable to players. Downgrades are not offered.

### Exactly what remains on disk

Only the updated files listed in `Files` are retained under `SovietRepublic/mods/`, plus one small receipt `mods/.aurora-workshop/<SteamId>.json` (version, release date and computed file fingerprints, **not DLL copies**). The update transaction temporarily stages files under `mods/.aurora-workshop/.transaction-<random>`, deleting that directory after success or handled failure. This behavior applies to the registered Workshop updater; a separate historical signed-module installer can still have its own backup policy. Existing historical backup folders are not silently removed, since their ownership cannot be proven.

### Detection and update decisions

1. Aurora startup scans registered Steam IDs only, at Steam `workshop/content/784150/<SteamId>/` and in `media_soviet/workshop_wip/<SteamId>/`.
2. If the folder or `aurora-module.json` is absent, offer nothing. If a JSON file is present but listed files have not arrived, silently wait for the completed Steam download. Malformed metadata may be reported in the launcher, without touching the game.
3. Read `Version`, `ReleaseDate`, `Files`; read the installed receipt (if any), compare versions, dates and compute actual file hashes. Downgrades are never offered. Newer versions, a changed date, a changed file list or changed file content prompt a comparison dialog. Registered IDs without a ready update are not shown as permanent cards on Home.
4. Player confirms installation. Aurora refuses if the game is running, validates the source again, and stages new files. It replaces allowed destinations under `mods`, removes only its own unchanged retired destinations, then atomically writes the new receipt.
5. Temporary rollback files are deleted. On the next scan, a matching installed receipt and matching actual game files produce no update offer.

Steam downloads Workshop subscription changes; Aurora does not fetch Workshop files directly from the network. A Steam ID is a discovery filter, not a cryptographic signature of the DLLs.
