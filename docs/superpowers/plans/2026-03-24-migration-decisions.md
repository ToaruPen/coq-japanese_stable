# No-Fallback Migration Decisions

Date: 2026-03-24

Scope: architectural decisions for Phases 2-6 of the no-fallback migration.

Inputs reviewed:
- `docs/superpowers/plans/2026-03-23-no-fallback-architecture.md`
- `docs/source-first-design.md`
- `docs/test-architecture.md`
- `Mods/QudJP/Assemblies/src/` translators and patches
- `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`
- parallel review from `omo-explore`, `omo-prometheus`, `omo-oracle`, `omo-momus`

## Evidence snapshot

Current local `messages.ja.json` contains **517** patterns, not 359.

Route counts in the current working tree:

| Route | Count |
| --- | ---: |
| `does-verb` | 157 |
| `emit-message` | 111 |
| `unclassified` | 129 |
| `leaf` | 54 |
| `message-frame` | 37 |
| `journal` | 24 |
| `popup` | 5 |

Important caveat: there is count drift across docs and PR descriptions.

- The no-fallback architecture memo still discusses **359** patterns.
- GitHub PR #86 says `messages.ja.json: 359 -> 550` and mentions **248** added patterns.
- The local working tree currently has **517** patterns.

For migration execution, use the **local file in the repo** as the source of truth, and refresh any planning spreadsheet/manifests before cutting a sink.

Another key fact: although every pattern now has a `route` field in `messages.ja.json`, the runtime `MessagePatternTranslator` does **not** deserialize or use `route`. At runtime it is still a global first-match-wins pool keyed only by `pattern` and `template`.

That means route metadata is currently planning metadata, not ownership enforcement.

---

## Question 1: Pattern migration destinations

### Recommended approach

Do **not** refactor the current global `MessagePatternTranslator` into a new global family of route-scoped sink translators for every route.

Recommended end-state:

- `journal` is the only route that should keep pattern-based translation as a legitimate producer-side mechanism.
- `message-frame` should move into existing producer-side frame translation (`XDidYTranslationPatch` + `MessageFrameTranslator` + `verbs.ja.json`).
- `does-verb` should move to **producer/call-site ownership**, usually via a new `Does()` composition seam that reuses `MessageFrameTranslator`/`verbs.ja.json` where possible.
- `emit-message` should move to **owner-specific producer patches**, not a new global regex pool.
- `leaf` should move to **exact dictionaries** owned by the route or domain.
- `popup` should move to **popup-producing patches**, not stay in `PopupTranslationPatch`.
- `unclassified` is not a destination; it must be reclassified before the sink is cut.

### Route-by-route destination map

| Route | Recommended physical destination | Concrete home |
| --- | --- | --- |
| `message-frame` | Producer-side frame translation | `Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs`, `Mods/QudJP/Assemblies/src/MessageFrameTranslator.cs`, `Mods/QudJP/Localization/MessageFrames/verbs.ja.json` |
| `does-verb` | Producer/caller-owned verb-composition translation | New producer-side helper(s), e.g. `Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs` or family-specific helpers, fed by scanner-identified `Does()` call sites; reuse `MessageFrameTranslator` and `verbs.ja.json` where the family normalizes to `(verb, extra)` |
| `emit-message` | Owner-specific producer patches | Producer-family patches under `Mods/QudJP/Assemblies/src/Patches/*`, plus family helpers such as `*FamilyTranslator.cs`; data should move to exact/template dictionary entries or structured helpers, not stay in `messages.ja.json` |
| `popup` | Popup-producing route patches | New popup-owner patches in `Mods/QudJP/Assemblies/src/Patches/` for the actual game methods that create the popup text; template strings can stay as exact dictionary/template keys via `Translator.Translate(...)` |
| `journal` | Journal-scoped pattern translator | Keep the logic in `Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs`, but split the route-specific pattern subset out of the global pool into a dedicated asset/class such as `JournalPatternTranslator` |
| `leaf` | Exact dictionary entries | Route/domain dictionaries under `Mods/QudJP/Localization/Dictionaries/*.ja.json`, consumed by the owning route patch via `Translator.Translate(...)` or `StringHelpers.TranslateExactOrLowerAscii(...)` |
| `unclassified` | No runtime destination yet | Keep only as a migration manifest until reclassified; do not create a new shipping translator for this bucket |

