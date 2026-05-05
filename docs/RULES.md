# QudJP Rules

This document is the canonical workflow and decision guide for translation patches, localization assets, runtime evidence, Phase F proof obligations, and deployment. `AGENTS.md` and `CLAUDE.md` point here instead of duplicating the same operating rules.

## Evidence order

Use evidence in this order:

1. Tests in `Mods/QudJP/Assemblies/QudJP.Tests/`
2. Layer boundaries in `docs/test-architecture.md`
3. Fresh runtime evidence from current game logs
4. Decompiled game source in `~/dev/coq-decompiled_stable/`
5. Older notes in `docs/archive/` and past investigations

If a stale note conflicts with tests or fresh runtime evidence, follow tests first.

## Phase F boundary and shared defaults

Phase F means runtime route-proof evidence. It is distinct from static coverage, and it does not replace the source-first scanner or fixed-leaf workflow.

For the first PR in issue #358:

- keep the scope on runtime observability and triage
- keep SoT cross-reference deferred until the post-#357 integration follow-up
- keep `DynamicTextProbe` and `SinkObserve` as runtime evidence records, not static coverage verdicts

Shared defaults for this boundary are fixed in the parent roadmap and repeated here for convenience:

- `template_id` is a transport-slot field in this PR, and runtime emitters use `<missing>` until the #357 follow-up owns the canonical static SoT side
- `family` uses the parent-roadmap vocabulary and is not renamed here
- `route` is emitted verbatim and is not normalized

