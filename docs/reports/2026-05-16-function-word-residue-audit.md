# Function-Word Residue Audit

Date: 2026-05-16

## Scope

This report records the first deterministic shelf for QudJP English
function-word residue: articles, possessives, directional preposition phrases,
`Grammar.MakePossessive` / `poss(...)` owner composition, `Does(...)` message
composition, and bracketed display-name state suffixes.

Generated artifact:

```bash
just function-word-residue-audit /Users/toarupen/dev/coq-decompiled_stable \
  Mods/QudJP/Assemblies/QudJP.Tests \
  /tmp/qudjp-function-word-residue-audit.json \
  20
```

Summary from `/tmp/qudjp-function-word-residue-audit.json`:

| Category | Hits |
|---|---:|
| `visible_string_function_word` | 1274 |
| `generated_article_call` | 450 |
| `does_message_frame_composition` | 309 |
| `test_expectation_function_word` | 153 |
| `possessive_composition` | 139 |
| `grammar_make_possessive` | 137 |
| `test_expectation_bracketed_state` | 5 |
| `bracketed_state_suffix` | 2 |
| `test_expectation_direction` | 1 |

Domain totals:

| Domain | Hits |
|---|---:|
| source | 2311 |
| test | 159 |

## High-Confidence Runtime-Aligned Fix Targets

- `Does(...)` route: `XRL.World.Effects/ShatteredArmor.cs:156`
  composes `Object.Does("were") + " cracked."`, which produced
  `Your 毛皮にひびが入った` in the latest `Player.log`. Fix at
  `DoesVerbRouteTranslator` subject normalization or the owning generated
  effect route, not with a mixed-output sink pattern.
- `Grammar.MakePossessive`: latest log showed
  `the {{B|濡れた}}グロウフィッシュの`. Fix
  `GrammarMakePossessivePatch` to strip leading English articles before
  appending `の`.
- Display-name state suffixes: tests and runtime show
  `{{B|濡れた}}グロウフィッシュ [swimming]`, `... [sitting]`,
  `... [sitting on a chair]`, and `水袋 [empty]`. Fix display-name state
  suffix routing and then update stale expectations.

## Stale Test Expectations Found

The scanner found 159 test-side candidates. High-confidence examples:

- `Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs:398`
  expects `{{B|濡れた}}グロウフィッシュ [swimming]`.
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/GetDisplayNameProcessPatchTests.cs:255`
  expects `タム, dromad merchant [sitting]`.
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/GetDisplayNameProcessPatchTests.cs:296`
  expects `タム, dromad merchant [sitting on a chair]`.
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/GetDisplayNameProcessPatchTests.cs:316`
  expects `水袋 [empty]`.
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs:298`
  expects `You see タム、ドロマド商人 to the east and stop moving.`

Additional prior subagent audit notes remain relevant:

- MessageFrame raw `{0}` placeholders preserve articles in generated noun
  phrases such as `the form of {0}`, `{0} away`, and `at {0} menacingly`.
  Prefer `{tN}` or a route-specific generated-noun normalizer where the slot is
  semantically a noun phrase.
- MessagePattern generic leading captures such as `^(.+?) ...` can capture
  `The ...` / `An ...`; either move the article outside the capture or use
  translated placeholders.
- XDidY tests do not cover several owner/direction combinations:
  `IndefiniteSubject`, `IndefiniteObjectForOthers`, possessive object labels,
  owner prefixes, and `DescribeSubjectDirection*`.

## Intentional-English Review Notes

The scanner is intentionally broad. Before changing a hit, allow intentional
English such as:

- hotkeys and controller buttons (`[Esc]`, `Ctrl+A`, `A/B/Y`),
- stat abbreviations and dice/stat UI (`1d6`, `PV`, `AV`, `HP`),
- authored quoted English and proper nouns,
- direct-marker fallback cases explicitly testing pass-through behavior,
- HistorySpice tokens and prompt-language fragments that need a separate owner
  route decision.

## Implemented Closure Batch

This batch fixed the runtime-aligned and high-confidence owner routes without
adding a broad sink fallback:

- `DoesVerbRouteTranslator` strips `Your ...` player-owned subjects before
  message-frame lookup, while preserving whole-line color wrappers and
  subject-local markup.
- `GrammarMakePossessivePatch` strips leading `a` / `an` / `the` before
  appending `の`.
- `GetDisplayNameRouteTranslator` now has real dictionary coverage for
  `[swimming]`.
- Active-effect labels now cover `bloody wet`.
- `InventoryLineTranslationPatch` routes item names through the display-name
  owner path after exact lookup fails, which fixes `water flask [empty]`.
- Combat-skill, liquid-loader, damage-frame, Sifrah token item, and tinkering
  owner routes normalize captured labels so leading articles and `your` /
  `its` / possessive forms do not leak into Japanese particles.

Final deterministic shelf generated to:

```bash
/tmp/qudjp-function-word-residue-audit-final.json
```

Final summary:

| Category | Hits |
|---|---:|
| `visible_string_function_word` | 1274 |
| `generated_article_call` | 450 |
| `does_message_frame_composition` | 309 |
| `possessive_composition` | 139 |
| `grammar_make_possessive` | 137 |
| `test_expectation_function_word` | 130 |
| `test_expectation_bracketed_state` | 4 |
| `bracketed_state_suffix` | 2 |
| `test_expectation_direction` | 1 |

The remaining static source counts are candidate producer shapes, not live
residue proof. The remaining test hits are mostly source literals,
observation-only pass-through tests, hotkeys, quoted authored English, proper
nouns, and HistorySpice/template fragments. Treat them as the next triage shelf,
not as a blanket instruction to add sink-level stripping.

