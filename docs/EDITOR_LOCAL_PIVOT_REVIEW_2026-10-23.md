# Local pivot and model settings consistency review — 2026-10-23

Ready source `5eaaa3f51f76fd92a94fa3da4bf1bffc4fec52f8`, clean production `build-46031a10aa29436cb518fd090e496326`, version 0.2. Installed after owner confirmed closure: 581 installed SHA256 hashes match, installed hidden WinUI startup/layout PASS. Backup `%LOCALAPPDATA%/Aurora/backups/586792000`; ignored receipt `artifacts/local-pivot-install.json`. Delivery Downloads/Aurora-0.2-LocalPivot-2026-10-23.

## Fixed in this package

- Tools previously used an identity NMF frame at vehicle zero even for an off-centre selected object. ObjectPivots.Local supplies that object's bounds centre when its source frame is identity, and preserves explicit nonidentity frames. This is a working origin fallback, without automatic geometry/frame rewriting on editor entry.
- New animation tracks previously used mainPivot. They now use the same ObjectPivots.Local as object tools unless an explicit native mechanism axis was selected. Existing tracks are preserved because their axes may have been deliberately authored.
- Removed technical parenthetical comments from category/purpose captions. Parentheses inside literal/localized vehicle names are preserved as source data.
- Model dimensions moved from model settings to bottom viewport geometry statistics, XYZ metres, one decimal. Bounds are cached by immutable NativeModel reference rather than scanned on every menu state refresh.

## Explained; deliberately unchanged

TYPE is the base transport family. Vehicle class selection applies a catalog profile of flags/settings. The additional roles menu toggles SKILL flags within the type; it is not a second independent class. The overlapping wording remains confusing; owner explicitly requested explanation without changing this workflow.

NAME_STR is a literal name; NAME is a localization text ID. Current form resolves NAME for display and retains it when unchanged; editing the displayed name switches to NAME_STR. It does not offer direct editing of the localization ID. This is an identified UI capability gap, not a reason to strip parenthetical parts of existing names.

## Remaining coupling risks

Existing animation axes are separately stored and are not automatically migrated from an older vehicle-origin default: no reliable marker distinguishes that default from an intentional custom axis. Similarly, changing an object's pivot after authoring a clip does not currently retarget its existing keyframes. A future explicit rebind operation needs pose-preservation tests, rather than silently rewriting clips. An identity source frame cannot distinguish an intentional vehicle-zero origin from a missing per-object origin; current fallback follows the owner's requested centred local editing. Two wheels in one mesh are still one object; use Detach for independent object pivots.

Review covered model settings, selected-object transforms/history, new animation track origin and viewport statistics. It is not an exhaustive audit of every editor subsystem.

Validation: 419 Core checks, Editor compile, production hidden signed WinUI construction/layout and object history checks, package/tamper/signature checks PASS. Interactive GUI, Blender/Max and game acceptance not claimed. Evidence ignored artifacts/local-pivot-*.log; final build log local-pivot-final-build.log. Initial build without bounds caching is superseded and not delivered.