Required verification commands for this boundary:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
uv run pytest scripts/tests/test_triage_log_parser.py scripts/tests/test_triage_models.py scripts/tests/test_triage_classifier.py scripts/tests/test_triage_integration.py -q
uv run pytest scripts/tests/test_triage_integration.py -q -k sample_log_smoke
```

Use these commands when checking Phase F docs, runtime observability, or the first-PR boundary.

## Route ownership

`Ownership` means the route where QudJP can safely define the translation strategy for a string:

- what the stable text is
- what is dynamic
- which markup must survive
- where translation should happen
- which tests prove the behavior

Use these ownership classes:

- `producer-owned`: the upstream producer builds the text and QudJP patches it there
- `mid-pipeline-owned`: the text is still stable enough to be translated safely before sink rendering
- `sink`: final UI text fields such as `UITextSkin`; usually observation-only
- `renderer`: game-owned conversion and display paths after QudJP hands text off

When a bug is rooted in a non-owner route, the first question is not "what dictionary entry would hide this?" It is "where can this route be safely promoted to an owner route?"

## Owner promotion rule

If a translation or color-tag bug is caused by a route that QudJP does not yet own:

1. Trace the upstream producer.
2. Decide whether safe ownership can be created in the producer or a stable mid-pipeline boundary.
3. Add or extend the owner patch there.
4. Add regression tests for the exact broken shape.
5. Keep sink patches observation-only unless a route-specific contract proves they are the correct owner.

Do not create sink-wide ownership just because a string is visible there. Sink text often mixes multiple unrelated routes, and sink-side "fixes" easily break other strings.

## Generic popup route policy

`PopupTranslationPatch.TranslatePopupTextForProducerRoute` and
`TranslatePopupMenuItemTextForProducerRoute` are shared popup producer
translators, not shortcuts for dynamic owner work.

They may be used only when one of these is true:

- the caller is a generic popup surface with no narrower known producer owner
- the text is a fixed popup label, fixed title, or fixed menu item
- the caller has already run its route-specific owner helpers and is falling
  back to shared popup families
- the popup route is explicitly documented in
  `ColorRouteCatalog.GenericPopupProducerRouteAllowlist`

They must not be the first fix for generated, composed, conversation, combat,
display-name, or placeholder-bearing text. If a runtime bug reaches a generic
popup route with an owner-specific sentence shape, add or expose a narrow owner
helper first, then hand off to that helper before the generic popup fallback.

## Choosing the fix type

Use localization assets for:

- true stable leaf strings
- fixed labels
- atomic names that tests and runtime evidence show are not procedural

Use C# route patches or translators for:

- dynamic or procedural text
- strings assembled from multiple fragments
- anything with placeholders, injected names, or mixed markup
- routes already covered by translator or patch tests

Do not add compensating dictionary or XML entries when a route is dynamic, observation-only at the sink, or already owned by a producer or translator.

## Proven fixed-leaf policy

A candidate is a proven fixed-leaf only when the source is stable, owner-safe, markup-preserving, and not `needs_runtime`.

- stable, the exact string shape is fixed and not assembled from live fragments
- owner-safe, the route is not a `message-frame`, builder/display-name, procedural, unresolved, or observation-only sink route
- markup-preserving, every required token, tag, placeholder, and escape sequence survives unchanged
- not `needs_runtime`, the string does not depend on live runtime state or per-run data

Acceptance of a proven fixed-leaf candidate does not change current Translator runtime semantics. `Translator` stays a flat key-only exact lookup path, and validation must reject duplicate or overly broad additions upstream instead of tolerating them at runtime.

The first executed fixed-leaf batch stayed prune-first: the entire 27-row pending queue was pseudo-leaf noise (`""`, `" "`, `BodyText`, `SelectedModLabel`), so the correct safe-survivor set was empty rather than force-promoted.

## Provenance requirements

Every accepted candidate must record:

- source route
- ownership class
- confidence
- destination dictionary
- rejection reason, when the candidate is excluded

## Destination selection

Use a global flat dictionary when the candidate is a proven fixed-leaf, exact, shared, and not route-specific. Use a scoped dictionary when the candidate belongs to one screen, family, or producer route and would be clearer with a narrower home. If a candidate can fit both, prefer the narrower-scoped home. `Popup` exact leaves may use that narrower scoped home when they are separately proven safe. `AddPlayerMessage` remains sink-observed and is not itself a fixed-leaf owner or destination shortcut. If a candidate is dynamic, procedural, `message-frame`, builder/display-name, unresolved, sink-observed, or `needs_runtime`, do not route it to a dictionary unless it is separately proven safe.

## Fixed-leaf batch guardrails

- Exclude pseudo-leaf placeholder and widget/channel rows before promotion review, including empty strings, whitespace-only keys, `BodyText`, and `SelectedModLabel`.
- The current fixed-leaf addition set excludes rows already marked `translated` or `excluded`; existing coverage is evidence for defer/reject decisions, not a fresh addition to validate.
- Validator failures must stay deterministic:
  - `duplicate_key` for competing exact additions in the active addition set
  - `broad_entry` for non-proven, owner-routed, or sink-observed routes
  - `wrong_destination` when a proven leaf bypasses its narrowest safe dictionary home
- Stale bridge bookkeeping already covered on an existing seam (for example the audited `Prone` / `HolographicBleeding` `DidX` rows) belongs to reconcile/owner-audit work, not fixed-leaf promotion.

## Color-tag rules

Preserve all supported markup exactly while translating visible text:

- Qud wrappers such as `{{W|text}}`
- foreground codes such as `&G`
- background codes such as `^r`
- escaped forms such as `&&` and `^^`
- TMP tags such as `<color=#44ff88>text</color>`
- runtime placeholders such as `=variable.name=`

For color-aware restoration:

- use `ColorAwareTranslationComposer` as the restore owner
- treat `ColorCodePreserver` as the low-level helper layer
- do not add ad hoc restore logic in unrelated files when a shared helper already defines the contract

Mixed Qud markup and TMP markup must be fixed route-by-route, with tests that preserve the exact broken input shape.

## Known boundaries

Some behavior is outside QudJP's ownership:

- `UITextSkin` sink fallback is not a general translation owner
- game-side render conversion after `ToRTFCached()` is renderer-owned
- `XRL.UI.Sidebar.FormatToRTF(...)` drops `^` background colors in the modern UI path
- direct TMP assignments that bypass the shared sink path need their own upstream owner or remain game-owned

These are not reasons to give up on a route. They are reasons to move ownership upstream where possible.

## Required workflow for translation bugs

When investigating untranslated or malformed text:

