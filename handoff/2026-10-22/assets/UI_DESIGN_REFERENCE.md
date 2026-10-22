# UI Design Standard — anti-template / anti-"AI slop"

Status: active. Established from the user's 17 visual references on 2026-10-18.
Scope: websites, launchers, dashboards, game tools, Windows utilities, HTML/interactive interfaces.
This is a **design/QA rule set**, not model retraining. New explicit user requirements override it.

## 1. Design intent

Build a product with its own coherent art direction, not a stock component showcase.
A UI is successful only if it is visually convincing **and** helps the user complete real tasks.
Do not treat the references as one universal appearance: editorial/photo-led pages, application settings, data dashboards, and system components require different composition.

Every new interface must answer:
1. Who uses it, for what primary task, and on which screen?
2. What is the information hierarchy and shortest route to the primary action?
3. Which reference family is relevant, and what should **not** be borrowed from it?
4. Which component states, dependencies, errors and keyboard paths must work?
5. What specific evidence will demonstrate that the result is not only a mockup?

## 2. Studied reference catalog

The user supplied a sequence of 17 screenshots (the source images are in the 2026-10-18 conversation, **not** stored as repository image assets). R01 is the current PBR interface and therefore a **baseline for improvement**, not automatically a target look.

| Ref | Distinct visual language | What to learn |
| --- | --- | --- |
| R01 | PBR graphics launcher, dark Windows-style utility | Preserve controls and behaviors, improve grouping, hierarchy, space efficiency and settings dependencies |
| R02 | Mountain house / Horizon photographic landing page | One dramatic visual, large disciplined editorial headline, overlay content in intentional positions |
| R03 | Onlinezona foggy forest website | Atmospheric imagery, restrained navigation, few high-priority controls |
| R04 | Light interior designer editorial multi-section portfolio | Asymmetric modular grid, alternating image/text rhythm, airy but content-rich sections |
| R05 | Waldorf Astoria city/luxury landing | Strong brand composition, architectural shapes, cinematic framing, precise type |
| R06 | Near-black minimalist interior portfolio | Restrained palette, generous deliberate negative space, bold photo crop |
| R07 | Wood/textured personal portfolio | Material-based depth and typography integrated with photographic content |
| R08 | IBERI architectural night city web design | Sophisticated visual hierarchy and visual storytelling; avoid copying its dated fine-print density |
| R09 | Black/orange San Francisco Pro design system | Typeface scale, consistent component vocabulary, accent discipline, explicit states |
| R10 | Bright blue/pink dashboard concept | Advanced data visualization layout; vivid accent is context-dependent, not the default |
| R11 | Navy/blue design system board | Tokenized colors, interaction states, typography, icon consistency |
| R12 | Native Apple/iOS controls reference | Familiar control behavior, grouping, obvious active/disabled/focus states |
| R13 | Dark video/media dashboard | Content-first grid, clear navigation and attention placement |
| R14 | Charcoal finance dashboard | Dense-but-readable information hierarchy, dashboards that put important facts first |
| R15 | Colorful mobile sleep/audio UI | Strong task-specific personality; do not indiscriminately copy its gradients and effects |
| R16 | Dark music/media desktop interface | Artwork-forward navigation, strong content hierarchy, anchored playback controls |
| R17 | Soft pastel dashboard/component board | Deliberate tactile softness and subdued contrasts when the product suits it |

Important: not all samples are equally applicable or modern. Analyze their **mechanisms**, not copy entire layouts, palettes, trademarks or decorative techniques.

## 3. Non-negotiable composition rules

- Define primary and secondary actions; one clear visual focus per screen/section.
- Start with content and interaction flow, then choose a grid, type scale, spacing and imagery.
- Follow coherent alignment and consistent rhythm. Use generous space only when it contributes to hierarchy; avoid vast empty slabs in utility applications.
- Use type as structure: hierarchy by size, weight, line length and spacing rather than piles of outlines and backgrounds.
- Real/accurate photos where objects need to be identifiable (vehicles, buildings, products, people, technology); no model mismatches, random illustrative substitutes or repeated placeholder pictures.
- Limited semantic color system: neutrals plus intentional accents; high contrast for critical controls. A bright/neon accent may be used if it is **specifically justified by the reference and use case**, not as automatic decoration.
- Roundness, translucency, shadows and gradients are design decisions, not defaults. Avoid repetitive pill buttons, uniformly rounded cards, glassmorphism and excessive glow.
- Content on images needs sufficient contrast across actual crops and responsive states.
- Desktop utilities must favor fast scanning and efficient interaction over cinematic hero sections.
- Prioritize recognizability, precision and affordance over looking 'futuristic'.

