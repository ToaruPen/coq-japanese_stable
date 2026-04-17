# 2026-04-13 remaining localization final

## Final scan and branch state

- Current `docs/candidate-inventory.json` metadata reports 4634 output sites, 777 rows flagged with `needs_review=true`, 2688 rows flagged with `needs_runtime=true`, and fixed-leaf validation split as 692 proven vs 3942 rejected.
- Reconciled bridge inventory status counts in `docs/candidate-inventory.json` are 2930 `translated`, 1199 `unresolved`, 200 `needs_review`, 40 `needs_patch`, and 265 `excluded`.
- The last two audited DidX rows that needed final inventory alignment, `XRL.World.Capabilities/Firefighting.cs::L125:C5` and `XRL.World.Parts.Mutation/ElectricalGeneration.cs::L384:C5`, are now aligned to `MessageFrames/verbs.ja.json` in the final inventory.

## Completed owner-safe families and seams

- DidX / MessageFrame is still an existing-seam track, not a new route discovery. The audited `Prone`, `HolographicBleeding`, `Firefighting`, and `ElectricalGeneration` rows now sit on `MessageFrames/verbs.ja.json` and are translated in the final bridge inventory (`docs/reports/2026-04-11-didx-prone-review.md`, `docs/reports/2026-04-11-didx-holographicbleeding-review.md`, `docs/reports/2026-04-11-didx-firefighting-review.md:38-55`, `docs/reports/2026-04-11-didx-electricalgeneration-review.md:42-76`, `docs/reports/2026-04-12-owner-seam-audit.md:17-18`).
- The broader owner-safe framing still holds for Does / VerbComposition, description seams, active-effect seams, and producer-owned EmitMessage families, which remain existing-seam work in the baseline and execution ledger (`docs/reports/2026-04-13-remaining-localization-baseline.md:21-39`, `docs/reports/2026-04-13-remaining-localization-execution-ledger.md:47-52`).

## Deferred holds still blocked by owner proof

- `Popup*` stays deferred until fresh Rosetta `Player.log` proof arrives.
- `GetDisplayName*` stays deferred on the same freshness gate.
- `<no-context>` remains its own rebucketing track, and Task 12's deterministic triage evidence now explicitly names the existing `scripts.triage.cli` package entrypoint for directory-based classification and empty-report format checks.
- generic `AddPlayerMessage` stays observation-only and excluded from promotion.
- These holds remain separated by the proof gates in `docs/reports/2026-04-13-deferred-route-proof-gates.md:22-52` and the execution ledger hold rules in `docs/reports/2026-04-13-remaining-localization-execution-ledger.md:53-65`.

## Explicit residue and blockers

- Fixed-leaf residue is still the 27-row `SetText/Leaf/excluded` queue, which is bookkeeping residue, not actionable promotion work (`docs/reports/2026-04-13-remaining-localization-baseline.md:54-70`).
- Non-goal residue still includes `HistoricStringExpander`, `JournalAPI`, `ReplaceBuilder`, `Mutations.GetLevelText`, `Mutations.GetDescription`, `GetShort/LongDescription`, and the remaining `SetText` / `EmitMessage` template and unresolved rows (`docs/reports/2026-04-13-remaining-localization-baseline.md:71-86`).
- No new owner-proof blocker was introduced by the final scan. The remaining backlog is intentionally partitioned into owner-safe work, deferred holds, and non-goal residue.
