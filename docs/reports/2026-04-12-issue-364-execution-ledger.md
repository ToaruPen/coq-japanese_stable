# 2026-04-12 issue-364 execution ledger

## Cross-cutting rules

- Evidence order is fixed: tests → layer boundaries → fresh runtime logs → decompiled source → older notes.
- Static work stays separate from Phase F work: `#362`, `#355`, and `#347` are static/fixed-leaf tracks; `#363` is the runtime Phase F track.
- `#347` is post-batch consolidation only. It depends on real outputs from earlier batches and is **not** a prerequisite for `#362`.
- Do not reorder the child issues: `#362 -> #363 -> #355 -> #354 -> #347`.

## 1. #362 — first fixed-leaf promotion batch

**Scope summary**

Refresh the static candidate inventory, prune obvious pseudo-leaf noise, and promote only the first safe fixed-leaf batch.

**Entry criteria**

- The issue-364 execution contract is frozen.
- Current decompiled inputs are available for a fresh scanner pass.
- Proven fixed-leaf policy and exclusion rules from `docs/RULES.md` are in force.

**Required commands**

```bash
python3 scripts/legacies/scan_text_producers.py --source-root ~/dev/coq-decompiled_stable --cache-dir .scanner-cache --output docs/candidate-inventory.json --phase all --validate-fixed-leaf
```

**Durable outputs**

- `docs/candidate-inventory.json`
- A durable `docs/reports/` batch report for the `#362` run
- Promotion / defer / reject records with reasons

**Stop conditions**

- No safe fixed-leaf promotions remain.
- The queue is still noise-heavy and needs later pruning.
- Validation exposes a rule gap that must be fixed before promotion continues.

## 2. #363 — first runtime untranslated triage batch

**Scope summary**

Capture fresh Rosetta-backed runtime evidence, split actionable untranslated routes from Phase F-only observations, and land the first actionable runtime route-fix batch.

**Entry criteria**

- A fresh Rosetta `Player.log` exists.
- `#362` has already produced its static batch evidence.
- Runtime evidence is treated as proof, not as a replacement for static coverage.

**Required commands**

```bash
python3 scripts/triage_untranslated.py --log ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log --output .sisyphus/evidence/task-4-runtime-triage.json
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
pytest scripts/tests/test_triage_log_parser.py scripts/tests/test_triage_models.py scripts/tests/test_triage_classifier.py scripts/tests/test_triage_integration.py -q
```

**Durable outputs**

- `.sisyphus/evidence/task-4-runtime-triage.json`
- `.sisyphus/evidence/task-4-runtime-triage.stderr`
- `.sisyphus/evidence/task-4-runtime-triage-error.txt`
- A durable `docs/reports/` triage report for `#363`
- Route-ownership notes for the first actionable batch

**Stop conditions**

- Evidence is native ARM64 only or otherwise not Rosetta-backed.
- The report can only produce Phase F-only observations.
- No actionable route owner can be identified without changing the ownership rules.

## 3. #355 — static untranslated detection quality review

**Scope summary**

Use the fresh `#362` outputs to explain scanner noise, over-conservatism, and valid exclusions, then recalibrate the static detection path only as far as the evidence allows.

**Entry criteria**

- Fresh `#362` inventory and promotion data exist.
- The analysis can point to concrete queue examples, not guesses.
- The work remains on the static scanner side, not on runtime route proof.

**Required commands**

```bash
pytest scripts/tests/ -k "scan_text_producers or scanner_rule_classifier or scanner_cross_reference or fixed_leaf" -v
ruff check scripts/
```

**Durable outputs**

- `docs/reports/2026-04-12-static-untranslated-quality-review.md`
- Deterministic pytest fixture updates, if required
- Scanner recalibration notes that preserve the conservative boundary

**Stop conditions**

- The evidence does not justify a narrower promotion rule.
- A proposed change would broaden fixed-leaf eligibility indiscriminately.
- The recalibration cannot stay deterministic under fixture tests.

## 4. #354 — owner seam and dictionary audit

**Scope summary**

Audit the remaining buckets after `#362` and `#363`, then classify representative families as existing seam, existing seam with asset gap, or true route gap.

**Entry criteria**

- Post-`#362` and post-`#363` evidence is available.
- The audit stays route-first and does not assume sink visibility is ownership proof.

**Required commands**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
pytest scripts/tests/ -k "triage or scanner or validate_pattern_routes" -q
```

**Durable outputs**

- `docs/reports/2026-04-12-owner-seam-audit.md`
- Reclassification / bookkeeping updates for stale buckets
- Any narrow asset-gap fixes that belong to an existing seam

**Stop conditions**

- A family is only stale noise and does not justify new patch work.
- A true route gap needs separate route ownership work.
- The audit starts drifting into bulk asset import or sink-side compensation.

## 5. #347 — post-batch fixed-leaf consolidation

**Scope summary**

Consolidate the proven fixed-leaf workflow after the earlier batches have produced real evidence, and align policy docs, scanner validation, repo guardrails, and contributor workflow docs to that observed process.

**Entry criteria**

- `#362`, `#363`, `#355`, and `#354` have all produced durable evidence.
- This task remains downstream consolidation, not an upstream prerequisite.
- The final wording must reflect what the first batches actually proved.

**Required commands**

```bash
bash -lc "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1 && pytest scripts/tests/ -k 'scanner or fixed_leaf or validate_pattern_routes or scan_text_producers' && ruff check scripts/"
python3 - <<'PY'
from pathlib import Path
text = Path('docs/fixed-leaf-workflow.md').read_text(encoding='utf-8')
for needle in ['source route', 'ownership class', 'destination dictionary', 'rejection reason', 'Translator']:
    print(needle, needle in text)
PY
```

**Durable outputs**

- `docs/RULES.md`
- `docs/fixed-leaf-workflow.md`
- `Mods/QudJP/Localization/AGENTS.md`
- `Mods/QudJP/Localization/Dictionaries/README.md`
- Updated tests and scanner/docs surfaces that preserve the current Translator model

**Stop conditions**

- Any attempt to redesign `Translator` runtime semantics.
- Any attempt to add route-aware runtime registration.
- Workflow prose no longer matches the evidence from the earlier batches.
