# 2026-04-11 DidX review: `XRL.World.Effects/HolographicBleeding.cs`

## Scope

Inventory filter used:

- `source_route = DidX`
- `type = MessageFrame`
- `status = needs_patch`
- `file = XRL.World.Effects/HolographicBleeding.cs`

Total sites in this family: **10**

## Current producer patterns

| Inventory id | Shape |
| --- | --- |
| `XRL.World.Effects/HolographicBleeding.cs::L37:C5` | `DidX("begin", base.DisplayNameStripped + " from another wound", "!", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L41:C5` | `DidX("begin", base.DisplayNameStripped, "!", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L46:C4` | `DidX("begin", "acting like " + Object.itis + " " + base.DisplayNameStripped + " from another wound", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L50:C4` | `DidX("begin", "acting like " + Object.itis + " " + base.DisplayNameStripped, ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L62:C6` | `DidX("realize", "one of your wounds is an illusion", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L66:C6` | `DidX("realize", "your wound is an illusion", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L71:C5` | `DidX("realize", "one of your wounds is an illusion, and the pain from it suddenly stops", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L75:C5` | `DidX("realize", "your wound is an illusion, and the pain suddenly stops", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L80:C4` | `DidX("stop", "acting like " + Object.itis + " " + base.DisplayNameStripped + " so much", ...)` |
| `XRL.World.Effects/HolographicBleeding.cs::L84:C4` | `DidX("stop", "acting like " + Object.itis + " " + base.DisplayNameStripped, ...)` |

## Existing message-frame asset coverage

Already present in `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`:

- `begin` + `acting like {0} {1}` (`1988-1990`)
- `begin` + `acting like {0} {1} from another wound` (`1993-1995`)
- `begin` + `{0}` (`1998-2000`)
- `begin` + `{0} from another wound` (`2003-2005`)
- `realize` + `one of your wounds is an illusion` (`1496-1498`)
- `realize` + `one of your wounds is an illusion, and the pain from it suddenly stops` (`1501-1503`)
- `realize` + `your wound is an illusion` (`1506-1508`)
- `realize` + `your wound is an illusion, and the pain suddenly stops` (`1511-1513`)
- `stop` + `acting like {0} {1}` (`2518-2520`)
- `stop` + `acting like {0} {1} so much` (`2523-2525`)

## Ownership reading

This family now looks like an **existing-owner, existing-asset** case.

Reasons:

1. The producer is already on the current `DidX` / `MessageFrameTranslator` seam.
2. All ten observed tails have direct or placeholder-shaped matches in `verbs.ja.json`.
3. The only remaining question is whether the runtime normalized tails match these rows exactly enough to resolve.

## Initial verdict

`HolographicBleeding` should be treated as a **verification / reclassification candidate**, not as a new template-addition task.

### Practical meaning

- no new producer patch is indicated
- no obviously missing message-frame asset row remains from the current static evidence
- next step is to confirm that inventory is stale or overly conservative for this family

## Likely next checks

1. compare the actual normalized `extra` strings emitted by this family with the existing placeholder rows
2. confirm whether `Object.itis` and `base.DisplayNameStripped` land in the same slot order as `{0} {1}`
3. if normalization matches, update triage status instead of adding route work
