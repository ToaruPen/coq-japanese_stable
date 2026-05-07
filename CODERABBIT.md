# CodeRabbit Review Knowledge

This root-level document is the CodeRabbit-specific global guideline for QudJP
changes that depend on local inspection of Caves of Qud 1.0.4 runtime behavior.
CodeRabbit may also auto-detect root `AGENTS.md` or `CLAUDE.md`; this file adds
review rules they do not contain. It must be self-contained for critical
CodeRabbit review rules because CodeRabbit scopes guideline files by the
directory where they live.

It contains only derived behavioral contracts. It must not include copied game
source, decompiled method bodies, large symbol inventories, game assets, or
near-verbatim paraphrases of decompiled code. Decompiled source is a local
read-only tracing aid, not a repository artifact.

## Source Hierarchy

When reviewing a change, apply evidence in this order:

1. Tests in `Mods/QudJP/Assemblies/QudJP.Tests/`.
2. Layer boundaries in `docs/test-architecture.md`.
3. Fresh runtime evidence from current game logs.
4. The Roslyn-derived text construction inventory in
   `docs/coderabbit/roslyn-text-construction-inventory-summary.md`.
5. The supporting CodeRabbit route, token, and terminology reference documents:
   `docs/coderabbit/owner-route-coverage.md`,
   `docs/coderabbit/color-token-review.md`, and
   `docs/coderabbit/glossary-variance-review.md`.
6. Derived findings from local decompiled inspection.
7. Older notes in `docs/archive/` and historical reports.

If this document conflicts with tests or fresh runtime evidence, the tests or
fresh runtime evidence win. Flag the conflict instead of silently accepting the
older guidance.

Supporting files under `docs/coderabbit/` organize the same knowledge for human
maintenance. Do not rely on those files being globally applied to C# or
localization paths; critical CodeRabbit-specific rules must be present in this
root file.

## Roslyn Text Construction Inventory

The broad static map of game-side English text construction is generated with
Roslyn, not grep and not the legacy scanner. The reviewer-facing summary is
`docs/coderabbit/roslyn-text-construction-inventory-summary.md`.

The committed CodeRabbit knowledge base must not include the full generated family inventory JSON
because that is a large symbol inventory. The local generator may produce the
full raw-free JSON for maintainer audits, but CodeRabbit should receive the
summary, route matrix, token rules, glossary rules, and tests.

Use this inventory as the completeness map for CodeRabbit review:

- a changed route should be checked against the relevant Roslyn family record;
- PRs that claim broad runtime coverage should update the Roslyn summary and
  provide local regeneration evidence for the full inventory;
- `AddPlayerMessage`, `SetText`, direct `.text` assignments, and renderer-like
  surfaces remain sink-adjacent unless producer ownership is separately proven;
- return surfaces such as effect descriptions, display names, display text,
  message frames, `Does`, journal additions, HistorySpice expansion, popup
  producers, and activated-ability APIs are route-family clues, not automatic
  proof of localization coverage.

Do not use `scripts/legacies/scan_text_producers.py` or archived legacy
candidate inventories as CodeRabbit knowledge-base authority.

## Display Route Coverage

Use `docs/coderabbit/owner-route-coverage.md` as the reviewer-facing
route ownership matrix. The Roslyn inventory identifies construction surfaces;
the matrix classifies whether each family is producer-owned, mid-pipeline-owned,
asset-owned, mixed, or observation-only.

Reviewers should require an explicit owner decision for display routes. Final
sinks such as `UITextSkin.SetText`, generic `AddPlayerMessage`, generic popup
display, renderer output, and inventory/screen hierarchy probes are not broad
translation owners by default. Route-specific tests or runtime evidence must
prove any exception.

Critical route defaults that CodeRabbit should enforce even when it does not
load supporting docs:

