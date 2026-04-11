# 2026-04-11 DidX / MessageFrame batch 01

## Why this batch exists

`docs/candidate-inventory.json` currently contains **338** `source_route=DidX`, `type=MessageFrame`, `status=needs_patch` sites.

That is too large to review site-by-site without first removing obvious framework-level noise and choosing concrete producer families.

## First split from the inventory

Top files in the current DidX bucket:

| Count | File |
| ---: | --- |
| 15 | `XRL.World/IComponent.cs` |
| 10 | `XRL.World.Effects/HolographicBleeding.cs` |
| 9 | `XRL.World.Parts.Mutation/ElectricalGeneration.cs` |
| 6 | `XRL.World.Capabilities/Firefighting.cs` |
| 6 | `XRL.World.Effects/Prone.cs` |
| 6 | `XRL.World.Parts.Mutation/MultiHorns.cs` |
| 6 | `XRL.World.Parts.Skill/Tactics_Charge.cs` |
| 6 | `XRL.World.Parts/Inventory.cs` |
| 5 | `XRL.World.Parts/Interior.cs` |
| 5 | `XRL.World.Parts/LongBladesCore.cs` |

## Important pruning finding

The largest file, `XRL.World/IComponent.cs`, is not a concrete gameplay family. Its 15 rows are generic helper overloads such as:

- `Messaging.XDidY(...)`
- `Messaging.XDidYToZ(...)`
- `Messaging.WDidXToYWithZ(...)`

These are API surfaces, not direct user-facing producer callsites. They should **not** be treated as the first manual review batch.

## Batch 01: concrete families to review first

This first batch keeps only concrete producer families that already look close to the existing `MessageFrameTranslator` seam.

### 1. `XRL.World.Effects/HolographicBleeding.cs` (10 sites)

Representative patterns:

- `DidX("begin", base.DisplayNameStripped + " from another wound", "!", ...)`
- `DidX("realize", "your wound is an illusion", ...)`
- `DidX("stop", "acting like " + Object.itis + " " + base.DisplayNameStripped, ...)`

Why first:

- concrete effect-family producer
- repeated local phrase shapes
- likely needs a small number of verb/extra decisions rather than a brand-new ownership route

### 2. `XRL.World.Parts.Mutation/ElectricalGeneration.cs` (9 sites)

Representative patterns:

- `DidX("discharge", Amount + " units of electrical charge", "!")`
- `DidXToY("drink", "the juice from", E.Item, "and recharge ...")`
- `DidX("recharge", num + " units of electrical charge from the damage")`

Why first:

- concrete mutation-family producer
- mixes `DidX` and `DidXToY`
- good test case for whether current frame assets are enough or explicit templates are needed

### 3. `XRL.World.Capabilities/Firefighting.cs` (6 sites)

Representative patterns:

- `Messaging.XDidY(Actor, "roll", "on the ground", "!", ...)`
- `Messaging.XDidYToZ(Actor, "try", "to beat at the flames on", Subject, ", but ...", "!", ...)`
- `Messaging.XDidYToZ(Actor, "beat", "at the flames on", Subject, "with ...", "!", ...)`

Why first:

- compact family
- mixes plain and richer frame forms
- likely exposes whether existing frame tiers already cover combat-capability wording

### 4. `XRL.World.Effects/Prone.cs` (6 sites)

Representative patterns:

- `DidXToY("lie", "down on", LyingOn, ...)`
- `DidX("are", "knocked prone", "!", ...)`
- `DidXToY("stand", "up from", LyingOn, ...)`

Why first:

- compact status-effect family
- repeated verbs with small phrase variations
- likely close to existing `verbs.ja.json` coverage shape

## Existing seam this batch should target

Repo docs already point to the producer-side message-frame path:

- `XDidYTranslationPatch` + `MessageFrameTranslator`
- `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`

See:

- `docs/superpowers/plans/2026-03-24-migration-decisions.md`
- `docs/superpowers/plans/2026-03-23-static-translation-batch.md`
- `docs/reports/2026-04-03-color-tag-ownership-gaps.md`

## Existing dictionary coverage already present

`Mods/QudJP/Localization/MessageFrames/verbs.ja.json` already contains many of the verbs used by this batch.

Base verb hits observed in the current asset:

- `are`
- `beat`
- `begin`
- `discharge`
- `drink`
- `lie`
- `realize`
- `recharge`
- `rise`
- `roll`
- `stand`
- `stop`
- `touch`
- `try`

This matters because batch 01 is not starting from an empty message-frame asset. The likely work split is:

1. reuse existing verb rows where the current `verb` alone is enough
2. add or adjust `extra`-qualified rows where the family needs phrase-specific handling
3. only add a new producer-side helper when the current frame contract cannot safely express the family

### Batch-01 reading after checking `verbs.ja.json`

- `Prone` now looks like the best first concrete review target because `lie`, `are`, `rise`, and `stand` already exist in the message-frame verb asset.
- `Firefighting` also looks promising because `roll`, `try`, and `beat` already exist.
- `HolographicBleeding` is likely partly asset-driven (`begin`, `realize`, `stop`) but may still need phrase-level `extra` review because of dynamic tails such as `acting like ...`.
- `ElectricalGeneration` already overlaps with `discharge`, `drink`, `recharge`, `touch`, and `try`, so the main question is whether its richer `DidXToY` shapes fit current tiers or need explicit templates.

