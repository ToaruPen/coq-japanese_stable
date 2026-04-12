# 2026-04-12 static untranslated quality review

## Scope

This review uses only the fresh `#362` outputs plus the cited routing/validation references already named in the issue-364 ledger. No code, plan, or runtime-route ownership changes were made.

Primary inputs:

- `docs/reports/2026-04-12-issue-362-static-baseline-refresh.md`
- `docs/reports/2026-04-12-issue-362-fixed-leaf-pruning-batch-02.md`
- `docs/reports/2026-04-12-issue-364-execution-ledger.md:73-102`
- reference set called out by the ledger: `docs/RULES.md`, `scripts/legacies/scanner/inventory.py`, `scripts/legacies/scanner/rule_classifier.py`

## What the fresh #362 outputs show

The refreshed baseline is still dominated by conservative routing, not by a shortage of scanner output:

- `translated`: 2356
- `unresolved`: 1199
- `needs_review`: 476
- `needs_patch`: 338
- `excluded`: 238
- `needs_translation`: 27

The fixed-leaf validation pass still reports only duplicate-key failures: 77 issues across 856 candidates. The follow-up pruning batch then rejected the entire 27-row `needs_translation` pseudo-leaf queue and promoted nothing.

## Bucket analysis

### 1) Pseudo-leaf noise to exclude earlier

This bucket is real and fully evidenced.

The 27 `needs_translation` rows are mostly placeholders, spacing sentinels, or widget/channel identifiers rather than player-facing leaf text. The batch report names the concrete examples:

- empty-string rows such as `AchievementViewRow`, `CharacterStatusScreen`, `CyberneticsTerminalRow`, `EquipmentLine`, `InventoryLine`, `PopupMessage`, `TradeLine`, `WorldGenerationScreen`, and `SteamScoresRow`
- a single-space row in `WorldGenerationScreen`
- `BodyText` widget/channel rows in `OptionsRow`, `AttributeSelectionControl`, and `Look`
- `SelectedModLabel` as a widget identifier paired with dynamic payload text

These should stay out of promotion work earlier in the pipeline. The evidence supports pruning them before fixed-leaf validation, not after.

### 2) Overly conservative routing

The fresh outputs do not justify broadening fixed-leaf eligibility, but they do show one narrow reclassification opportunity:

- `Choose a reward`
- `Are you sure you want to delete this entry?`
- `Would you like to save your changes?`

Per the pruning batch, these already have narrow homes (`ui-popup.ja.json` / `ui-options.ja.json`) and should be treated as deduped existing coverage, not as new fixed-leaf promotions.

No other safe promotion subfamily is evidenced by the refreshed queue. The exact-leaf popup keys reviewed in the batch (`That code is invalid.`, `Your new pet is ready to love.`, `You have no activated abilities.`) remain deferred because the current validator does not collapse duplicate families by existing coverage.

### 3) Correctly excluded routes

The remaining backlog is correctly outside fixed-leaf work under the current boundary:

- unresolved / sink-heavy inventory remains the dominant class
- producer-owned and mid-pipeline-owned families remain separate from fixed-leaf promotion
- the issue-364 ledger explicitly keeps `message-frame`, builder/display-name, procedural, unresolved, and `needs_runtime` routes outside the fixed-leaf task

So the review does **not** support a wider fixed-leaf promotion rule. It supports earlier pseudo-leaf pruning and more explicit deduped-existing-coverage classification.

## Recalibration target for task 7

The evidence-backed target is narrow:

1. exclude the 27 pseudo-leaf shapes before fixed-leaf validation,
2. reclassify already-covered duplicate families as deduped existing coverage, and
3. keep the conservative boundary for all runtime-owned or unresolved routes.

## Verdict

The static untranslated queue is still small/noisy because it mixes true candidates with pseudo-leaf scaffolding and duplicate-family residue. The correct response is not a broader promotion rule; it is earlier noise pruning plus explicit duplicate-family reclassification.
