# issue-376 — Color tag application/restoration static analysis

Investigation-only scoping doc. No production code is shipped in the same commit;
this plan exists so the next implementation PR can run against a fixed contract.

## Problem statement

Display names rendered in combat / death / ability-bar paths exhibit two shapes
of color-tag corruption (see `gh issue view 376` body):

- Triple-nested `{{r|{{r|{{r|...}}}}}}` accumulation around the killer.
- Ampersand-prefix codes (`&r`, `&y`) splicing into Japanese bracketed tokens
  such as `[座っている]`, e.g. `[座ってい&yる]`.

Empirical pivots from prior PRs:

- `Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs:326-328` —
  the `HasColorMarkup` branch is the only place we currently guard against
  re-applying outer wrapper spans onto an already-marked-up localized killer.
  No equivalent guard exists in `MessagePatternTranslator` or
  `DescriptionTextTranslator`.
- `Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs:575` —
  capture-group `RestoreCapture` is called unconditionally; if the capture
  value carries its own markup the closing `}}` can land mid-token.
- `Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:85` —
  `RestoreCapture` is span-driven and has no notion of "the value already owns
  its own opening/closing markup".
- PR #432 lesson (`Grammar.InitCap`): runtime regex options
  `RegexOptions.Compiled | CultureInvariant` only uppercase ASCII `a-z`. A
  pattern's source-side casing is not the runtime casing. Color-tag analysis
  has the analogous gotcha — source-side `{{r|...}}` is not always the
  runtime-emitted shape; producers can emit `&r…&y` or `<color=…>…</color>`
  and the static catalog has to enumerate every shape.

## Detection strategy

Three anchor points, in priority order:

1. **Translator pipeline (preferred).** Add a "markup-aware" branch to every
   `RestoreCapture` call site, mirroring `DeathWrapperFamilyTranslator`'s
   `HasColorMarkup` guard. Producer-owned, deterministic, testable in L1.
2. **Validator (CI gate).** Static scan over `Mods/QudJP/Assemblies/src/` for
   `Strip` / `Restore` symmetry and for `RestoreCapture` calls that target
   name-like capture groups without a markup guard. Encodes the rule once and
   runs on every PR.
3. **Runtime observability (last resort).** Extend `DynamicTextObservability`
   to log "wrapper-double-restored" events. Useful for L3 smoke runs but not
   the source of truth.

False-positive mitigation:

- Allowlist file (path → reason). Each entry must cite the issue or PR that
  blessed the exemption. Example: pre-rendered fragments produced by sinks.
- Symbolic guards over regex-only matching. The validator must look for the
  guard *or* an equivalent abstraction (`HasColorMarkup`,
  `IsAlreadyLocalized*`, `MarkupAwareRestore`, …) — the catalog rule is "the
  guard exists", not "the guard is named `HasColorMarkup`".
- Runtime-shape enumeration. Catalog the producers that emit `&<letter>` and
  `<color=…>` separately from the `{{shader|…}}` form so the regex has full
  coverage.

## Test architecture per `docs/test-architecture.md`

Both files live in **L1** (pure C#, no HarmonyLib, no `Assembly-CSharp.dll`,
no UnityEngine — see `docs/test-architecture.md` for layer boundaries):

