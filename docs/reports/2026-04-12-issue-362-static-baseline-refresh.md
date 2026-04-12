# 2026-04-12 issue #362 static baseline refresh

## Scope

Fresh rerun of the legacy static scanner from `~/dev/coq-decompiled_stable` using the documented happy-path command.

Evidence log: `.sisyphus/evidence/task-2-baseline-run/scanner.log`

```bash
python3 scripts/legacies/scan_text_producers.py \
  --source-root ~/dev/coq-decompiled_stable \
  --cache-dir .scanner-cache \
  --output docs/candidate-inventory.json \
  --phase all \
  --validate-fixed-leaf
```

## Scanner output

- Phase 1a: 5367 unique files, 3778 sink hits, 874 override hits
- Phase 1b: 4634 total sites; proven fixed-leaf 856; rejected fixed-leaf 3778; translated 0
- Phase 1d: 4634 total sites; proven fixed-leaf 856; rejected fixed-leaf 3778; translated 2356
- Fixed-leaf validation: 77 issues across 856 candidates

## Fresh inventory totals

- `translated`: 2356
- `unresolved`: 1199
- `needs_review`: 476
- `needs_patch`: 338
- `excluded`: 238
- `needs_translation`: 27

Type totals:

- `Unresolved`: 2446
- `Leaf`: 856
- `MessageFrame`: 339
- `VerbComposition`: 289
- `ProceduralText`: 242
- `Builder`: 164
- `NarrativeTemplate`: 130
- `Template`: 129
- `VariableTemplate`: 39

Ownership totals:

- `sink`: 2446
- `producer-owned`: 1332
- `mid-pipeline-owned`: 856

Destination dictionary totals:

- `scoped`: 789
- `global_flat`: 67

## Validation findings

- All 77 fixed-leaf validation errors were `duplicate_key`
- The validation noise is still dominated by repeated exact keys, not by a small set of clean import-ready survivors

## Queue reading

The current fixed-leaf residue is still prune-first and noise-heavy. The 27 `Leaf` rows needing translation are mostly placeholders or UI/channel identifiers such as `""`, `" "`, `BodyText`, and `SelectedModLabel`; there are no clear promotion candidates yet.

## Bridge/view-only status

`docs/candidate-inventory.json` was refreshed by this run, but it remains a bridge/view-only artifact for current static consumers, not the source of truth.
