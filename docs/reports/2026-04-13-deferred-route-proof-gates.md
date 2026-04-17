# 2026-04-13 deferred route proof gates

## Purpose

This report freezes the entry gates, proof requirements, and non-goals for deferred buckets. It does not change ownership, add producer patches, or widen the implementation queue.

## Source anchors

- `docs/reports/2026-04-12-issue-363-runtime-triage-batch-01.md:57-63`
- `docs/reports/2026-04-11-emit-addplayermessage-batch-01.md:54-64`
- `docs/reports/2026-04-03-color-tag-route-audit.md:63-65,67-70,84-86,91-97,111-125,182-200`
- `docs/test-architecture.md:124-135`
- `docs/reports/2026-04-13-remaining-localization-execution-ledger.md:53-65`
- `docs/reports/2026-04-13-remaining-localization-baseline.md:35-45`

## Entry gate for runtime-owner-proof work

Before any deferred bucket is promoted by runtime evidence, collect a fresh Rosetta `Player.log`. On Apple Silicon, native ARM64 logs do not count as localization proof, so a non-Rosetta log cannot open runtime-owner-proof work (`docs/test-architecture.md:128-135`).

If the log is stale, native, or missing, the work stays deferred.

## Route-proof hold records, `Popup*` / `GetDisplayName*`

These families stay together only as hold records. They are not generic sink owners, and they do not merge into `AddPlayerMessage`.

| Hold record | Named route families | Named owner seam | Hold reason | Current decision |
| --- | --- | --- | --- | --- |
| Popup producer/handoff routes | `PopupShowTranslationPatch`, `PopupMessageTranslationPatch`, `PopupPickOptionTranslationPatch`, `PopupAskStringTranslationPatch`, `PopupAskNumberTranslationPatch`, `PopupShowSpaceTranslationPatch`, `QudMenuBottomContextTranslationPatch` | `PopupTranslationPatch.TranslatePopupTextForProducerRoute`, `PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:238-276`) | The current triage artifact only identifies routes, and the popup handoff still lacks a fresh Rosetta `Player.log`, so this stays a hold record instead of owner-safe backlog. | Hold, no promotion |
| Display-name builder/process routes | `GetDisplayNamePatch`, `GetDisplayNameProcessPatch` | `GetDisplayNameRouteTranslator.TranslatePreservingColors` (`Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:66-185`), `GetDisplayNamePatch.Postfix` (`Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs:13-78`), `GetDisplayNameProcessPatch.Postfix` (`Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs:22-98`) | The triage artifact is route-identifying only, and the builder/process seam still needs a fresh Rosetta `Player.log` before any owner promotion. | Hold, no promotion |
| Sink-observed fallback boundary | `UITextSkinTranslationPatch` | `UITextSkinTranslationPatch.TranslatePreservingColors` (`Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:65-119`), `SinkObservation.LogUnclaimed` (`Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:107-116`) | Observation only. The sink can record unclaimed text, but it does not establish ownership and cannot promote `Popup*` or `GetDisplayName*` on its own. | Observation only, no promotion |

Current `Player.log` evidence in the triage artifact is route-identifying only. It does not satisfy the fresh Rosetta promotion bar unless a new Rosetta log proves the owner seam (`docs/test-architecture.md:128-135`).

## `<no-context>` rebucketing track

`<no-context>` remains its own track. It does not get absorbed into sink traffic, and it does not get merged with generic `AddPlayerMessage` (`docs/reports/2026-04-13-remaining-localization-execution-ledger.md:55-65`).

| Subgroup | Promote | Keep deferred | Reroute |
| --- | --- | --- | --- |
| `<no-context>` | A fresh Rosetta `Player.log` plus a narrower owner route and destination are identified, so the row can be rebucketed cleanly | The row is still unresolved, sink-adjacent, or missing route proof | The row is assigned to the actual upstream family that owns it, not to the generic sink bucket |

Current deterministic handling: if `<no-context>` is present in triage output, keep the route distinct and preserve its per-row classification split (`static_leaf`, `logic_required`, `unresolved`) rather than collapsing it into one deferred blob. If the current evidence set has no `<no-context>` rows, this track is a no-op rather than a backfill target.

## Generic `AddPlayerMessage` exclusion audit

`AddPlayerMessage` stays an observation-only umbrella. The sink itself is not the permanent translation home, and it should only point back to an upstream producer when that producer is proven (`docs/reports/2026-04-11-emit-addplayermessage-batch-01.md:54-64`).

| Subgroup | Promote | Keep deferred | Reroute |
| --- | --- | --- | --- |
| generic `AddPlayerMessage` | An explicit upstream producer family is proven, so the producer route can be promoted instead of the sink | The row is sink-only evidence, observation-only, or still unclaimed | The text belongs to a producer-owned overlay, such as a queue-edge family, and should move there instead of staying at the sink |

Current deterministic handling: generic `AddPlayerMessage` keeps sink ownership in scanner provenance and stays on the review/exclusion path unless a producer-specific route has already been split out and proven separately.

## Non-goals

- Do not implement producer patches here.
- Do not broaden scanner or classifier logic here beyond the deterministic guardrails that keep `<no-context>` separate and generic `AddPlayerMessage` observation-only.
- Do not merge `Popup*`, `GetDisplayName*`, `<no-context>`, or generic `AddPlayerMessage` into one bucket.
- Do not treat non-Rosetta logs as proof for runtime-owner work.
- Do not convert the sink into a permanent translation home.
