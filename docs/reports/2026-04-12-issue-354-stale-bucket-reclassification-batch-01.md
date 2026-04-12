# 2026-04-12 issue-354 stale bucket reclassification batch 01

## Scope

Task 9 applies the smallest bookkeeping changes justified by the Task 8 audit for issue #354.

- no new owner route
- no sink-side fallback
- no broad asset import
- no Python production-logic change

The goal here is to make already-covered families stop reading as fresh `needs_patch` route work.

## What changed

### 1. Reclassified the audited `HolographicBleeding` message-frame family out of fresh route work

The current repo already had all ten audited `DidX` tails on the existing message-frame seam:

- review note: `docs/reports/2026-04-11-didx-holographicbleeding-review.md`
- audit verdict: `docs/reports/2026-04-12-owner-seam-audit.md`
- asset home: `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`
- bookkeeping logic: `scripts/legacies/reconcile_inventory_status.py`

The stale part was the bridge artifact: those ten `docs/candidate-inventory.json` rows still said `status = needs_patch` even though the current `verbs.ja.json` coverage already matched them.

This batch updates only those audited `XRL.World.Effects/HolographicBleeding.cs` rows to:

- `status = translated`
- `existing_dictionary = MessageFrames/verbs.ja.json`

### 2. Narrowed `Prone` from "existing-seam asset-gap" to "already covered on the current seam"

Task 8 inherited an older reading that `Prone` still needed three tier3 rows. Current repo evidence is tighter than that:

- audit input: `docs/reports/2026-04-11-didx-prone-review.md`
- current message-frame coverage still includes `lie/down`, `stand/up`, and `are/knocked prone` in `verbs.ja.json`
- `MessageFrameTranslator.TryTranslateXDidYToZ(...)` already has the object-phrase fallback for tier1 verb + object-tail assembly on the existing seam
- `scripts/legacies/reconcile_inventory_status.py` recognizes the current `Prone` inventory rows as covered by `MessageFrames/verbs.ja.json`

So for issue #354, `Prone` is no longer actionable as a fresh route or fresh asset task. The audited six `XRL.World.Effects/Prone.cs` rows are now also recorded as:

- `status = translated`
- `existing_dictionary = MessageFrames/verbs.ja.json`

## Regression / bookkeeping guard

Added a repo-backed guard in `scripts/tests/test_reconcile_inventory_status.py` that asserts the audited `Prone` + `HolographicBleeding` `DidX` ids are no longer left in `needs_patch` inside `docs/candidate-inventory.json`.

This is intentionally a bookkeeping regression, not a new route-ownership test.

## Intentionally left unchanged

- `AddPlayerMessage` remains observation-only; no sink promotion was added.
- Broad `EmitMessage` combat/environment families remain partial existing-seam coverage work under the current producer-owned seam.
- `Does` composition-specific families (`is empty`, `has no room for more {x}`, `encoded with ...`) remain existing-seam helper/template work; this batch does not broaden into those helpers.
- No Python logic modules were changed; only the bridge artifact and its regression test were updated.

## Files touched for this batch

- `docs/candidate-inventory.json`
- `scripts/tests/test_reconcile_inventory_status.py`

## Evidence used

- `docs/reports/2026-04-12-owner-seam-audit.md`
- `docs/reports/2026-04-11-didx-holographicbleeding-review.md`
- `docs/reports/2026-04-11-didx-prone-review.md`
- `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`
- `Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs`
- `scripts/legacies/reconcile_inventory_status.py`
