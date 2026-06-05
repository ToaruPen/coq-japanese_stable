# Issue 815 HistorySpice Dictionary Route Triage

This note records the direct-dictionary portion of the Issue 815 handoff.
The task was to continue expanding missing HistorySpice vocabulary by route
without promoting generated templates into exact dictionary leaves.

## Evidence

Command:

```bash
uv run python scripts/historyspice_vocabulary_coverage.py \
  "$HOME/Games/CavesOfQud-stable-1.0.4-public-build20241062/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/HistorySpice.json" \
  --format json
```

Current counts after this slice:

| Set | Covered | Missing | Coverage |
| --- | ---: | ---: | ---: |
| HSE dictionaries | 3119 / 3897 | 778 | 80.04% |
| All JSON dictionaries | 3232 / 3897 | 665 | 82.94% |

Focused group counts:

| HistorySpice group | Covered | Missing | Route decision |
| --- | ---: | ---: | --- |
| `spice.commonPhrases.*` | 859 / 860 | 1 | Two safe spouse-family leaves closed; remaining item is generated. |
| `spice.instancesOf.*` | 471 / 479 | 8 | Remaining items are generated fragments or grammar-sensitive connectors. |
| `spice.cooking.*` | 818 / 838 | 20 | Remaining items are cooking owner-route text, grammar fragments, or placeholders. |
| `spice.cooking.recipeNames.*` | 525 / 531 | 6 | Remaining items are recipe-name grammar or display-name placeholders. |
| `spice.gossip.*` | 5 / 32 | 27 | Remaining items are generated journal patterns. |
| `spice.proverbs*` | 19 / 23 | 4 | Remaining items are generated proverb templates. |

## Closed Direct Leaf

`spice.commonPhrases.civicSocialWork` in game `Base/HistorySpice.json` includes
the fixed spouse-family leaves `husband` and `wife`. The scoped HistorySpice
dictionary now covers those exact source leaves with `夫` and `妻`.

These are fixed component leaves. They are safe as scoped HistorySpice vocabulary
because they are not symbolic paths, placeholder templates, or owner-route frames.

## Deferred Routes

The following direct-coverage gaps are intentionally not exact dictionary
leaves:

| Route | Text | Reason |
| --- | --- | --- |
| `spice.commonPhrases.sultanClone[2]` | `*var* twin` | Generated name template; expanded twin/clone output is owned by journal route grammar and capture reconstruction. |
| `spice.instancesOf.ascensionReasons_VAR[0]` | `of *var* *var2*` | Generated possessive plus reason phrase; Japanese output needs reordered captures. |
| `spice.instancesOf.furnitureStuckPreposition[1..3]` | `under`, `inside`, `behind` | Grammar-sensitive position fragments handled by journal route patterns for expanded furniture-death output. |
| `spice.instancesOf.inYear[1..3]` | `early in`, `late in`, `sometime in` | Year connectors handled by `JournalPatternTranslator.TryTranslateAnnalsInYearCapture`. |
| `spice.instancesOf.murdered[6]` | `drowned in a lake of *liquid*` | Generated murder template with liquid capture; expanded liquid variants are journal-pattern-owned. |
| `spice.cooking.ate[0]` | `You eat the meal.` | Fixed popup frame already covered by the popup pattern and Campfire owner-route tests. |
| `spice.cooking.cookTemplate[*]` | `*ingredients*` meal frames | Cooking owner route, not dictionary leaf. |
| `spice.cooking.pinchOf[*]` | quantity fragments | Ingredient-fragment grammar; direct leaves would break Japanese ordering. |
| `spice.cooking.cookbooks.*` | `$markovTitle`, `$focus: $markovTitle` | Control placeholders; generated display names are owned by `CookbookDisplayNameTranslationPatch`, not exact dictionary leaves. |
| `spice.cooking.recipeNames.ingredients.*` | `$JerkyDisplayName`, `$LimbDisplayName` | Display-name placeholders, not visible leaves. |
| `spice.cooking.recipeNames.preposition[*]` | `with`, `inside of`, `on top of`, `over` | Recipe-name grammar connectors; owner route must own Japanese ordering. |
| `spice.gossip.twoFaction[*]` | `*f1*`, `*f2*`, `@item.*` frames | Generated journal observation templates. |
| `spice.proverbs[*]` | `*SacredThing*`, `*Activity*`, etc. frames | Generated proverb templates with captured values. |

Do not close these rows by adding exact English template keys to
`Scoped/historyspice-common.ja.json`. Close them through owner routes, pattern
translation, or runtime evidence that proves an existing owner already handles
the expanded output.

## Verification

- `uv run python -m pytest scripts/tests/test_historyspice_vocabulary_coverage.py -q`: passed, 83 tests.
- Focused C# cookbook owner-route tests for `CookbookDisplayNameTranslatorTests`
  and `CookbookDisplayNameTranslationPatchTests`: passed, 20 tests.
- Focused C# journal route tests for `JournalPatternTranslatorTests` and
  `JournalEntryDisplayTextPatchTests`: passed, 69 tests. These cover expanded
  `sultanClone`, `inYear`, furniture-stuck, and liquid-drowning HistorySpice
  output.
- HistorySpice coverage command above: HSE direct coverage is now `3119 / 3897`.