### What should happen to `MessagePatternTranslator`

Recommended outcome:

- Remove `MessageLogPatch -> MessagePatternTranslator`.
- Remove `PopupTranslationPatch -> MessagePatternTranslator`.
- Keep pattern matching only for `JournalTextTranslator`, after extracting the journal subset into a journal-scoped asset/class.

That makes `MessagePatternTranslator` either:

- a renamed `JournalPatternTranslator`, or
- a thin generic engine only invoked by `JournalTextTranslator` with a journal-only asset.

Do **not** keep it as a global rescue pool.

### What happens to the journal route

`journal` is the one legitimate producer-side user of pattern translation today.

Current intentional call sites:
- `JournalTextTranslator.TryTranslateDisplayText(...)`
- `JournalTextTranslator.TryTranslateLines(...)`
- invoked by `JournalEntryDisplayTextPatch` and `JournalMapNoteDisplayTextPatch`

Recommendation:

- Keep journal pattern matching.
- Scope it explicitly to journal.
- Treat the journal subset as part of the route owner, not part of the sink fallback system.

### Main risk and mitigation

Main risk: treating route tags as if they already imply runtime ownership.

Why this is risky:
- runtime still ignores `route` in `MessagePatternTranslator`
- `does-verb` and `emit-message` are producer-family problems, not just sink-route labels
- `UITextSkin` route attribution is still partly stack-inferred, not fully authoritative

Mitigation:
- make a route-owner manifest before moving patterns
- define “migrated” as “the owner no longer delegates translation decisions to a generic sink helper”
- keep `unclassified` out of cutover decisions until reclassified

### Concrete next steps

1. Generate a migration manifest from the current `messages.ja.json` with one row per pattern: current route, proposed owner, concrete destination file/class/method, and status.
2. Extract the `journal` subset first into a journal-only asset/class.
3. Convert `message-frame` rows into `verbs.ja.json` or explicit `MessageFrameTranslator` templates.
4. Create a `does-verb` worklist from scanner output; do not treat it as sink-owned text anymore.
5. For `leaf`, move capture-free rows out of `patterns` and into exact dictionaries.
6. Freeze new additions to global `messages.ja.json` for non-journal routes.

---

## Question 2: Migration cutover strategy

### Option A: All-at-once per sink

Definition: complete the producer work for a given sink, then flip that whole sink to observation-only in one change.

#### How it would be implemented

For each sink:

- `MessageLogPatch`: keep marker stripping, already-localized checks, and `SinkObservation`, but remove the call to `MessagePatternTranslator.Translate(...)`.
- `PopupTranslationPatch`: keep marker stripping, menu-item normalization if still considered route-owned, and `SinkObservation`, but remove both downstream fallback edges:
  - `PopupTranslationPatch -> MessagePatternTranslator`
  - `PopupTranslationPatch -> UITextSkinTranslationPatch`
- `UITextSkinTranslationPatch`: keep marker stripping and observation, but remove route-specific mutation branches and final `Translator.Translate(stripped)` for the sink once the sink is fully retired.

#### Regression risk

- `PopupTranslationPatch`: moderate, but bounded.
- `MessageLogPatch`: moderate-high, because it currently rescues producer misses.
- `UITextSkinTranslationPatch`: **very high** if attempted globally; it still behaves as a hidden service layer for many wrappers.

#### Verification

Before flip:
- route-owner manifest complete for that sink
- no expected untranslated traffic for that sink in Rosetta L3 baseline
- L1/L2/L2G green for all families moved upstream

After flip:
- no unexpected `SinkObserve/v1` spikes for that sink
- no previously translated strings reverting to English in scenario smoke runs

### Option B: Route-by-route within each sink

Definition: leave the sink alive, but selectively disable sink translation for specific routes as each route owner becomes ready.

#### How it would be implemented

Important constraint: the current runtime does **not** use the `route` field from `messages.ja.json`, so this cannot be implemented by editing JSON only.

