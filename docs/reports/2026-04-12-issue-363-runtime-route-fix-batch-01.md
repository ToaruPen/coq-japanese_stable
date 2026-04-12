# 2026-04-12 issue-363 runtime route-fix batch 01

## Scope

This batch implements the first actionable runtime family selected in `docs/reports/2026-04-12-issue-363-runtime-triage-batch-01.md:39-63`.

- owner seams only: `DescriptionLongDescriptionPatch` / `DescriptionShortDescriptionPatch`
- no sink-side compensation
- no `.sisyphus/plans/issue-364-roadmap.md` changes
- no `Popup*`, `GetDisplayName*`, or `<no-context>` expansion

Verification evidence:

- `.sisyphus/evidence/task-5-dotnet-build.txt`
- `.sisyphus/evidence/task-5-l1.txt`
- `.sisyphus/evidence/task-5-l2.txt`
- `.sisyphus/evidence/task-5-triage-pytest.txt`

## Implemented owner-route fix

The narrow fix stays entirely inside `Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs` under the existing description owner seams.

What changed:

1. mixed-language disposition targets such as `Loved by the ジョッパの村人たち.` now normalize at the description route instead of leaking the English article into visible output
2. `the villagers of {JP-name}` now reuses the existing `The villagers of {0}` owner-routed template instead of requiring sink-side compensation
3. already-localized Japanese faction/group names behind a leading English article now drop the orphaned article before the final description sentence is composed

What did **not** change:

- no sink patches were promoted to owners
- no broad dictionary/import workaround was added
- no active-effect owner seams were touched

## Regression coverage added

Added route-specific L2 regressions for the exact runtime family shape observed in Task 4:

- `DescriptionLongDescriptionPatchTests.Postfix_TranslatesMixedJapaneseDescriptionBlock_FromRuntimeShape_WhenPatched`
- `DescriptionShortDescriptionPatchTests.DescriptionShortDescriptionPatch_TranslatesMixedJapaneseDescriptionBlock_FromRuntimeShape_WhenPatched`

These tests prove:

- the owner route remains `DescriptionLongDescriptionPatch` / `DescriptionShortDescriptionPatch`
- mixed Japanese/English description blocks now render translated visible text on the owner seam
- separator lines and already-localized description segments survive unchanged
- sink observation stays at zero for the translated runtime block

The broader markup/wrapper preservation coverage already present in `DescriptionTextTranslatorTests` remained green in the full L1 run recorded at `.sisyphus/evidence/task-5-l1.txt`.

## Deferred after this batch

Still deferred by design:

- `PopupShowTranslationPatch` / `TradeUiPopupTranslationPatch`: sink-observed only
- `GetDisplayNamePatch` / `GetDisplayNameProcessPatch`: route still unresolved
- `<no-context>` actionable bucket: intentionally unresolved until route proof improves

Within the description family itself, this batch fixed the mixed-language disposition-target shape only. It did not broaden into new asset imports or unrelated route families.
