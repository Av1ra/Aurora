# Guided model settings and visual cargo, 2026-10-20

Owner changes supersede the initial raw script-field workflow. Retain WinUI, version 0.2 and one combined archive; owner tests actual windows/game. No release publication.

## Native evidence and model settings

`VehicleCatalog.cs` scans installed vehicles, trains, ships, airplanes and helicopters, plus the same roots beneath DLC/ELC folders. Local inspection found 1062 script files. Native countries and localized model descriptions come from the big-endian BTF string tables, with English fallback for missing translations. The table reader validates index and string spans.

Settings reveal purchase bloc, country name, production years, price, then name/class/resources/sounds and other specifications. Existing values populate the sequence. Years are editable drop-downs spanning 1900-2100, initially 1955/1960 for a new model. Bloc selects RUB/USD purchase records; country availability follows associations in installed native vehicles, with defaults for unused country entries. Never substitute guessed translations for native IDs on disk.

Class choices are actual native combinations of vehicle type, transport category and skill/train flags. Preserve skill numeric values (for example fire engine power) when retaining a class; expose them as numeric controls. A changed class adopts its required flags. Native resource restrictions use named choices filtered by transport class. Main lamp controls remain in Modifiers. Description/family selectors show native text/model names. Advanced spatial/animation records remain preserved and have descriptive labels; unknown parameters are retained rather than reinterpreted.

Sound presets retain the complete ordered SOUND_PARAMS stack from native models. Choices show class/model name. The selected class sorts first and is highlighted; the current/custom sound stack remains unchanged until a preset is selected.

## Visual cargo

The cargo action is inside Model settings, shown only for a transport/resource combination with a visible native resource model. Passenger and liquid classes have no false cargo-model tool. Covered/open/bulk/general/livestock/waste use separate resource choices; optional RESOURCE_ALLOW_ONLY further filters them. Native vehicle-carrying/joint/bone attachments remain preserved advanced data, not claimed supported main-area gizmos.

Choose area, resource and empty/full load state, then work directly in the viewport with Move, Rotate and Scale. Scale offers checked Uniform/Per-axis modes and a centre handle for proportional scaling. XYZ entries remain available. Move/rotate/scale update GPU cargo vertices and inverse-scale normals immediately without rebuilding textures or changing exported vehicle geometry. Empty/full positions/scales and the shared native radians rotation write RESOURCE_VISUALIZATION. One drag is one undo action; tool/endpoint state restores with history. Cargo assets and configuration remain portable in .aur.

## Checks and remaining acceptance

Core tests cover localization bounds/fallback, DLC class catalog, purchase conversion/year validation, native skill preservation, sound stack replacement and resource compatibility. Optional local native tests enumerate every discovered class and offered resource file. Actual viewport-script tests drag axis/centre Scale handles, verify proportions/live vertices and coordinate convention. Both Editor hosts compile.

Owner checks remain: staged reveal/keyboard interactions, drop-down year scrolling, actual visual cargo alignment, all per-class semantics in the game, and performance with unusually large cargo models. No desktop automation is used for those checks.
