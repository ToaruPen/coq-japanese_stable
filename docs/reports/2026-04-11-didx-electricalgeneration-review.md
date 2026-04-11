# 2026-04-11 DidX review: `XRL.World.Parts.Mutation/ElectricalGeneration.cs`

## Scope

Inventory filter used:

- `source_route = DidX`
- `type = MessageFrame`
- `status = needs_patch`
- `file = XRL.World.Parts.Mutation/ElectricalGeneration.cs`

Total sites in this family: **9**

## Current producer patterns

| Inventory id | Shape |
| --- | --- |
| `...::L164:C4` | `DidX("discharge", Amount + " units of electrical charge", "!")` |
| `...::L168:C4` | `DidX("discharge", "an electrical arc", "!")` |
| `...::L384:C5` | `DidXToY("try", "to touch", E.Item, ", but ...", ...)` |
| `...::L396:C8` | `DidXToY("drink", "the juice from", E.Item, "and recharge ... units of electrical charge", ...)` |
| `...::L400:C8` | `DidXToY("drink", "the juice from", E.Item, "but don't seem to retain any of it", ...)` |
| `...::L405:C7` | `DidXToY("touch", E.Item)` |
| `...::L414:C6` | `DidXToY("touch", E.Item, ..., IndefiniteObject: true)` |
| `...::L458:C6` | `DidX("recharge", num + " unit(s) of electrical charge from the damage")` |
| `...::L462:C6` | `DidX("seem", "to have absorbed some of the electrical charge")` |

## Existing message-frame asset coverage seen in static scan

Observed matching rows already present in `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`:

- `discharge` + `an electrical arc` (`1187`)
- `discharge` + `{0} units of electrical charge` (`2064`)
- `drink` + `the juice from {0} and recharge {1} unit of electrical charge` (`2104`)
- `drink` + `the juice from {0} and recharge {1} units of electrical charge` (`2109`)
- `recharge` + `{0} unit of electrical charge from the damage` (`2374`)
- `recharge` + `{0} units of electrical charge from the damage` (`2379`)
- `recharge` + `{0} {1} of electrical charge from the damage` (`2384`)
- `seem` + `to have absorbed some of the electrical charge` (`1552`)
- `try` + `to touch {0}, but {1}` (`2644`)

## Current reading

Static evidence now suggests this family is much closer to `Firefighting` / `HolographicBleeding` than to a true missing-owner case.

What already looks covered:

1. discharge quantity variants
2. discharge arc variant
3. drink + recharge quantity variants
4. recharge-from-damage quantity variants
5. `seem ... absorbed some of the electrical charge`
6. `try to touch {0}, but {1}`

## Touch-path resolution

The remaining uncertainty from the first pass was the plain `DidXToY("touch", E.Item)` path.

That uncertainty is now small:

- `touch` already exists as a base message-frame verb in `verbs.ja.json`
- `MessageFrameTranslator.TryTranslateXDidYToZ(...)` falls back to `tier1 verb + object phrase` when `extra` is blank
- the same seam already handles single-object tails through exact/template resolution for richer `DidXToY` cases

So the plain `touch` form no longer looks like evidence for a missing C# route.

## Verdict

`ElectricalGeneration` is currently **reclassify-likely**.

From the repo evidence collected so far, it no longer looks like a new-template or new-C#-route case.

### Practical meaning

- keep this family on the existing `DidX` / `MessageFrameTranslator` seam
- prefer status correction / verification over new owner-route work
- if any subcase still fails at runtime, it should be narrowed to a concrete normalization mismatch rather than treated as a missing architecture seam
