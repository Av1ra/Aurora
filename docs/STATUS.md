# Current status — Aurora 0.2

Work in progress. The last public release is being withdrawn at the owner's request; no new release or draft may be published. Version is fixed at 0.2.

## In progress

Packaging, adjacent archive installer, narrow launcher home, compact tilde console, file cleanup, cloud build and source handoff. Verify actual compiled results before marking complete.

## Existing limits to preserve

No Windows Authenticode certificate. Package signatures verify publisher/integrity, not antivirus reputation. Never disable Defender or add exclusions. No promise of a warning-free download.

Earlier .NET downloader stalled on an archive body; add bounded cancellation and progress. Previous native migration retirement differed from external file checks; do not rely only on its receipt. Core/export fixture checks do not prove a full game/F9 smoke test. Full editor localization and additional editor work are out of this task's scope.

## Checkpoint

Implemented and awaiting final verification: root entry EXEs + app folder, external archive installer, game/Aurora default via Steam detection, English title, 200px sidebar, actionable game home, plain language menu, editor description removed, version 0.2 messages, compact tilde-only alpha console host, bounded archive download. Editor rendering features were not redesigned. The cloud development workflow never publishes releases and uses an ephemeral test signing key.

Downloads cleanup archived 25 known working items and wrote a local MOVE-MAP.json. Personal media, game downloads, unrelated installers and unknown archives were preserved. Old WinForms code is under legacy/. Old audit/handoff notes are under docs/archive/.