1. Capture the exact string shape from tests or a fresh log.
2. Identify the current route and whether QudJP already owns it.
3. If the route is non-owned, trace the upstream producer in repo code and decompiled game source.
4. Decide whether to:
   - use a stable asset entry
   - extend an existing owner patch
   - promote a non-owner route into a producer or mid-pipeline owner
5. Add the smallest route-specific regression tests that reproduce the failure.
6. Only then implement the fix.

The desired end state is not "the visible bug is hidden." It is "the route now has an explicit, test-backed owner."

## Test expectations

Use the test layers this way:

- `L1`: pure helper logic and route-catalog assertions
- `L2`: Harmony patch behavior without Unity runtime
- `L2G`: target and signature verification against real game DLLs
- `L3`: manual in-game confirmation under Rosetta on Apple Silicon

New translation logic should come with the narrowest regression tests that prove:

- the owner route is correct
- the visible text is translated correctly
- markup and placeholders survive unchanged

When a bug came from a real runtime shape, prefer turning that exact shape into a regression test instead of inventing a cleaner synthetic string.

Producer or queue-gated patches that translate traffic before a generic sink must prove the route contract, not just one translated string. For new or materially changed `AddPlayerMessage`, popup handoff, or similar producer patches, include focused coverage for:

- exact translation of the owned sentence family
- unchanged fallback for unknown or out-of-family text
- empty input staying unchanged when the route can emit it
- color markup and dynamic captures being restored in the final output
- direct-translation markers passing through without retranslation
- owner-absent or queue-only traffic staying unchanged
- observability hit counts proving transformed owner traffic is recorded and pass-through traffic is not recorded as an owner hit

If a route can emit articles, generated names, quantities, or placeholder-like fragments, include those emitted shapes in tests instead of replacing them with dictionary leaves.

## PR review hygiene

Before addressing PR review feedback, confirm the checkout matches the PR head branch:

```bash
git status --short --branch
gh pr view <number> --json headRefName
```

If the active checkout has unrelated dirty work, either use a separate worktree or stage only explicit paths for the PR. Do not commit review fixes from an unrelated branch just because the patch applies cleanly.

## Runtime evidence

Use runtime logs as evidence, not as the primary behavior definition.

Important paths:

- current log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- previous log: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- build log: `~/Library/Application Support/Freehold Games/CavesOfQud/build_log.txt`

Useful markers:

- `[QudJP] Build marker`
- `DynamicTextProbe/v1`
- `SinkObserve/v1`
- `missing key`
- `MODWARN`

On Apple Silicon, use Rosetta for in-game evidence:

- `scripts/launch_rosetta.sh`
- `Launch CavesOfQud (Rosetta).command`

Do not treat native ARM64 runtime logs as localization observability evidence.

## Mod sync and deployment

Preferred deploy path:

```bash
dotnet clean Mods/QudJP/Assemblies/QudJP.csproj
dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental
python3.12 scripts/sync_mod.py
```

Helpful variants:

```bash
python3.12 scripts/sync_mod.py --dry-run
python3.12 scripts/sync_mod.py --exclude-fonts
```

`scripts/sync_mod.py` deploys only game-essential files to the platform default mod directory. Use `--destination` if your install uses a non-standard path.

Do not deploy arbitrary source files. The game will try to compile any `.cs` file it finds, and only `Bootstrap.cs` is meant to be game-compiled.

## Decompiled game source

Decompiled source is a tracing aid, not a shipped artifact. issue-357 の Roslyn pilot はここを read-only external inputs として読みます。

- location: `~/dev/coq-decompiled_stable/`
- regenerate with `scripts/decompile_game_dll.sh`
- never commit decompiled output or game binaries

Use decompiled code to:

- trace upstream producers
- verify method signatures and UI plumbing
- identify renderer-side stop points
- distinguish repo-owned bugs from game-owned limits

## Repo constraints

- Do not commit `Assembly-CSharp.dll` or other game binaries.
- Contributors need a local game install for DLL-assisted work.
- Blueprint and conversation IDs must match game version `2.0.4`.
- The shipped mod is the built DLL plus localization assets and fonts, not the C# source tree.
