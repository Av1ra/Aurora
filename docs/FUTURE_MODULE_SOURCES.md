# Future module sources: design only

These workflows are **not implemented in Aurora 0.2 yet**. Do not treat them as current functionality.

## Common catalog contract

Keep the present registered Steam ID discovery working unchanged. **Aurora Launcher and Aurora Editor remain bundled together in one installer package**; source-independent package ideas below apply to additional optional modules, not a decision to split the Editor. In a later stage, introduce a source-independent module descriptor with module ID, title, semver, release date, file list, supported launcher/game versions and an optional publisher signature. A source adapter returns a local staged folder and descriptor; one install engine validates, compares and installs listed files. Keep a separate installed receipt per module and restrict removal to files previously tracked as installed by that module. Refuse DLL path collisions unless the user explicitly chooses which provider controls the path.

## User-owned local modules (future)

Allow the user to import a local directory or ZIP through an explicit Browse action. Preview the module ID, publisher, version, files and destination paths before installation. Do not silently load imported DLLs or accept arbitrary absolute/traversal paths. Permit unload/uninstall for Aurora-managed files only, with the same tracked receipt semantics. A version/date-only update can work offline; a local file watcher can be optional, not mandatory. Per-user approved modules should remain separate from built-in Workshop IDs.

## GitHub-distributed modules (future)

A small catalog under the Aurora GitHub repository could list independent packages for **additional optional tools and addon modules**. Aurora Editor is already bundled and must not become a separate installation without fresh owner approval. Use a signed metadata document per package, matching ZIP digest and publisher key; do not trust a GitHub URL alone as authorization to execute DLLs. Use existing UpdateService transport, but fetch and install components individually rather than bundling them into a single launcher ZIP. Treat Stable and Dev as different signed channels; granting access to Dev requires an actual protection mechanism, not just local `dev.code` presence on a public plaintext archive. Never commit private keys or access tokens into the repository.

## Suggested delivery stages

1. Finalize the minimal Workshop detection, comparison and install flow with real game tests.
2. Extract a generic module list/receipt API, preserving the current registered-ID behavior.
3. Add local user-owned module import with explicit confirmation and collision handling.
4. Add signed GitHub catalog for optional add-on modules and opt-in Dev transport; maintain the current one-bundle Launcher + Editor distribution.
5. Add cross-source update UI and conflict resolution once multiple sources work reliably.
