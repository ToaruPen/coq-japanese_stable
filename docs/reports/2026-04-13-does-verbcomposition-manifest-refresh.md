# 2026-04-13 Does / VerbComposition manifest refresh

## Purpose

This refresh keeps the Does bucket on the existing seam, not on a new route gap. The seam is already in place via `DoesFragmentMarkingPatch` and `DoesVerbRouteTranslator`, and the current work remains existing-seam verification plus narrow asset-gap cleanup, not route discovery (`Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs:8-60`, `Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs:14-195,256-380`, `docs/reports/2026-04-12-owner-seam-audit.md:19-20`).

## Current counts

| Item | Count | Reading |
| --- | ---: | --- |
| old manifest rows | 157 | the March split from `docs/superpowers/plans/2026-03-24-does-verb-manifest.md:7-20` |
| current Does queue | 283 | current `needs_review` queue for `Does / VerbComposition` (`docs/reports/2026-04-13-remaining-localization-baseline.md:25-33`, `docs/reports/2026-04-11-fixed-leaf-owner-triage.md:52-60,102-110`) |
| broader VerbComposition inventory | 289 | current inventory total for the route family (`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:34-45`) |

The key point is that the April queue is bigger than the March manifest, but the original four-way split still holds as the batch-selection map for the queue we already understand.

## Reconciled four-way split

| Subgroup | March manifest count | Representative families now | Use this for |
| --- | ---: | --- | --- |
| `message-frame-normalizable` | 78 | `stunned`, `open`, `already full`, `already fully loaded`, `falls to the ground`, `returns to the ground`, `shies away from you`, `starts to glitch`, `unconvinced by your pleas` | Task 6 quick wins, keep on the existing Does seam and route through `MessageFrameTranslator` |
| `does-composition-specific` | 66 | `is empty`, `is unresponsive`, `has no room for more {x}`, `encoded with ...`, `engaged in ... and is too busy ...`, `shares ...`, `teaches ...`, `detaches`, `kicks`, `reflects`, `ponies up`, `needs water in it`, `needs to be hung up first`, `sees no reason for you to amputate` | Task 7 family translators and producer-side helpers |
| `emit-message-overlap` | 3 | `MagazineAmmoLoader` no more ammo, `MissileWeapon` suppressive fire, `MissileWeapon` flattening fire | provenance-only overlap, keep out of the true Does queue |
| `needs-harmony-patch` | 10 | `exhausted`, `sealed`, `utterly unresponsive`, `not bleeding`, `no limbs`, `beeps loudly and flashes a warning glyph` | leftover owner-specific patch work |

## Batch selection guidance

### Task 6 source set

Start with the `message-frame-normalizable` rows that already read like direct `MessageFrameTranslator` reuse:

- quick wins already called out in earlier notes, `stunned`, `open`, `shies away from you`, `starts to glitch`
- stable status and motion groups, `already fully loaded`, `already full`, `falls to the ground`, `returns to the ground`
- low-risk sentence forms that stay on the same seam, `unconvinced by your pleas`, `resists your life drain`, `needs to be hung up first`

### Task 7 source set

Take the `does-composition-specific` families next, because they need producer-side composition handling rather than sentence normalization:

- ownership and encoded-state families, `is empty`, `is unresponsive`, `has no room for more {x}`, `encoded with ...`
- social and busy-state families, `engaged in ...`, `shares ...`, `teaches ...`, `ponies up ...`
- interaction families, `detaches`, `kicks`, `reflects`, `sees no reason for you to amputate`, `needs water in it`

## Task 7 status after the composition-specific helper/template batch

- Completed on the existing Does seam with route-level proof: `has no room for more {x}` and the representative `encoded with ...` variants. The current `verbs.ja.json` helper/template entries already satisfy these families, and the route proof now lives in `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbRouteTranslatorTests.cs` rather than in sink observation.
- Kept separate from this batch: `is empty`. It remains outside the Does-seam proof target for this task and should stay with the `WorldPartsFragmentTranslatorTests` seam-specific work instead of being pulled back into `DoesVerbRouteTranslatorTests`.
- Still deferred within `does-composition-specific`: the social/busy families (`engaged in ...`, `shares ...`, `teaches ...`, `ponies up ...`) and the broader interaction families (`detaches`, `kicks`, `reflects`, `sees no reason for you to amputate`, `needs water in it`) because they were not needed for this smallest safe batch.

## Notes for downstream batching

- Keep `emit-message-overlap` as a provenance flag, not a destination bucket. The old manifest already treated it that way, and the April audit still does not move it into true Does ownership.
- `emit-message-overlap` remains out of scope after this batch as well; proving `has no room for more {x}` on the Does seam does not change the `EmitMessage` provenance-only treatment for the `MagazineAmmoLoader` / `MissileWeapon` overlap rows.
- Keep `needs-harmony-patch` separate from both normalized and composition-specific work. These rows are the leftovers after seam-safe rows are removed.
- This report is the batch-selection source for Tasks 6 and 7. It should let the next pass pick exact families without re-triaging the whole Does queue.
