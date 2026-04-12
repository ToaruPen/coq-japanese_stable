# 2026-04-12 issue #362 fixed-leaf pruning batch 02

## Scope

Task 3 used the fresh static baseline artifacts from Task 2 as review input only:

- `docs/reports/2026-04-12-issue-362-static-baseline-refresh.md`
- `.sisyphus/evidence/task-2-baseline-run/scanner.log`
- `docs/candidate-inventory.json`

The goal for this batch was intentionally narrow:

1. prune the 27-row pseudo-leaf queue before any import attempt,
2. read representative `duplicate_key` families against current validator semantics, and
3. promote only candidates that remain safe after both checks.

## Batch outcome

- **Promoted:** none
- **Deferred:** representative duplicate exact-leaf families only
- **Rejected from promotion:** the entire 27-row `needs_translation` pseudo-leaf queue

The promotion set is empty in this batch. After pruning the current queue, no survivor remains that can be promoted without bypassing the current duplicate-key rule.

## Why no promotion happened

`fixed_leaf_validation.py` validates every site whose `destination_dictionary` is non-null; in the current scanner semantics, that means sites whose `status` is neither `translated` nor `excluded`. Duplicate detection still runs across the whole candidate set, regardless of `existing_dictionary` or `existing_patch` coverage.

- `scripts/legacies/scanner/fixed_leaf_validation.py:61-74`
- `scripts/legacies/scanner/fixed_leaf_validation.py:135-156`

That behavior is already visible in the fresh baseline: `Choose a reward`, `Are you sure you want to delete this entry?`, and `Would you like to save your changes?` still appear as `duplicate_key` failures even though the current inventory already marks them with `existing_dictionary` coverage.

## Rejected from promotion: 27 pseudo-leaf rows

These rows still satisfy the scanner's default fixed-leaf gate, but they fail the human review checklist because they are placeholders, spacing sentinels, or widget/channel identifiers rather than player-facing stable leaves.

### `""` placeholder rows — reject

Reason: empty-string reset/clear operations are not player-visible text, and importing `""` would be a broad empty-key addition.

- `Qud.UI/AchievementViewRow.cs:79`
- `Qud.UI/AchievementViewRow.cs:87`
- `Qud.UI/AchievementViewRow.cs:99`
- `Qud.UI/CharacterStatusScreen.cs:295`
- `Qud.UI/CyberneticsTerminalRow.cs:65`
- `Qud.UI/EquipmentLine.cs:377`
- `Qud.UI/EquipmentLine.cs:383`
- `Qud.UI/InventoryLine.cs:302`
- `Qud.UI/InventoryLine.cs:378`
- `Qud.UI/InventoryLine.cs:384`
- `Qud.UI/MissileWeaponAreaInfo.cs:67`
- `Qud.UI/PopupMessage.cs:610`
- `Qud.UI/SkillsAndPowersStatusScreen.cs:154`
- `Qud.UI/TradeLine.cs:453`
- `Qud.UI/TradeLine.cs:470`
- `Qud.UI/WorldGenerationScreen.cs:229`
- `Qud.UI/WorldGenerationScreen.cs:230`
- `Qud.UI/WorldGenerationScreen.cs:245`
- `Qud.UI/WorldGenerationScreen.cs:246`
- `Qud.UI/WorldGenerationScreen.cs:252`
- `SteamScoresRow.cs:34`

### `" "` spacing row — reject

Reason: single-space layout padding is a UI spacing sentinel, not a translatable exact leaf.

- `Qud.UI/WorldGenerationScreen.cs:223`

### `BodyText` widget/channel rows — reject

Reason: `BodyText` is the target widget/style channel for dynamic RTF payloads, not player-facing leaf text.

- `Qud.UI/OptionsRow.cs:60`
- `XRL.CharacterBuilds.Qud.UI/AttributeSelectionControl.cs:69`
- `XRL.UI/Look.cs:293`
- `XRL.UI/Look.cs:316`

### `SelectedModLabel` widget identifier row — reject

Reason: `SelectedModLabel` is a transform/widget identifier paired with the dynamic payload `"Managing - " + info.ID`, so it is not a stable leaf candidate.

- `SteamWorkshopUploaderView.cs:121`

## Deferred duplicate families reviewed in this batch

### Already covered by the narrowest safe dictionary home — defer, no new import

These families are already mapped to the correct scoped home, but the current validator still reports them because duplicate detection runs across candidate sites rather than across missing dictionary entries.

| Key | Current home | Evidence |
| --- | --- | --- |
| `Choose a reward` | `ui-popup.ja.json` | `docs/candidate-inventory.json:36684-36702`, `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json:813-816` |
| `Are you sure you want to delete this entry?` | `ui-popup.ja.json` | `docs/candidate-inventory.json:38523-38601`, `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json:973-976` |
| `Would you like to save your changes?` | `ui-options.ja.json` | `docs/candidate-inventory.json:38771-38865`, `Mods/QudJP/Localization/Dictionaries/ui-options.ja.json:117-120` |

Decision: keep these as **deduped existing coverage**, not as new Task 3 promotions.

### Exact-leaf Popup families still blocked by duplicate semantics — defer

These keys are stable Popup exact leaves and their narrowest safe home is still `scoped`/`ui-popup.ja.json`, but adding them in this batch would not make validator output pass for the handled rows because duplicate detection does not collapse by existing coverage.

| Key | Candidate evidence | Decision |
| --- | --- | --- |
| `That code is invalid.` | `docs/candidate-inventory.json:36260-36372` | Defer until a later task can truly merge/reject the duplicate family at the candidate-set level. |
| `Your new pet is ready to love.` | `docs/candidate-inventory.json:36316-36408` | Defer for the same reason. |
| `You have no activated abilities.` | `docs/candidate-inventory.json:36815-36851` | Defer for the same reason. |

## Safe-survivor verdict

After pruning the pseudo-leaf queue and reviewing the representative duplicate families surfaced by the fresh validator log, the **first safe fixed-leaf survivor set is empty**.

This keeps the batch aligned with the existing prune-first guidance:

- placeholder/UI-identifier rows are explicitly excluded from promotion,
- existing covered duplicates are recorded as already deduped to their narrow home, and
- uncovered duplicate families are deferred instead of being force-imported into a validator-failing batch.

## Verification

Evidence files produced in this task:

- `.sisyphus/evidence/task-3-fixed-leaf-validator.txt`
- `.sisyphus/evidence/task-3-l1-tests.txt`

Observed results:

- `python3 scripts/legacies/scan_text_producers.py --source-root ~/dev/coq-decompiled_stable --cache-dir .scanner-cache --output docs/candidate-inventory.json --phase 1d --validate-fixed-leaf`
  - still fails with **77 `duplicate_key` issues across 856 candidates**
  - failure classes remain duplicate-only; the handled pseudo-leaf rows are not promoted in this batch
- `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1`
  - **Passed: 1092, Failed: 0, Skipped: 0**

This keeps the batch within scope: the pseudo-leaf rows are explicitly excluded from promotion, and the remaining validator blockers are documented as duplicate-family work outside this batch rather than being silently force-imported.
