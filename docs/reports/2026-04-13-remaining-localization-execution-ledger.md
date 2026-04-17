# 2026-04-13 remaining-localization execution ledger

## Cross-cutting rules

- Evidence order is fixed: tests → layer boundaries → fresh runtime logs → decompiled source → older notes (`docs/RULES.md:7-15`).
- Phase F runtime evidence is proof, not behavior definition, and it does not replace the source-first scanner or fixed-leaf workflow (`docs/RULES.md:19-31`).
- Route ownership stays explicit: do not invent sink-wide ownership, and do not add compensating dictionary/XML entries for dynamic, observation-only, or already-owned routes (`docs/RULES.md:43-89`).
- Provenance is required for every accepted candidate: source route, ownership class, confidence, destination dictionary, and rejection reason (`docs/RULES.md:104-126`).
- Runtime proof for deferred work must be Rosetta-backed; native ARM64 logs are not accepted as localization observability evidence (`docs/RULES.md:191-214`).

## Authoritative inputs / source-of-truth map

| Input | Role in this phase | Boundary |
| --- | --- | --- |
| `docs/candidate-inventory.json` | Current static inventory input for backlog partitioning | Review artifact, not a behavioral source of truth |
| `docs/reports/2026-04-12-issue-364-execution-ledger.md` | Prior execution contract and guardrail template | Carry forward, do not rewrite history |
| `docs/reports/2026-04-12-issue-363-runtime-triage-batch-01.md` | Runtime carry-forward decisions for the deferred bucket | `Description*` first; `Popup*`, `GetDisplayName*`, `<no-context>` stay gated |
| `docs/reports/2026-04-12-owner-seam-audit.md` | Route-first cleanup framing | Existing-seam verification vs asset-gap vs true route gap |
| `docs/RULES.md` | Canonical evidence-order / route-ownership / fixed-leaf rules | Highest local policy authority for this ledger |
| `issue-364` notepads and phase notes | Supporting decision memory for wording and carry-forward context | Reference input only; do not treat as mutable history |
| Fresh Rosetta runtime triage artifacts | Proof source for deferred runtime batches | Must include `Player.log`-backed evidence |

## Batch order

### Wave 1: contract + baseline + residual DidX + Does partition + deferred gates

1. Freeze the remaining-localization execution contract and source-of-truth map.
2. Regenerate and partition the remaining localization baseline.
3. Close residual DidX / MessageFrame follow-through on the existing seam.
4. Refresh the Does / VerbComposition manifest against the current queue.
5. Establish deferred-bucket proof gates and evidence requirements.

### Wave 2: existing-seam follow-through

1. Execute Does message-frame-normalizable quick wins.
2. Execute Does composition-specific helper/template batch.
3. Expand description-family coverage on the existing owner seams.
4. Expand active-effect description/details coverage on the existing owner seams.
5. Expand EmitMessage producer-owned family coverage.

### Wave 3: deferred resolution + final cleanup

1. Resolve `Popup*` / `GetDisplayName*` route-proof batches.
2. Rebucket `<no-context>` and audit generic `AddPlayerMessage` exclusions, using the existing `scripts.triage.cli` package entrypoint for deterministic directory-based triage classification and empty-report format checks.
3. Close duplicate-family / destination / fixed-leaf residue and publish the final remaining-work report.

## Owner-safe backlog

- The owner-safe backlog is the work that already sits on an existing owner seam and can be completed without new route discovery.
- This includes residual DidX, Does, description, active-effect, and EmitMessage follow-through when the seam is already proven safe.
- Stable leaves and narrow asset gaps may use localization assets when they satisfy the fixed-leaf and provenance rules from `docs/RULES.md`.

## Deferred bucket

- `Popup*` is deferred bucket work, not an implementation target, until its task-specific proof gate is satisfied.
- `generic AddPlayerMessage` is sink-observed and stays excluded from the implementation queue until explicit owner proof exists.
- `GetDisplayName*` is deferred until tighter owner proof is available.
- `<no-context>` remains intentionally unresolved until it can be rebucketed with route-proof evidence.
- `scripts.triage.cli` is the bridge-side package entrypoint that wraps the existing triage classifier for Task 12 evidence collection, including deterministic directory-based classification and empty-input report checks.
- Deferred items must be tracked as gated work items with reasoned hold / defer / reject records, not as generic backlog.

## Hold language

- Keep the deferred bucket on hold when the available evidence is only sink-observation, unresolved ownership, or Phase F-only runtime proof.
- Do not promote deferred routes into the owner-safe backlog until the proof gate names the owning route and destination explicitly.
- Do not collapse `Popup*`, `GetDisplayName*`, `<no-context>`, and `generic AddPlayerMessage` into one unresolved bucket; each needs its own decision record.

## Durable outputs

- A report that names the authoritative inputs and the batch order.
- A report that separates owner-safe backlog work from deferred-bucket owner-proof work.
- A report that preserves the carry-forward decisions from issue-363 and the route-first framing from the owner-seam audit.

## Stop conditions

- Stop if any deferred track lacks Rosetta-backed `Player.log` evidence.
- Stop if a change would invent sink ownership or add compensating dictionary entries for dynamic / unresolved routes.
- Stop if the wording drifts away from the issue-364 ledger shape or the plan’s wave order.