Implementation has to happen in the sink code itself, keyed on the explicit route/context reaching the sink.

Examples:

- `PopupTranslationPatch.TranslatePopupTextForRoute(source, route)` can skip sink translation for retired routes because it already has an explicit `route` argument.
- `UITextSkinTranslationPatch.TranslatePreservingColors(source, context)` can disable specific branches per resolved primary route, but this is only safe for contexts that are passed explicitly by wrapper patches. It is unsafe to rely on stack-inferred routes as the main cutover switch.

#### Regression risk

- Lower blast radius than a full sink flip.
- But higher ambiguity risk if ownership is not exclusive.
- Especially dangerous in `UITextSkin` because current route inference is partly stack-based and cached by source text.

#### Verification

- Per-route L1/L2 regression tests
- For retired routes, assert that sink mutation no longer happens
- For non-retired routes, assert behavior remains unchanged
- L3 smoke on that route only

### Option C: Runtime flag

Definition: add a config flag to toggle fallback ON/OFF at runtime.

#### How it would be implemented

- Add a global configuration value checked in `MessageLogPatch`, `PopupTranslationPatch`, and `UITextSkinTranslationPatch`.
- Each sink would branch between current fallback behavior and observation-only behavior.

#### Regression risk

- Lowest rollback cost
- but highest long-term maintenance cost
- doubles the number of modes that must be tested
- invites “it only works with fallback enabled” drift

For a game mod, this also risks users ending up in a partially migrated, partially untranslated mode without realizing it.

#### Verification

- Every sink test must run in both flag states
- L3 smoke must cover both modes if the flag is shipped to users

### Recommended choice

Use a **hybrid of A and B**:

- Use **A (all-at-once per sink)** for `PopupTranslationPatch` and `MessageLogPatch`, because they have bounded or explicit responsibilities and clearer cut points.
- Use **B (route-by-route)** only for `UITextSkinTranslationPatch`, because it multiplexes too many unrelated routes to flip safely in one shot.
- Do **not** ship **C (runtime flag)** as a user-facing feature. If a temporary developer-only debug switch is needed during bring-up, keep it local and non-productized.

This is also the safest shape for a Harmony-patched game mod:

- bounded cutover where ownership is explicit
- no permanent dual-path architecture
- no global `UITextSkin` big bang

### Main risk and mitigation

Main risk: removing sink rescue before the rescued families are fully enumerated.

This is most dangerous for:
- `MessageLogPatch`, because `XDidYTranslationPatch` still falls back to English when verb lookup fails and the sink may currently salvage that output
- `PopupTranslationPatch`, because popup chains currently fall through into `UITextSkinTranslationPatch`
- `UITextSkinTranslationPatch`, because wrappers still call it directly and some route attribution is guess-based

Mitigation:
- never cut a fallback edge without a concrete rescued-family inventory
- use Rosetta L3 observation data as a hard gate before any sink flip
- for `UITextSkin`, only retire routes whose context is explicit and whose wrapper no longer delegates decisions to generic sink logic

### Concrete next steps

1. Adopt the cut order already suggested by the architecture memo: popup -> message log -> `UITextSkin` per route -> display names last.
2. Before cutting popup, enumerate every popup family currently rescued by `MessagePatternTranslator` or `UITextSkinTranslationPatch`.
3. Before cutting message-log, enumerate every producer miss that the log sink currently rescues.
4. Before any `UITextSkin` route cut, require explicit route context and a wrapper that no longer depends on generic sink decision-making.
5. Do not add a shipped runtime fallback flag.

---

## Question 3: Testing strategy for producer migration

### Recommended approach

Use the existing L1/L2/L2G/L3 structure, but add a **route-aware coverage regression layer** and one missing **end-to-end producer→sink contract test**.

#### What can be reused immediately

Existing assets already cover most of the migration surfaces:

- `MessagePatternTranslatorTests` — current source/expected corpus for many sink-era families
- `DoesVerbFamilyTests` — large corpus for `does-verb` families
- `MessageFrameTranslatorTests` — pure logic for verb/extra/template assembly
- `XDidYTranslationPatchTests` — L2 producer-side dispatch and marker behavior
- `PopupTranslationPatchTests` — popup sink behavior and popup template families
- `JournalEntryDisplayTextPatchTests` / `JournalMapNoteDisplayTextPatchTests` — journal producer ownership
- `UITextSkinTranslationPatchTests` — current sink route behavior and observability
- `LocalizationCoverageTests` — exact dictionary completeness checks for some domains

