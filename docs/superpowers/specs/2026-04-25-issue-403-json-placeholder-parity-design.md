# Issue #403 — JSON dictionary `{N}` placeholder parity

## Why

Four entries across `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json` and `ui-trade.ja.json` carry `String.Format`-style `{N}` numeric placeholders in the English key but drop one of them in the Japanese `text`. At runtime the renderer calls `String.Format` with the full positional argument list; a missing slot in the format string silently loses player-facing information (poison name, debtor pronoun) while a stray slot would throw. The current shipped translations therefore render with semantic content missing.

`docs/RULES.md` requires that markup and placeholders be preserved exactly. This spec restores that invariant for `{N}` and locks it behind a dict-wide pytest contract so any future regression is caught at validation time, not at runtime.

## What

### Data fixes

Four entries change. Source key stays as-is; only the `text` field is updated.

| File | Line | Source key (excerpt) | Current Japanese | New Japanese |
| --- | ---: | --- | --- | --- |
| `ui-popup.ja.json` | 2582 | `You cure the {0} coursing through {1} with a balm made from {2}.` | `{2}で作った塗り薬で{1}を蝕む毒を治した。` | `{2}で作った塗り薬で{1}を蝕む{0}を治した。` |
| `ui-trade.ja.json` | 57 | `{0} will not trade with you until you pay {1} the {2} you owe {3}.` | `{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。` | `{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。` |
| `ui-trade.ja.json` | 62 | `... pay {1} the {2} you owe {3}. Do you want to give {4} your {5} now?` | `{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐあなたの{5}を{4}に渡しますか？` | `{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。今すぐあなたの{5}を{4}に渡しますか？` |
| `ui-trade.ja.json` | 67 | `... pay {1} the {2} you owe {3}. Do you want to give it to {4} now?` | `{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐそれを{4}に渡しますか？` | `{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。今すぐそれを{4}に渡しますか？` |

Independent grep (`scripts/tests/.../check`) confirms these are the only four `{N}` mismatches across `Mods/QudJP/Localization/Dictionaries/` and `Dictionaries/Scoped/`. There is no fifth case.

### Translation rationale

- **Poison cure (`ui-popup.ja.json:2582`)**: `{0}` is the poison display name (per `Campfire.cs`). Inserting `{0}` in place of the generic word `毒` restores the specificity ("cured the venom"/"cured the snakeoil") instead of an abstract "poison".
- **Trade-debt lines (`ui-trade.ja.json:57, 62, 67`)**: `TradeUI.cs:387,393,402` builds these via string concatenation. Both `{1}` and `{3}` are the same value `_Trader.them` (a pronoun); they appear twice because the English sentence references the trader pronoun in two grammatical positions (as the recipient of payment, and as the entity you owe). The new Japanese cleanly maps `{3}` to the creditor role (`{3}に借りている`) and `{1}` to the payee role (`{1}に支払う`), preserving English order. The two slots resolve to the same string at runtime, so no visible text changes — only the `String.Format` argument multiset becomes consistent.

### New test contract

Add `scripts/tests/test_json_placeholder_parity.py`. It scans every `*.ja.json` under `Mods/QudJP/Localization/Dictionaries/` (recursively, so the `Scoped/` subdirectory is included) and asserts, for each entry whose `key` contains a `{N}` numeric placeholder, that the multiset of placeholder indices in `key` matches the multiset in `text`.

The placeholder regex must be index-aware to handle format-spec variants like `{0:+#;-#}` (used in `world-mods.ja.json` for stat modifiers): `\{([0-9]+)(?::[^{}]*)?\}`.

No allowlist is needed; only the four entries above currently mismatch, and they will be green after the data fix.

The test goes red on today's data (4 cases) and green after the fixes. It runs as part of the standard pytest suite.

## How

1. Write the failing pytest first.
2. Run it; confirm exactly 4 mismatches surface, identical to the table above.
3. Apply the four `text` field edits.
4. Re-run the pytest; expect green.
5. Run the full repo verification suite (`validate_xml`, `check_encoding`, `ruff`, full `pytest`, `dotnet build`).

Step-by-step task layout lives in the implementation plan.

## Verification

```bash
uv run pytest scripts/tests/test_json_placeholder_parity.py -v
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
uv run pytest scripts/tests/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

All must pass.

## Out of scope

- Color/markup tokens (`{{Y|...}}`, `&X`, `^X`) — that is #401's territory.
- `=variable=` runtime slots — that is #402's territory.
- CI gate plumbing for the new test (#409 lands the workflow yaml later).
- Any other dictionary entries flagged by the test going forward — they should be filed as follow-ups, not bundled here.
- Translation tone changes unrelated to placeholder parity in the four entries.

## Risks

- **Trader pronoun grammar.** `_Trader.them` is the trader's third-person pronoun, which Caves of Qud determines per-creature. The translation reuses the same `{1}`/`{3}` pronoun in both grammatical roles; if the runtime renders mid-clitic Japanese particles incorrectly because `_Trader.them` lacks a Japanese form, the symptom will already exist in the current text on the unaffected portions. We are not changing pronoun rendering, only restoring the missing slot.
- **Future placeholder regressions.** The dict-wide test catches them mechanically, but only for `{N}` numeric format slots. Other markup classes are still uncovered until #401, #402, #409 land.
- **Test runtime.** Scanning all dictionaries on every test run reads ~64 JSON files; each is small and the parse is fast, so this is well below 1 second total. No fixture optimization needed.
