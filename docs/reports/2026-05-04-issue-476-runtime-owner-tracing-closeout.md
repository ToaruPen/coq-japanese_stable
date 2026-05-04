# Issue #476 runtime owner tracing closeout

## Evidence

Runtime log inspected:

- `$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- mtime: `2026-05-04 00:55:36 JST`

Command:

```bash
python3.12 scripts/triage_untranslated.py \
  --log "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log" \
  --output .codex-artifacts/issue-476/triage-current.json
```

## Current actionable summary

The issue was opened from an older triage state with `total=111`,
`unresolved=103`, and `<no-context>` carrying `36 unresolved` entries plus
other mixed outcomes.

Current triage of the latest local `Player.log` reports:

| classification | count |
| --- | ---: |
| total | 51 |
| static_leaf | 0 |
| route_patch | 0 |
| logic_required | 0 |
| preserved_english | 0 |
| unexpected_translation_of_preserved_token | 0 |
| runtime_noise | 41 |
| unresolved | 10 |

`<no-context>` no longer contains actionable unresolved entries. The only
remaining `<no-context>` rows in the report's actionable section are `20`
`runtime_noise` entries, which are retained there for auditability but have
non-actionable dispositions:

| class | count | disposition |
| --- | ---: | --- |
| already-localized Japanese re-entry | 17 | non-actionable runtime re-entry |
| whitespace-only formatting | 2 | non-actionable formatting noise |
| known flat Translator re-entry token `items` | 1 | non-dictionary action; owner tracing follow-up if it recurs with user-visible English |

## Remaining unresolved routes

The remaining `10` actionable unresolved rows are not `<no-context>`; they
carry owner route context:

| route | unresolved |
| --- | ---: |
| `DescriptionLongDescriptionPatch` | 1 |
| `DescriptionShortDescriptionPatch` | 4 |
| `TradeLineTranslationPatch` | 1 |
| `TradeUiPopupTranslationPatch` | 4 |

Those are owner-routed follow-up/fix targets, not undifferentiated
`<no-context>` tracing failures. The current runtime-triage batch PR handles
the description and popup/message examples covered by #470 and #472. The
trade-line `{{w|bronze}}` row remains a separate owner-routed trade/display
name question.

## Phase F evidence

Phase F observations are now kept separate from actionable untranslated triage:

| Phase F bucket | count |
| --- | ---: |
| total | 1788 |
| dynamic_text_probe | 542 |
| sink_observe | 274 |
| final_output_probe | 972 |
| markup_semantic_drift | 111 |

`markup_semantic_drift` is therefore reportable as its own Phase F bucket. The
sample drift set contains:

- `JournalPatternTranslator` unmatched Qud close on a journal location notice,
  covered by the #472 journal markup-drift fix.
- `UITextSkinTranslationPatch` loading/progress-map strings with
  `empty_qud_wrapper` and `unclosed_qud_scope`, which are final-output evidence
  rather than dictionary work.
- popup button nested-wrapper rows such as `{{W|{{W|[y]}} {{y|はい}}}}`,
  which remain Restore/ownership follow-up evidence for #459.

## Closeout

Issue #476's original blocker was that `<no-context>` mixed true untranslated text,
already-localized Japanese fragments, preserved tokens, display-name artifacts,
and flat Translator re-entry noise into one manual bucket. Current triage no
longer collapses those cases:

- preserved/Japanese/blank/re-entry rows are non-actionable classifications,
- Phase F final output and semantic drift are separated from actionable triage,
- remaining actionable rows have owner routes.

Fresher runtime smoke can still improve release evidence, but this issue's
owner-tracing and triage-classification gate is satisfied by the current report.