- `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorTagAllowlistCoverageTests.cs` —
  static catalog scans (Catalog-1, Catalog-2, Catalog-3 over
  `Mods/QudJP/Assemblies/src/`; Catalog-4 over
  `Mods/QudJP/Localization/Dictionaries/*.json`). The scan target is
  per-Catalog, not "all of L1 is `src/Patches/*.cs`".
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorTagStaticAnalysisTests.cs` —
  six representative drop-scenario tests that call the pure-C# translator
  surface (`ColorAwareTranslationComposer`, `DeathWrapperFamilyTranslator`,
  `MessagePatternTranslator`, `DescriptionTextTranslator`) directly. No
  HarmonyLib / DummyTarget, so L1 not L2.
- **No L0.** No math/algorithm core that can be tested in isolation from the
  translator surface.
- **No L2 / L2G.** None of the scenarios need Harmony patch resolution or
  game-DLL signature integration to verify the contract. Phase F runtime
  proof remains separate per `docs/RULES.md`.
- **No L3 in the first cut.** Once the L1 layer is green we add a smoke
  step that diffs a fresh `Player.log`. Captured separately in the
  acceptance criteria below.

Skip-pattern note: the prompt asked for `[Fact(Skip = "...")]` (xUnit). The
QudJP test project is NUnit. We use `[Test, Ignore("issue-376 — production
code pending")]` which has the same "discoverable but not run" semantics.

## Implementation phases

### Phase A — catalog of expected tag-flow shapes

1. Enumerate every Patches/translator file that calls `Strip` or
   `RestoreCapture`. Group by route family
   (death, popup, message-pattern, description, journal, owner, observation).
2. For each call site, record:
   `(path, line, capture-group-name, has-markup-guard, owner-or-observation)`.
3. Land the catalog as a `SortedDictionary` next to
   `ColorRouteCatalog.ExpectedSymbolOccurrences` so it is human-reviewable
   and PR-diffable.

### Phase B — detection layer

1. L1 catalog test: every `Strip` has a matching `Restore*` (with allowlist).
2. L1 catalog test: every `RestoreCapture` on a name-like group has a
   markup guard in the same file (allowlist exempts pre-rendered sinks).
3. L1 translator-surface tests for the six representative drop scenarios in
   `ColorTagStaticAnalysisTests.cs` (four canonical drops from the issue body
   plus two adjacent-translator contract tests for `MessagePatternTranslator`
   and `DescriptionTextTranslator`).

### Phase C — regression guard wiring

1. Add `MarkupAwareRestoreCapture(value, spans, group)` to
   `ColorAwareTranslationComposer` that delegates to `RestoreCapture` only
   when `!HasColorMarkup(value)`.
2. Migrate the `MessagePatternTranslator.cs:575` call site first (highest
   blast radius). The L1 translator-surface tests must flip from skipped
   to passing.
3. Migrate remaining call sites flagged by the L1 catalog.
4. Tighten the L1 allowlist to forbid the un-guarded `RestoreCapture` shape
   project-wide.

## Cross-references

- **#401 (ColorRoute).** Closed. JSON-dictionary token-loss bug. Fixed at the
  dictionary level. Issue-376 is *runtime* tag flow — a different layer.
  Borrow #401's token-multiset invariant for the L1 dictionary corpus check
  (Catalog-4) but do not reopen #401's surface.
- **#404 (Preacher color).** Closed. Single unbalanced `{{W|` in Creatures.jp.xml.
  Issue-376 catalog covers the *general* shape; #404's specific entry is a
  one-off that is already fixed in shipped XML.
- **#409 (translation consistency CI gate).** Open. Tech-debt umbrella for the
  CI gate side. Defer Catalog-4 (dictionary balance) to #409 if the gate
  already covers it; otherwise land it here and reference back.

## Acceptance criteria

- The six representative drop scenarios in `ColorTagStaticAnalysisTests.cs`
  flip from skipped to passing under one production PR (no skips remain
  cited to issue-376 outside intentional, allowlisted exemptions).
- The L1 catalog asserts non-empty coverage and rejects new `Strip`
  call sites that lack a paired `Restore*`.
- A fresh `Player.log` from a death-popup smoke run shows zero
  `{{r|{{r|...}}` occurrences and zero `&<letter>` codes inside Japanese
  bracketed tokens.
- `dotnet test ... --filter TestCategory=L1` remains green (target counts:
  current totals + new tests, all passing). L2 / L2G are not affected by
  this work.

## Open questions

1. **Allowlist storage.** Inline `HashSet<string>` in the L1 test, or a
   committed JSON file under `scripts/_artifacts/` like
   `validate_xml_warning_baseline.json`? Latter mirrors existing tooling.
2. **Sink-vs-owner classification.** The catalog distinguishes "owner"
   from "observation-only" routes. Do we trust `docs/RULES.md` as the
   single source of truth, or do we re-derive the classification from
   `ColorRouteCatalog.ExpectedSymbolOccurrences`? Mixed signals exist
   today.
3. **Runtime shape coverage.** Should phase A enumerate `&<letter>` and
   `<color=…>` producer paths via decompiled-source scan
   (`~/dev/coq-decompiled_stable/`) or rely on runtime log evidence
   only? Decompiled scan is more thorough but not committable.
