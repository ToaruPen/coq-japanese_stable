# Issue #471 display-name ownership closeout

## Evidence

Runtime log inspected:

- `$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- mtime: `2026-05-04 00:55:36 JST`

Command:

```bash
python3.12 scripts/triage_untranslated.py \
  --log "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log" \
  --output .codex-artifacts/issue-471/triage-current.json
```

## Current actionable summary

Current triage reports:

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

The display-name routes are no longer actionable unresolved buckets:

| route | unresolved | runtime_noise |
| --- | ---: | ---: |
| `GetDisplayNamePatch` | 0 | 3 |
| `GetDisplayNameProcessPatch` | 0 | 3 |

The remaining `GetDisplayName*` actionable rows are already-localized mixed
runtime re-entry noise, such as translated Japanese bases with procedural title
tails. They are not sink-owned untranslated work.

## Phase F owner evidence

Phase F still records display-name observations, but they carry owner families
and transformed output rather than undifferentiated sink ownership:

| route | representative family | current disposition |
| --- | --- | --- |
| `GetDisplayNameProcessPatch` | `DisplayName.MarkupLeadingModifier` | owner-routed transform evidence |
| `GetDisplayNameProcessPatch` | `DisplayName.MixedModifier` | owner-routed transform evidence |
| `GetDisplayNameProcessPatch` | `DisplayName.BracketedSuffix` | owner-routed transform evidence |
| `GetDisplayNameProcessPatch` | `DisplayName.QuantifiedLiquidState` | owner-routed transform evidence |

Representative transformed examples include:

| source | transformed |
| --- | --- |
| `{{spiked|spiked}} {{W|オーガ毛皮}}の手袋` | `{{spiked|トゲ付き}} {{W|オーガ毛皮}}の手袋` |
| `slender フラーレンプレートメイル` | `細身なフラーレンプレートメイル` |
| `水袋 [empty]` | `水袋 [空]` |
| `塩挽き器 [1 dram of salt]` | `塩挽き器 [1ドラムの塩]` |

`SinkPrereqSetDataTranslationPatch` still observes final UI text, but the
display-name examples that previously made this issue ambiguous now have
upstream `GetDisplayName*` owner evidence. The sink remains an observer and is
not promoted to a generic display-name owner.

## Closeout

Issue #471's acceptance criteria are satisfied at the ownership/classification
level:

- state-composed display names have explicit `GetDisplayName*` owner families,
- common bracketed and liquid/container states are translated by route-local
  display-name logic,
- current actionable triage has zero `GetDisplayName*` unresolved rows,
- sink observations remain observation-only and no sink-wide fallback was added.

Further display-name polish can continue as narrower quality issues for
specific procedural title fragments, but #471 no longer blocks the #468 owner
route roll-up.
