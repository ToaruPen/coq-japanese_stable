# 2026-05-05 issue 493 static producer inventory

## Scope

This report records the first non-legacy static producer inventory for issue `#493`.
The inventory is generated from the current decompiled C# source under
`~/dev/coq-decompiled_stable` and does not use `Player.log` for discovery.

The committed machine-readable artifact is:

- `docs/static-producer-inventory.json`

The generator is:

- `scripts/scan_static_producer_inventory.py`
- `scripts/tools/StaticProducerInventoryScanner/` Roslyn syntax scanner

Historical scanner outputs under `scripts/legacies/`,
`docs/archive/candidate-inventory-2026-04-14-reconciled-bridge.json`, and old
emit-message audit notes are not source of truth for this artifact.

## Reproduction

```bash
python3.12 scripts/scan_static_producer_inventory.py \
  --source-root ~/dev/coq-decompiled_stable \
  --output docs/static-producer-inventory.json
```

The JSON intentionally omits timestamps and absolute source-root paths so the
output remains stable across machines with the same decompiled source snapshot.

## Inventory shape

The scanner inventories exactly these surfaces:

- `EmitMessage`
- `Popup.Show*`
- `AddPlayerMessage`

Producer families are grouped by nearest source owner:

```text
{relative_file}::{type_name}.{member_name}
```

This keeps the inventory producer-family oriented rather than sink-callsite
or text-skeleton oriented. A single family can contain multiple target
surfaces.

## Totals

| Metric | Count |
| --- | ---: |
| callsites | 2208 |
| producer families | 1012 |
| text arguments | 2238 |

### Callsites by surface

| Surface | Count |
| --- | ---: |
| `Popup.Show*` | 1393 |
| `AddPlayerMessage` | 583 |
| `EmitMessage` | 232 |

### Text-argument status by surface

| Surface | `messages_candidate` | `owner_patch_required` | `runtime_required` | `sink_observed_only` | `debug_ignore` |
| --- | ---: | ---: | ---: | ---: | ---: |
| `EmitMessage` | 162 | 0 | 69 | 1 | 0 |
| `Popup.Show*` | 554 | 567 | 194 | 8 | 100 |
| `AddPlayerMessage` | 0 | 444 | 57 | 19 | 63 |

### Family closure status

| Family closure status | Count |
| --- | ---: |
| `owner_patch_required` | 416 |
| `messages_candidate` | 262 |
| `runtime_required` | 167 |
| `needs_family_review` | 144 |
| `sink_observed_only` | 15 |
| `debug_ignore` | 8 |

## Status meanings

- `messages_candidate`: statically visible text that can be reviewed later for
  a route-scoped message/popup candidate. This is not an automatic dictionary
  addition.
- `owner_patch_required`: statically visible text assembled by a producer where
  ownership should stay with the producer route rather than a broad sink.
- `runtime_required`: static analysis cannot safely infer a stable candidate.
  This bucket is explicit and must not be counted as implementation-ready.
- `sink_observed_only`: generic sink or forwarding wrapper traffic. This is not
  a translation owner.
- `debug_ignore`: wish/debug/diagnostic output.
- `needs_family_review`: a family mixes actionable and deferred statuses and
  should be split or reviewed before implementation work is assigned.

## Policy notes

- `AddPlayerMessage` is never classified as `messages_candidate` in this
  inventory. Literal or templated non-wrapper `AddPlayerMessage` producers are
  marked `owner_patch_required` to avoid promoting the message queue sink into
  dictionary ownership.
- `Popup.Show*` exact literals can be `messages_candidate`, but templated popup
  text is `owner_patch_required`.
- `EmitMessage` exact and templated text are `messages_candidate` because the
  current code has an explicit emit-message route surface for later
  message-pattern review. This does not claim the family is already covered.
- `covered_existing_route` and `already_tested` are not assigned by this first
  artifact. Future passes can populate current-repo evidence from tests and
  implementation, but stale docs must not populate it.