These should be treated as **golden Japanese output fixtures**, not as permanent proof that sink ownership is acceptable.

#### What should be added

1. **Translation coverage regression test**

Create a new route-aware regression suite that proves all currently covered examples still produce the same Japanese output after ownership moves.

Recommended shape:

- message-frame cases run through `MessageFrameTranslator`/`XDidYTranslationPatch`
- journal cases run through `JournalTextTranslator`
- popup cases run through their new producer-owned seam
- exact leaf cases run through the owning route patch or dictionary lookup
- only the remaining journal-scoped pattern cases use pattern matching

This should not be a single global sink test; it should be organized by owner route.

2. **End-to-end producer→sink contract test**

Add an integration test that proves:

1. producer translates upstream
2. producer prepends `\x01`
3. downstream sink strips `\x01`
4. sink does not retranslate
5. sink does not log the message as unclaimed

This is the core no-fallback contract, and current tests mostly exercise producer and sink behavior separately.

3. **Route manifest diff test/tooling**

Add a validation step that compares the migration manifest before and after each route move so ownership changes are explicit, not inferred.

### Is L3 necessary?

Yes — at specific milestones.

L3 is not required for every individual producer family, but it **is required**:

- before the first sink cut on each sink class
- after cutting popup
- after cutting message-log
- after each significant `UITextSkin` route batch
- before any display-name migration acceptance

Reason:
- only L3 proves real screen/update order and actual runtime presentation
- only L3 verifies there is no hidden dependence on stack shape, UI update timing, or routed wrapper behavior

### How SinkObservation helps

`SinkObservation` is now the migration safety net.

Use it as a gating signal:

- establish a Rosetta L3 baseline before cutting a sink
- after a route/sink cut, compare `SinkObserve/v1` deltas
- if a retired route suddenly starts producing many unclaimed observations, ownership is incomplete

Also use `DynamicTextObservability` to confirm the new owner is transforming text on the intended route/family, not just producing equivalent output by accident.

### Main risk and mitigation

Main risk: mistaking L1/L2 translation equivalence for full producer equivalence.

Why this matters:
- L1 proves string transformation logic, not that the real producer still emits the same family shape
- L2 DummyTarget tests prove patch logic, not real game provenance
- L2G proves target resolution/signature validity, not end-to-end output equivalence

Mitigation:
- use L1/L2/L2G together, not as substitutes
- add the end-to-end producer→sink contract test
- require targeted L3 smoke at each sink cutover or major UI-route batch

### Concrete next steps

1. Extract a reusable golden-case corpus from existing pattern and route tests.
2. Add a `TranslationCoverageRegressionTests` suite organized by destination owner.
3. Add one explicit end-to-end marker-strip/no-retranslate/no-unclaimed integration test.
4. Make Rosetta L3 observation capture a mandatory artifact before and after each sink cut.
5. Add a simple manifest diff step to every migration PR.

---

## Question 4: Handling PR #86 patterns

### Recommended approach

Treat PR #86 patterns as a **migration inventory and Japanese-output oracle**, not as the final runtime architecture.

The translation work is **not** wasted.

What is reusable:
- Japanese phrasing
- capture ordering
- punctuation choices
- family grouping hints
- golden expected outputs for tests
- many `message-frame` and `does-verb` families that can be converted into structured producer entries

What is not final:
- keeping those families in the global sink-era `messages.ja.json` pool
- continuing to grow the global pool as the default answer for non-journal routes

### Is it double work?

Not exactly.

The expensive part is usually not just typing Japanese text, but deciding:
- the correct family boundary
- correct capture order
- whether `{tN}` translation is needed
- punctuation/markup handling
- the expected Japanese output

PR #86 already paid much of that cost.

The remaining work is mostly **ownership relocation**:
- source tracing
- deciding the correct producer seam
- converting regex/template content into exact keys, verb/extra entries, or route-owned helpers
- replacing sink rescue with producer ownership

