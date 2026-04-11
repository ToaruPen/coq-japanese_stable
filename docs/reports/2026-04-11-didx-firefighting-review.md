# 2026-04-11 DidX review: `XRL.World.Capabilities/Firefighting.cs`

## Scope

Inventory filter used:

- `source_route = DidX`
- `type = MessageFrame`
- `status = needs_patch`
- `file = XRL.World.Capabilities/Firefighting.cs`

Total sites in this family: **6**

## Current producer patterns

| Inventory id | Shape |
| --- | --- |
| `XRL.World.Capabilities/Firefighting.cs::L86:C4` | `Messaging.XDidY(Actor, "roll", "on the ground", "!", ...)` |
| `XRL.World.Capabilities/Firefighting.cs::L103:C7` | `Messaging.XDidYToZ(Actor, "try", "to beat at the flames on", Subject, ", but ... dodges", "!", ...)` |
| `XRL.World.Capabilities/Firefighting.cs::L109:C6` | `Messaging.XDidYToZ(Actor, "try", "to beat at the flames on", Subject, ", but ... pass through ...", "!", ...)` |
| `XRL.World.Capabilities/Firefighting.cs::L121:C5` | `Messaging.XDidY(Actor, "beat", "at the flames with " + Actor.its + " " + bodyPart.Name, "!", ...)` |
| `XRL.World.Capabilities/Firefighting.cs::L125:C5` | `Messaging.XDidYToZ(Actor, "beat", "at the flames on", Subject, "with " + Actor.its + " " + bodyPart.Name, "!", ...)` |
| `XRL.World.Capabilities/Firefighting.cs::L181:C4` | same as line 86 |

## Existing message-frame asset coverage

Already present in `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`:

- `roll` + `on the ground` → `地面を転がった` (`1546-1548`)
- `beat` + `at the flames on {0} with {1}` → `{0}の炎を{1}で叩いた` (`1968-1970`)
- `beat` + `at the flames with {0}` → `炎を{0}で叩いた` (`1973-1975`)
- `beat` + `at the flames with {0} {1}` → `{0}{1}で炎を叩いた` (`1978-1980`)
- `try` + `to beat at the flames on {0}, but {1}` (`2613-2615`)
- `try` + `to beat at the flames on {0}, but {1} dodges` (`2618-2620`)

## Ownership reading

This family looks like an **existing-owner, existing-asset** case.

Reasons:

1. The producer is already on `Messaging.XDidY` / `XDidYToZ`, so it belongs to the current `XDidYTranslationPatch` seam.
2. The needed verb/tail shapes are already present in `verbs.ja.json`.
3. The current translator tests already exercise these exact structural patterns for `try ... flames ...` and `beat ... flames ...` object-tail frames.

## Initial verdict

`Firefighting` should be treated as a **reclassification / verification candidate**, not as a new-owner-route task.

### Practical meaning

- no new producer patch is indicated
- no new message-frame asset row is obviously required from the current evidence
- the next step is to verify why inventory still marks these six sites as `needs_patch` despite existing asset coverage

## Likely next checks

1. compare the normalized tails produced at runtime with the existing `verbs.ja.json` rows
2. confirm whether pronoun/possessive wording such as `its fists` / `pass through` is already normalized into the covered placeholder shapes
3. if normalization matches, update triage status instead of adding new route work