## Representative high-volume families

These families have the largest `owner_patch_required` counts and are useful
starting points for later batches:

| Count | Producer family | Surfaces |
| ---: | --- | --- |
| 15 | `XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal` | `AddPlayerMessage` |
| 13 | `XRL.World.Parts/LongBladesCore.cs::XRL.World.Parts.LongBladesCore.FireEvent` | `AddPlayerMessage`, `Popup.Show*` |
| 12 | `XRL.World.Parts/PetEitherOr.cs::XRL.World.Parts.PetEitherOr.explode` | `AddPlayerMessage` |
| 11 | `XRL.Core/XRLCore.cs::XRL.Core.XRLCore.PlayerTurn` | `AddPlayerMessage`, `Popup.Show*` |
| 11 | `XRL.UI/TradeUI.cs::XRL.UI.TradeUI.PerformOffer` | `Popup.Show*` |

## Current branch implementation overlay

`docs/static-producer-inventory.json` is static decompiled-source evidence and
does not mark current-repo coverage by itself. The issue-493 implementation
branch adds current-repo owner evidence for these producer families:

| Producer family | Inventory status | Current branch route evidence |
| --- | --- | --- |
| `XRL.UI/TradeUI.cs::XRL.UI.TradeUI.PerformOffer` | `needs_family_review` | Dynamic water-payment popup text is removed from `ui-trade.ja.json` and handled by `TradeUiPopupTranslationPatch.TryTranslatePerformOfferTradeWaterMessage`. L1/L2 tests prove dictionary entries are ignored for this owner-template path and color markup is preserved. |
| `XRL.World.Parts/PetEitherOr.cs::XRL.World.Parts.PetEitherOr.explode` | `owner_patch_required` | `PetEitherOrExplodeTranslationPatch` gates `AddPlayerMessage` translation to the `explode()` owner route. L2 tests prove queue-only traffic is not translated, direct markers are preserved, dynamic captures are restored, and whole-message color boundaries survive. |
| `XRL.World/Zone.cs::XRL.World.Zone.WindChange` | `owner_patch_required` | `ZoneWindChangeTranslationPatch` gates wind-change `AddPlayerMessage` translation to the `WindChange(long)` owner route. L2 tests cover direction changes, wind speed changes with and without Trailblazer direction text, unknown component fallback, direct markers, and color markup. |

These owner-route fixes intentionally do not promote generated runtime text to
dictionary leaves. Existing dynamic-looking dictionary debt discovered during
the branch was split into GitHub issue `#497`.

## Verification

Focused scanner verification:

```bash
dotnet build scripts/tools/StaticProducerInventoryScanner/StaticProducerInventoryScanner.csproj --configuration Release
uv run pytest scripts/tests/test_scan_static_producer_inventory.py -q
ruff check scripts/scan_static_producer_inventory.py scripts/tests/test_scan_static_producer_inventory.py
uvx basedpyright scripts/scan_static_producer_inventory.py scripts/tests/test_scan_static_producer_inventory.py
```

The Python entrypoint invokes the Roslyn console scanner and keeps this JSON
shape stable for current consumers.

In the current decompiled `2.0.4` snapshot, Roslyn semantic enrichment resolved
or produced candidate symbols for every inventoried callsite:

| `roslyn_symbol_status` | Count |
| --- | ---: |
| `resolved` | 2206 |
| `candidate` | 2 |

`method_symbol` is present on all 2208 callsites, and `receiver_type_symbol` is
present on 2136 callsites.

## Remaining limits

- The scanner uses Roslyn syntax APIs for call shape and source context, but it
  does not fail when decompiled game source has incomplete semantic resolution.
- `roslyn_symbol_status=candidate` means Roslyn could not choose a single method
  symbol but did provide a candidate method symbol for audit context.
- Runtime logs remain useful for prioritization and verification, but not for
  primary discovery in this artifact.
- `needs_family_review` families need a human split before being counted as
  implementation candidates.
