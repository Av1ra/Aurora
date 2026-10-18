# Retired implementation

The old self-contained WinForms installer and its tests are kept under legacy/ for historical reference. Do not build or ship them. The current installer is Aurora.Setup.Native/Setup.cpp and current root entries are built from Entry.cpp. No active build references the legacy projects.

Closed PR #1 used the old installer architecture. The native adjacent-archive installer and current WinUI cards supersede it. The branch was archived locally before cleanup; its commits remain accessible through the closed PR.