| Route family | Default review stance | Flag when |
| --- | --- | --- |
| UITextSkin.SetText / direct UI text | Observation-only unless screen-owned | Sink-wide translation or missing screen/field tests |
| Generic AddPlayerMessage | Observation-only unless producer handoff is proven | Flat message-log dictionary catchalls or bypassed producer helpers |
| Generic popup display | Mixed and cautious | Broad popup fallback without overload/route tests |
| XDidY / message frames | Producer or mid-pipeline owner | English SVO shape survives or color/placeholders are lost |
| Does verb routes | Tested route-owned translator; no broad Does() patch | Direct broad mutation or verb token leakage |
| Descriptions/effects/display names | Route-owned only where tested | Procedural text hidden as asset leaves or renderer-only labels translated broadly |
| Renderer/final-output probes | Observation-only | Probe evidence is treated as ownership or visible text is mutated at the renderer |

## Decompiled Knowledge Policy

Review comments may rely on decompiled-derived knowledge only as a behavioral
claim, for example:

- the safe owner route is a producer or stable mid-pipeline method;
- a sink is observation-only for a route;
- a target method signature, overload set, or route family changed;
- a rendered string is assembled dynamically from live fragments;
- a route belongs to the renderer and is outside QudJP ownership.

Review comments must not ask contributors to commit decompiled source, game
binaries, or copied upstream snippets. If a change needs upstream confirmation,
ask for a route note, L2G target-resolution test, runtime log, or focused test
case instead.

## Runtime Ownership

QudJP should translate at the earliest route where the text is stable enough to
define:

- the stable source text;
- the dynamic fragments;
- the markup and placeholders that must survive;
- the owner patch or asset destination;
- the regression tests that prove the behavior.

Prefer `producer-owned` or `mid-pipeline-owned` fixes. Do not promote a final UI
sink into a broad translation owner just because English text is visible there.
Common sink or near-sink paths such as `UITextSkin`, generic popup display,
`AddPlayerMessage`, and unresolved display-name buckets are usually
observation-only unless a route-specific contract proves otherwise.

When reviewing a translation bug fix, flag changes that:

- add a dictionary/XML entry to hide dynamic or procedural text at a sink;
- introduce sink-wide fallback behavior without route-specific tests;
- bypass an existing owner translator or owner patch;
- treat runtime observability hits as static coverage verdicts;
- skip the question of where ownership should live.

The expected end state is not "the visible English disappeared." The expected
end state is "the route has an explicit, test-backed owner or a documented
observation-only decision."

## Harmony Patch Contracts

Harmony patches must be crash-safe. Prefix/Postfix patch bodies are expected to
catch exceptions, log with `Trace.TraceError`, and fall back to the original
English/runtime behavior rather than breaking the game loop.

Target resolution changes should be reviewed against the test layer:

- use L2G tests for real game DLL target names, declaring types, return types,
  parameter lists, overload selection, and obsolete target avoidance;
- use L2 tests with dummy targets for Prefix/Postfix behavior, argument rewrites,
  `__result` rewrites, direct marker handling, and markup preservation;
- do not instantiate real game types in tests.

One patch class should live in one file under `Mods/QudJP/Assemblies/src/Patches/`.
Avoid broad rewrites of existing route families when a focused owner extension
or shared helper extraction is enough.

## Test Layer Contracts

Test categories are contractual:

- `L1`: pure logic only. No Harmony, no Unity runtime, no game DLL types.
- `L2`: Harmony integration against dummy targets with matching signatures.
- `L2G`: game-DLL-assisted target/signature verification without Unity runtime.
- `L3`: manual in-game smoke evidence.

New translation logic should include the narrowest regression tests that prove:

- the owner route is correct;
- Japanese output is produced, not merely that code runs;
- fallback to English remains safe on unknown or unsupported input;
- Qud markup, TMP markup, placeholders, and direct markers are preserved or
  stripped according to the route contract.

When a real runtime shape caused a bug, prefer turning that exact shape into a
regression test instead of inventing a cleaner synthetic string.

Game-DLL-assisted tests are conditional on local managed assemblies. Their
absence should not lead to committing `Assembly-CSharp.dll` or any other game
binary.