## Follow-up Closure Batch

A later pass closed additional owner-routed function-word residue discovered
from the remaining test shelf:

- `JournalPatternTranslator` and `MessagePatternTranslator` now strip leading
  articles from `{tN}` captures even when the capture falls back to untranslated
  English. This fixes journal entries such as `a chrome pyramid`,
  `a forgotten ruin`, `a snapjaw scavenger`, and `the Mechanimists`.
- `MessagePatternTranslator` also translates `yourself` and possessive-pronoun
  captures such as `your clone` before Japanese particles are appended.
- `MutationGeneratedTextTranslationPatch` normalizes belcher generated labels,
  stripping `a` / `the` from generated subject/object slots without invoking a
  sink fallback.
- `CudgelConkPopupTranslationPatch` normalizes `yourself` and `the head` in the
  owner route.
- Generated random statue labels now include the `brass` component, allowing
  `brass statue of a ...` to route through the existing generated-statue owner
  translator.

Latest deterministic shelf generated to:

```bash
/tmp/qudjp-function-word-residue-audit-final-goal.json
```

Latest summary:

| Category | Hits |
|---|---:|
| `visible_string_function_word` | 1274 |
| `generated_article_call` | 450 |
| `does_message_frame_composition` | 309 |
| `possessive_composition` | 139 |
| `grammar_make_possessive` | 137 |
| `test_expectation_function_word` | 109 |
| `test_expectation_bracketed_state` | 4 |
| `bracketed_state_suffix` | 2 |
| `test_expectation_direction` | 1 |

The remaining test-side entries are dominated by intentional English controls,
source literals, fallback/pass-through assertions, hotkeys, pronoun tokens,
proper nouns, and broad static producer candidates. They should stay on the
audit shelf unless fresh runtime evidence or a route-specific test proves they
are user-visible residue.

## Classified Residue Shelf

The audit now emits `classification`, `risk`, and `owner_route_hint` for each
entry so the remaining shelf can be split into fix candidates, stale tests,
fixtures, and intentional English instead of treating all hits equally.

Latest classified shelf generated to:

```bash
/tmp/qudjp-function-word-residue-audit-classified.json
```

Classification summary:

| Classification | Hits | Meaning |
|---|---:|---|
| `visible_literal_route_candidate` | 1074 | Source literal needs producer-route proof before changing. |
| `owner_route_candidate` | 585 | Owner-route composition such as `Does`, `poss`, or `Grammar.MakePossessive`. |
| `generated_display_name_candidate` | 450 | Generated noun/display-name route can introduce articles. |
| `static_visible_literal_shelf` | 200 | Broad static hit, low confidence without runtime evidence. |
| `intentional_english_allow` | 38 | Hotkeys, UI tokens, pronouns, proper nouns, or quoted English. |
| `localized_expectation_review` | 27 | Test expectation still needs manual route classification. |
| `stale_test_particle_boundary_candidate` | 24 | Test likely preserves article/possessive residue before a Japanese particle. |
| `pass_through_or_fixture` | 15 | Explicit fallback, source-literal, or pass-through fixture. |
| `mixed_sentence_owner_route_candidate` | 7 | Test shows mixed English/Japanese sentence likely needing owner-route review. |
| `stale_test_display_state_candidate` | 3 | Test likely preserves an English display-name state suffix. |
| `display_name_state_candidate` | 2 | Source route can emit bracketed display-name state suffixes. |

Risk summary:

| Risk | Hits |
|---|---:|
| `medium` | 1563 |
| `high` | 609 |
| `low` | 200 |
| `intentional` | 38 |
| `observation` | 15 |

Use `high` test classifications as the next stale-test cleanup queue. Use
`owner_route_candidate` and `generated_display_name_candidate` as static owner
route inventory, but require runtime evidence or a focused route test before
adding implementation fixes. `intentional_english_allow` and
`pass_through_or_fixture` are not closure blockers.

## Display-Name Modifier Audit

Static follow-up over decompiled `AddAdjective`, `RequireAdjective`, and
`AddTag` display-name producers found additional display-name modifier coverage
outside the original `[swimming]` example.

Fixed dictionary gaps:

- 8 literal `AddAdjective` / `RequireAdjective` values:
  `burrowing`, `dazzling`, `cryptic`, `plasmatic`, `rank`, `conjoined`,
  `magnetized`, and `fungus-ridden`.
- 18 bracketed `AddTag` state labels:
  `[1 cooking serving]`, `[deactivated]`, `[chapter unspecified]`,
  `[no cells]`, `[1 cell]`, `[raised]`, `[furled]`, `[hanging]`,
  `[on ground]`, `[no cell]`, `[flying]`, `[prone]`, `[wading]`,
  `[broken]`, `[tomb-tethered]`, `[cracked]`, `[rusted]`, and `[sitting]`.
- `stuck` / `grabbed` display states and templates:
  `stuck in {0}` and `grabbed by {0}`.

Dynamic bracketed `AddTag` producers now have route coverage for:

- timer suffixes such as `[10 sec]`,
- cooking serving counts such as `[3 cooking servings]`,
- cell counts such as `[2 cells]`,
- faction chapter labels such as `[Hindren chapter]`,
- object-name state labels such as `[lead slug]`,
- existing template families such as `sitting on`, `lying on`, `enclosed in`,
  `engulfed by`, and `auto-collecting`.

The remaining statically observed full bracket label is `[EMP]`, which is kept
as an intentional acronym rather than a translation gap.