## 4. Prohibited anti-patterns ("нейрослоп")

Reject or redesign when present without product justification:
- same large rounded cards everywhere, no hierarchy;
- neon rings, gratuitous gradients/glows or decorative noise;
- meaningless charts, fake metrics, icon spam or filler badges;
- fake toggles/buttons without behavior or incorrectly advertised functions;
- inconsistent typography, padding, alignment, icon sets or hover states;
- invented/incorrect subject imagery, duplicated stock visuals, distorted AI details;
- sprawling configuration pages with no group structure, dependencies or actionable error states;
- a cosmetic recolor presented as complete UX redesign;
- screenshots/prototypes advertised as verified working applications.

These are **quality failure patterns**, not bans on any visual technique in all contexts.

## 5. Recommended design process

1. **Audit:** List existing screens, user tasks, constraints, actual controls, dependencies and missing behaviors; note what is currently functional.
2. **Information architecture:** Group by task/domain; map happy path and errors; avoid unnecessary navigation.
3. **Reference selection:** Choose 2–3 fitting mechanisms from the reference catalog; document why each helps the task.
4. **Design tokens:** Define typography (family and scale), spacing, panel surfaces, semantic accent, contrast, border/radius, icon language. No arbitrary per-widget styling.
5. **Functional prototype:** Implement real local interactions, real disabled states, validation and persistence where requested/possible. If not connected to a real backend, say so visibly.
6. **Visual review:** Compare actual screens at typical and constrained viewport sizes; correct hierarchy, content density, clipping and visual repetition.
7. **QA and delivery:** Provide PASS/FAIL checks with evidence and reproducible steps. Separate IMPLEMENTED from VERIFIED and UNVERIFIED. Explicitly identify unsupported runtime/platform tests.

## 6. Default acceptance checklist

A design is ready for delivery only when all applicable items are checked:
- [ ] Primary task can be completed without unnecessary detours.
- [ ] Every visible interactive control has meaningful state/action; disabled controls explain dependencies.
- [ ] Content hierarchy is obvious at a glance; labels are legible; no meaningless fake metrics.
- [ ] Layout is consistent and tested at narrow and normal desktop widths, relevant DPI/scaling and with long localized strings.
- [ ] Keyboard/focus behavior and contrast are reasonable for the platform.
- [ ] Real-world images, if used, are accurate, sourceable, appropriately licensed and not duplicated as placeholders.
- [ ] No unmotivated glow, gradients, rounded cards or decorative clutter.
- [ ] Error, loading, empty, selected, hover/focus and saved/unsaved states are handled where applicable.
- [ ] The new design preserves agreed functionality; changes to behavior are explicit.
- [ ] Actual runtime evidence exists, or limitations are labeled UNVERIFIED.
- [ ] For each build, deliver a compact step-by-step test with PASS/FAIL and specific logs/screenshots to report.

## 7. Specific application: Workers & Resources PBR launcher

R01 defines the **existing** feature set: master PBR; vehicles; buildings/objects; roads; additional shadows; glass; small grass; diagnostics; language switch; save/launch.

Target: a compact, credible **Windows desktop graphics configurator**, not a marketing site. Do not transplant mountain backgrounds, huge headings or dashboard analytics into it.

- Use a restrained app header and compact grouped settings: General, Materials, Effects, Diagnostics.
- Keep PBR global enable readily visible. Distinguish supported options from beta/experimental functions without visually flooding the screen.
- Reflect dependencies explicitly: glass requires Vehicle PBR; contact shadows require Road PBR where applicable. Disabled options should retain user intent if master toggle goes off rather than silently erasing it.
- Make diagnostics opt-in or clearly explain when it produces disk I/O/performance costs. Avoid uncontrolled logging in game.
- Show save vs launch semantics clearly; no silent game start as a side effect of the wrong action.
- Do not claim shaders, renderer features or runtime loading work without verification in the actual game.
- Keep all originally available options accessible; optimize vertical space with grouped rows, optional disclosure sections and sane defaults.
- Validate language strings, focus/keyboard control, scroll behavior, scaling and real persistence.

## 8. Operating constraints

- This file is the canonical shared UI policy. Do not duplicate the full rules in other active Markdown files.
- Repository indexing should point to this file; other modules may contain project-specific overrides only.
- These references record the user's preferred **approaches**, not an instruction to copy unrelated site designs into a PBR configuration window.
- No assurance of permanent model-level 'self-improvement': use persisted user preferences and this GitHub policy to guide future work, checking accessibility each session.