## Direct Translation Marker

The direct translation marker prevents downstream retranslation after an owner
route has already produced Japanese text. Review changes for these invariants:

- owner routes may use the marker only when their downstream path expects it;
- sink or late-stage code should strip the marker before display when the route
  contract requires visible text;
- translated assets and persisted narrative text should not leak the marker to
  players unless a test explicitly proves the marker is internal-only and removed
  before display;
- unknown marked text should pass through safely after marker handling instead
  of being retranslated.

## Observability Contracts

Runtime observability records are evidence. They are not proof that QudJP owns a
route and they are not a static coverage source of truth.

Review changes involving observability for these expectations:

- `DynamicTextProbe/v1`, `SinkObserve/v1`, missing-key logs, and final-output
  probes should keep their structured fields stable;
- escaping, hashing, throttling, route/family naming, and source/final text
  distinctions should remain deterministic;
- observation-only routes should log evidence without mutating visible text;
- Phase F runtime evidence should follow `docs/RULES.md` verification commands.

On Apple Silicon, only Rosetta-launched L3 runtime logs count as localization
observability evidence. Native ARM64 logs should not be accepted for that
purpose.

## Localization Asset Contracts

Use localization assets for true stable leaf strings, fixed labels, and atomic
names. Use C# owner patches or translators for dynamic or procedural text,
assembled fragments, live placeholders, injected names, and mixed markup.

A fixed-leaf candidate is acceptable only when it is:

- stable: the exact string shape is fixed;
- owner-safe: not a message-frame, builder/display-name, procedural,
  unresolved, or observation-only sink route;
- markup-preserving: every required token, tag, placeholder, and escape survives;
- not `needs_runtime`.

Before promoting candidates, prune pseudo-leaves and widget/channel identifiers
such as empty strings, whitespace-only strings, `BodyText`, and
`SelectedModLabel`.

Destination selection should prefer the narrowest safe home:

- scoped dictionaries for one screen, family, or producer route;
- global flat dictionaries only for exact, shared, proven fixed leaves.

`Translator` remains a flat exact-key lookup path. Route-aware decisions,
duplicate rejection, broad-entry rejection, and scoped destination choices belong
upstream in validation or owner patches.

## Markup, Tokens, And Text Integrity

Apply the detailed token rules in `docs/coderabbit/color-token-review.md`.

Reviewers should flag any change that drops, broadens, or normalizes runtime
markup without a test-backed route contract. Preserve these forms exactly unless
the route-specific test proves a deliberate transformation:

- Qud wrappers such as `{{W|text}}`;
- foreground codes such as `&G`;
- background codes such as `^r`;
- escaped forms such as `&&` and `^^`;
- TMP tags such as `<color=#44ff88>text</color>`;
- runtime placeholders such as `=variable.name=`;
- numeric placeholders such as `{0}` and `{12:format}`.

Color-aware restoration ownership belongs to `ColorAwareTranslationComposer`.
Do not add ad hoc color restoration in unrelated patches when the shared helper
already defines the route contract.

JSON dictionary entries should preserve the placeholder-index multiset from
`key` to `text`. Duplicate source-key conflicts are review failures unless an
explicit baseline allows them; divergent cross-file duplicates should be flagged.

## Glossary And Validation Gates

Apply the detailed terminology and variance rules in
`docs/coderabbit/glossary-variance-review.md`.

`docs/glossary.csv` is canonical for stable player-facing proper nouns and fixed
terminology when rows are `confirmed` or `approved`. New localized text should
follow those forms unless fresh tests or runtime evidence prove the glossary row
is wrong.

Relevant verification commands:

```bash
just build
just test-l1
just test-l2
just test-l2g
just python-check
just python-test
just localization-check
just translation-token-check
```

For localization assets, reviewers should expect UTF-8 without BOM, LF line
endings, valid XML/JSON structure, deterministic validation failures, and no
unbalanced color wrappers or missing source-side tokens.
