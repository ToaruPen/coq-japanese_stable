# QudJP Rules

This document is the canonical workflow and decision guide for translation patches, localization assets, runtime evidence, and deployment. `AGENTS.md` and `CLAUDE.md` point here instead of duplicating the same operating rules.

## Evidence order

Use evidence in this order:

1. Tests in `Mods/QudJP/Assemblies/QudJP.Tests/`
2. Layer boundaries in `docs/test-architecture.md`
3. Fresh runtime evidence from current game logs
4. Decompiled game source in `~/dev/coq-decompiled_stable/`
5. Older notes in `docs/archive/` and past investigations

If a stale note conflicts with tests or fresh runtime evidence, follow tests first.

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

`scripts/sync_mod.py` deploys only game-essential files to:

`~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP/`

Do not deploy arbitrary source files. The game will try to compile any `.cs` file it finds, and only `Bootstrap.cs` is meant to be game-compiled.

## Decompiled game source

Decompiled source is a tracing aid, not a shipped artifact.

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
