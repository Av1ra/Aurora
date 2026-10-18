# FOV Changer

Independent game DLL and signed Aurora module. No FOV memory logic is compiled into the launcher. Schema 2 metadata asks the generic launcher renderer to show an enabled toggle and 20–100 degree slider under Settings. Changes apply at the next game launch. Default: 45 degrees.

The resolver reuses the previously researched SovietCDK camera constant table, requires one exact match in readable non-code/non-writable PE sections, and additionally pins the supported executable SHA-256. Unknown game builds are refused without writing memory. Diagnostic result: fov-status.txt beside the module DLL.

Offline recognition of the current game executable and synthetic resolver tests are required. A real-game visual acceptance remains necessary; do not present the first preview as a universally compatible camera mod. No game executable is distributed.
