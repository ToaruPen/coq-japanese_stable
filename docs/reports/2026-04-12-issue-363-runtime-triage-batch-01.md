# 2026-04-12 issue-363 runtime triage batch 01

## Scope

Fresh Rosetta-backed triage from the current `Player.log` using:

```bash
python3 scripts/triage_untranslated.py --log ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log --output .sisyphus/evidence/task-4-runtime-triage.json
```

Evidence files:

- `.sisyphus/evidence/task-4-runtime-triage.json`
- `.sisyphus/evidence/task-4-runtime-triage.stderr`
- `.sisyphus/evidence/task-4-runtime-triage-error.txt`

## Fresh triage summary

- actionable queue: 90 entries
- `static_leaf`: 1
- `logic_required`: 3
- `unresolved`: 86
- Phase F queue: 547 entries
  - `DynamicTextProbe`: 227
  - `SinkObserve`: 320

## Route buckets in the actionable queue

| Route | Count | Ownership note | Action |
| --- | ---: | --- | --- |
| `<no-context>` | 48 | intentionally unresolved (44 unresolved, 3 logic_required, 1 static_leaf) | hold |
| `DescriptionLongDescriptionPatch` | 16 | mid-pipeline-owned | **first actionable batch** |
| `DescriptionShortDescriptionPatch` | 12 | mid-pipeline-owned | **first actionable batch** |
| `GetDisplayNamePatch` | 4 | intentionally unresolved builder/display-name seam | hold |
| `GetDisplayNameProcessPatch` | 4 | intentionally unresolved builder/display-name seam | hold |
| `PopupShowTranslationPatch` | 3 | sink-observed | observation-only |
| `TradeUiPopupTranslationPatch` | 3 | sink-observed | observation-only |

## First actionable runtime batch

The first actionable runtime batch is the **description family pair**:

- `DescriptionLongDescriptionPatch`
- `DescriptionShortDescriptionPatch`

Why this is first:

1. It is the earliest concrete owner-routed batch in the fresh log.
2. Prior batch notes already treat description families as owner-routed, route-family-first work rather than fixed-leaf imports.
3. The `Popup*` routes remain sink-observed, so they stay out of the implementation queue.
4. The `<no-context>` bucket is larger, but it is intentionally unresolved and does not give a safe owner route yet.

## Phase F separation

`DynamicTextProbe` and `SinkObserve` stay in the separate Phase F section only. They are runtime evidence records, not actionable untranslated entries, so they must not be merged into the implementation queue.

## Readout for task 5

Task 5 should start from the description family pair above. The remaining buckets are:

- `GetDisplayName*`: route still unresolved; defer until owner proof is tighter
- `Popup*`: sink-observed; observation-only
- `<no-context>`: intentionally unresolved; not a first-batch target