That relocation work is real, but it is not a full rewrite of the translation content.

### How much effort is “move to producer” vs “create translation”?

Roughly:

- `leaf`: move is usually cheaper than authoring; this is mostly normalization and dictionary relocation.
- `popup`: move is moderate; only a few families, mostly template-key relocation and owner patching.
- `message-frame`: move is moderate; many can be normalized into `verbs.ja.json` tiers or explicit frame templates.
- `does-verb`: move is often **harder** than the original pattern authoring, because the ownership seam is call-site specific and `Does()` is not the same seam as `DidX`.
- `emit-message` / `unclassified`: move cost varies widely and can exceed original authoring when the real producer path is still unclear.

So the content is reusable, but the **routing debt** is still substantial for `does-verb`, `emit-message`, and `unclassified`.

### Should the remaining work still go through `messages.ja.json`?

Default answer: **no**.

New policy for remaining work:

- If the contract is `MessageFrame`, `VerbComposition`, `Template`, or `Builder`, go straight to the producer-side route or owner patch.
- Only use pattern-based `messages.ja.json` as a final runtime home for `journal`, or for a very short-lived, explicitly temporary quarantine during investigation.
- Do not keep adding non-journal sink-era patterns to the global pool.

In other words, PR #86 should be treated as the **last large sink-era batch**, not the model for future work.

### Main risk and mitigation

Main risk: false confidence from good coverage in the wrong layer.

PR #86 improved coverage quickly, but it also deepened dependence on:
- ordered global regex matching
- route metadata that runtime ignores
- hidden downstream salvage from message-log and popup sinks

Mitigation:
- keep PR #86 outputs as fixtures/oracles
- stop using the global pattern pool as the default destination for new non-journal work
- convert low-risk classes first (`leaf`, `popup`, `message-frame`, `journal` scoping), then tackle `does-verb`/`emit-message`/`unclassified`

### Concrete next steps

1. Freeze new non-journal additions to the global `messages.ja.json` pattern pool.
2. Reuse PR #86 pattern content as a migration manifest plus regression fixtures.
3. Convert the easiest reusable classes first:
   - `leaf`
   - `popup`
   - `message-frame`
   - journal scoping
4. Start a dedicated `does-verb` producer-ownership workstream from scanner output.
5. Update any triage tooling or workflow guidance that still says “message/popup miss -> add another regex pattern.”

---

## Final recommendations

1. **Destination model**
   - `message-frame` -> `XDidYTranslationPatch` + `MessageFrameTranslator` + `verbs.ja.json`
   - `does-verb` -> producer/call-site ownership, ideally reusing `MessageFrameTranslator` where the family normalizes cleanly
   - `emit-message` -> owner-specific producer patches/family translators
   - `popup` -> popup-producing patches, not popup sink fallback
   - `journal` -> journal-scoped pattern translator
   - `leaf` -> exact dictionaries
   - `unclassified` -> reclassify before cutover

2. **Cutover strategy**
   - Popup: all-at-once per sink once rescued families are enumerated
   - Message log: all-at-once per sink once producer misses are closed
   - `UITextSkin`: route-by-route only
   - No shipped runtime fallback flag

3. **Testing strategy**
   - Reuse current L1/L2 tests as golden output corpus
   - Add route-aware coverage regression tests
   - Add one end-to-end marker-strip/no-retranslate contract test
   - Require Rosetta L3 observation baselines before and after each sink cut

4. **PR #86 interpretation**
   - Not wasted work
   - Reusable translation content and test oracles
   - But it should be the last major sink-era batch, not the model for future non-journal work

## Immediate next PR sequence

1. Extract and scope `journal` patterns.
2. Move `leaf` patterns to exact dictionaries.
3. Convert `message-frame` patterns into `verbs.ja.json` / `MessageFrameTranslator` entries.
4. Build the `does-verb` owner manifest from scanner output.
5. Enumerate popup-rescued families and cut popup fallback.
6. Enumerate message-log-rescued families and cut message-log fallback.
7. Retire `UITextSkin` routes one explicit route at a time.