## Next action from this batch

For the next pass, review these four files against the current message-frame implementation and sort each site into one of:

1. `verbs.ja.json` entry already sufficient with new verb/tier data
2. existing `MessageFrameTranslator` template path can own it with no new route
3. needs a new explicit producer-side helper/template
4. should leave the DidX bucket and be reclassified

Operationally, the next concrete order should be:

1. `XRL.World.Effects/Prone.cs`
2. `XRL.World.Capabilities/Firefighting.cs`
3. `XRL.World.Effects/HolographicBleeding.cs`
4. `XRL.World.Parts.Mutation/ElectricalGeneration.cs`

## Preliminary ownership verdict for batch 01

The existing owner path is already explicit:

- producer patch entry: `XDidYTranslationPatch`
- frame resolver: `MessageFrameTranslator`
- asset source: `Localization/MessageFrames/verbs.ja.json`
- sink re-translation guard: direct marker (`'\x01'`)

This is backed by:

- `Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs`
- `Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs`

Given that contract, the current batch-01 families read as follows.

| Family | Site count | Initial verdict | Why |
| --- | ---: | --- | --- |
| `XRL.World.Effects/Prone.cs` | 6 | `asset-first` | Existing seam is correct; missing coverage is limited to three object-tail tier3 rows (`down on {0}`, `from {0}`, `up from {0}`). |
| `XRL.World.Capabilities/Firefighting.cs` | 6 | `reclassify-likely` | Existing `verbs.ja.json` already contains `roll on the ground`, `beat ... flames ...`, and `try ... flames ...` rows, so this looks closer to a verification/reclassification task than new route work. |
| `XRL.World.Effects/HolographicBleeding.cs` | 10 | `reclassify-likely` | Current `verbs.ja.json` already contains `begin` / `realize` / `stop` rows for the observed illusion and `acting like ...` tails, so this now looks like a verification task instead of new template work. |
| `XRL.World.Parts.Mutation/ElectricalGeneration.cs` | 9 | `reclassify-likely` | Static scan already finds matching `discharge`, `drink`, `recharge`, `seem`, `try-to-touch`, and the plain `touch` path can fall back through the existing `tier1 + object phrase` seam. |

### What `asset-first` means here

It does **not** mean exact-leaf dictionary work. It means:

1. stay on the existing `DidX` / `MessageFrameTranslator` owner seam
2. prefer `verbs.ja.json` tier additions or exact tail/template rows first
3. only add new C# route logic if the current frame contract cannot express the family safely

## Immediate next review unit

The first concrete owner-side review should be `XRL.World.Effects/Prone.cs`.

Reasons:

- smallest semantic surface in batch 01
- all observed verbs already exist in `verbs.ja.json`
- no obvious dynamically composed object tails beyond `down on {0}` / `up from {0}`
- likely to confirm the fastest path for other DidX families: asset extension on the existing seam, not a new owner route

## Updated batch state

After the first two concrete reviews:

- `Prone` is technically settled as **existing-owner seam + new tier3 asset rows**.
- `Firefighting` is technically settled as **existing-owner seam + likely status correction after verification**.

At this point, all four concrete families in batch 01 appear to be on the existing owner seam already:

- `Prone` -> missing tier3 asset rows only
- `Firefighting` -> likely stale inventory / verification needed
- `HolographicBleeding` -> likely stale inventory / verification needed
- `ElectricalGeneration` -> likely stale inventory / verification needed

## Batch 01 closing state

`DidX / MessageFrame batch 01` is now effectively closed as a triage batch.

What remains is not architecture discovery but follow-through work:

1. `Prone`: add the three missing tier3 asset rows
2. `Firefighting`: verify normalization against existing rows, then reclassify if confirmed
3. `HolographicBleeding`: verify normalization against existing rows, then reclassify if confirmed
4. `ElectricalGeneration`: verify normalization against existing rows, then reclassify if confirmed

## Follow-through map

| Family | Review note | Next concrete action |
| --- | --- | --- |
| `Prone` | `docs/reports/2026-04-11-didx-prone-review.md` | Add the three missing tier3 rows for `down on {0}` / `from {0}` / `up from {0}` on the existing `verbs.ja.json` seam. |
| `Firefighting` | `docs/reports/2026-04-11-didx-firefighting-review.md` | Verify normalized `extra` strings against existing `roll` / `beat` / `try` flame rows, then downgrade from `needs_patch` if they resolve. |
| `HolographicBleeding` | `docs/reports/2026-04-11-didx-holographicbleeding-review.md` | Verify placeholder slot order for `acting like {0} {1}` and illusion tails, then downgrade from `needs_patch` if they resolve. |
| `ElectricalGeneration` | `docs/reports/2026-04-11-didx-electricalgeneration-review.md` | Verify plain `touch` and quantity-bearing recharge/discharge tails against existing rows, then downgrade from `needs_patch` if they resolve. |

This leaves batch 01 in a PR-friendly state: ownership is decided, and every remaining action is now a concrete asset or reclassification follow-through rather than open-ended route discovery.
