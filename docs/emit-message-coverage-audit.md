# Emit-message coverage audit

## Scope and evidence model

This audit covers message-log text that enters through:

- `XRL.World.GameObject.EmitMessage(string, GameObject, string, bool)`
- `XRL.World.Capabilities.Messaging.EmitMessage(GameObject, string, char, bool, bool, bool, GameObject, GameObject)`

Evidence is ranked as:

1. **Tests** — source of truth for current behavior
2. **Current runtime logs** — current evidence only
3. **Decompiled producer inspection** — inventory and ownership evidence only

This document is intentionally conservative. It does **not** claim universal emit-message coverage.

## Route summary

`GameObjectEmitMessageTranslationPatch` wraps two producer entrypoints: the 4-argument `GameObject.EmitMessage` instance method and the 8-argument `Messaging.EmitMessage` helper. Its `Prefix()` increments a thread-static `activeDepth`, its `Finalizer()` decrements it, and `TryTranslateQueuedMessage()` only claims queued messages while `activeDepth > 0`, forwarding them through `MessageLogProducerTranslationHelpers.TryPreparePatternMessage(..., markJapaneseAsDirect: true)`.[^emit-patch]

The actual queue interception happens in `CombatAndLogMessageQueuePatch`, which short-circuits through a fixed OR-chain before it reaches the emit route. `GameObjectEmitMessageTranslationPatch` is slot **15** in a chain of **19** delegates, after 14 earlier owner-specific routes including heal, move, melee attack, die, regenera, spot, and lost-sight patches.[^queue-patch] The emit route is therefore a **generic fallback inside the queue patch**, not the sole owner of every string produced during an `EmitMessage` call.

`markJapaneseAsDirect: true` is the key mixed-language behavior. If stripped text already contains Japanese, `TryPreparePatternMessage()` either:

1. applies a matching pattern and marks the result direct, or
2. marks the original string direct even when no pattern matches.[^helpers]

That prevents downstream double-translation, but it does **not** prove the repository owns a good Japanese template for every mixed JP/EN skeleton.

### Current guarantees

- **Guaranteed by L2 tests:** both emit entrypoints are wired correctly and can claim queued messages while `activeDepth > 0`.[^l2-emit]
- **Guaranteed by repository tests:** the repo dictionary translates the tested player hit-with-roll family and the tested `acid!`-terminated player and third-person acid families through the emit route.[^l1-repo][^l2-hit-acid]
- **Maintained invariant:** the hit-with-roll family is only correct while its specific patterns remain ordered ahead of the generic hit patterns in `messages.ja.json`.[^l1-ordering]
- **Guaranteed only structurally, not universally:** mixed-language emit messages can be claimed and marked direct without double-processing, but only a narrow subset is explicitly exercised by tests.[^l2-mixed]
- **Not guaranteed:** any family that is only represented by a regex in `messages.ja.json` but lacks an emit-route L2 test is only partially evidenced.
- **Not guaranteed:** `"route"` in `messages.ja.json` has runtime meaning. The loader ignores it and uses flat first-match ordering instead.[^route-blind]

## Producer inventory

The current decompiled audit pass grouped the emit surface into six broad producer domains:

| Domain | Main producers | What the family space looks like |
| --- | --- | --- |
| Projectile and melee combat | `MissileWeapon.cs`, `Combat.cs` | High-volume stable combat skeletons with dynamic names, numbers, pronouns, and directions |
| Liquids and environmental damage | `BaseLiquid.cs`, `LiquidVolume.cs`, `LiquidWarmStatic.cs`, `LiquidGoo.cs`, `LiquidOoze.cs`, `LiquidSludge.cs`, `LiquidProteanGunk.cs` | Mix of stable damage/taste/reaction skeletons and builder-heavy procedural output |
| Status and body/effect text | `Bleeding.cs`, `Nosebleed.cs`, `Ill.cs`, `FungalCureQueasy.cs`, `Luminous.cs`, `IrisdualCallow.cs`, `SunderMind.cs`, `FungalInfection.cs` | Stable-looking start/stop/body-part phrasing with dynamic names and occasional dynamic tail terms |
| Interaction/access/item-state | `Door.cs`, `Harvestable.cs`, `MagazineAmmoLoader.cs`, `Tinkering_Mine.cs`, `PowerSwitch.cs`, `RemotePowerSwitch.cs`, `ForceProjector.cs`, `DesalinationPellet.cs`, `CyberneticsButcherableCybernetic.cs`, `Garbage.cs` | Stable English shells mixed with content/tag-driven strings |
| Speech and quoted dialog | `Chat.cs`, `Preacher.cs`, `JoppaZealot.cs`, `SixDayZealot.cs`, `ErosTeleportation.cs` | Stable outer frames, arbitrary inner quote payloads |
| Generic pass-through / template-driven producers | `GameObject.cs`, `Explores.cs`, `SpawnVessel.cs`, `Spawner.cs`, `SplitOnDeath.cs`, `LifeSaver.cs`, `SwapOnUse.cs`, `Interactable.cs`, `Pettable.cs`, `ReplaceBuilder` callers | Data-driven or template-resolved strings whose stability depends on upstream content |

### Representative families

| Family | Representative text | Main producers | Family shape |
| --- | --- | --- | --- |
| Player hit with roll | `You hit (x1) for 1 damage with your レンチ! [18]` | `MissileWeapon.cs` / `Messaging.EmitMessage` | Stable skeleton, dynamic weapon/numbers |
| Incoming hit with roll | `The ワニ hits (x1) for 2 damage with his 噛みつき. [18]` | `MissileWeapon.cs` / `Messaging.EmitMessage` | Stable skeleton, dynamic subject/weapon/numbers |
| Acid damage | `You take 1 damage from the 腐食性ガスの acid!` | `BaseLiquid.cs` via `Physics.EmitMessage` | Stable skeleton after upstream possessive normalization |
| Reach / pass-through failures | `X cannot reach Y.` / `X's attack passes through Y!` | `Combat.cs` | Stable skeleton |
| Bleed / nose / hemorrhage | `One of X wounds stops bleeding.` / `X nose begins bleeding.` / `X brain begins to hemorrhage.` | `Bleeding.cs`, `Nosebleed.cs`, `SunderMind.cs` | Mostly stable shells, some dynamic tail terms |
| Harvest | `You harvest ...` / `There is nothing left to harvest.` | `Harvestable.cs` | Stable skeletons with count/item variants |
| Door/access failures | `You cannot open ...`, `... cannot be closed with ... in the way.` | `Door.cs` | Stable skeletons with dynamic object names and reason tails |
| Say/yell outer frames | `The X yells, '...'.` / `X says, '{{|...}}'.` | `Chat.cs`, zealot/E-Ros producers | Stable outer frame, dynamic quoted payload |
| Tag-driven pass-through | `EmitMessage(Message)` / `EmitMessage(GameText.VariableReplace(Message, ...))` | `GameObject.cs`, `PowerSwitch.cs`, `ForceProjector.cs`, `Explores.cs`, `SpawnVessel.cs` | Upstream-content-owned |
| Builder-heavy procedural output | collection/purification/static-effect StringBuilder messages | `LiquidVolume.cs`, `LiquidWarmStatic.cs`, `Physics.cs` | Procedural, mixed-shape output |

## Coverage ledger

| Skeleton family | Representative source text | Owner producer route | Current repo handling | Evidence | Recommended fix layer | Status |
| --- | --- | --- | --- | --- | --- | --- |
| Structural emit-route wiring | `You are surrounded by baboons.` | emit-message owner patch | End-to-end route proven for both signatures | L2 tests patch both target signatures and assert translated queue output.[^l2-emit] | n/a | **covered** |
| Player hit-with-roll | `You hit (x1) for 1 damage with your レンチ! [18]` | `Messaging.EmitMessage` combat route | Repository pattern proven through emit route | L1 repo-dict translator test, L2 emit-route test, and current runtime evidence all exist. This guarantee depends on the specific hit-with-roll pattern staying ordered ahead of the generic hit patterns.[^l1-repo][^l1-ordering][^l2-hit-acid][^runtime-hit] | emit-pattern-dict | **covered** |
| Acid damage (tested `acid!` forms) | `You take 1 damage from the 腐食性ガスの acid!` / `The ワニ takes 1 damage...` | liquid/environment route through emit | Repository patterns proven through emit route for the currently tested exclamation-terminated shapes | L1 repo-dict translator tests and L2 emit-route tests cover both `acid!` shapes. Current runtime log still contains historical `no pattern` lines, so tests outrank logs here. Period-terminated or bare `acid` variants are not proven by this audit.[^l1-repo][^l2-hit-acid][^runtime-acid][^messages-acid] | emit-pattern-dict + upstream possessive normalization | **covered** |
| Mixed-language incoming hit-with-roll | `The ウォーターヴァイン農家 hits (x2) for 4 damage with his 鉄の蔓刈り斧. [17]` | `Messaging.EmitMessage` combat route | Route and mixed-JP handling are proven, but repo-dict ownership is only indirectly evidenced | L2 isolated-pattern test proves the route; current and previous logs show live emit-route translations; there is no repo-dict L2 assertion for this exact family.[^l2-mixed][^runtime-hit][^runtime-prev-hit] | emit-pattern-dict | **partial** |
| Broad projectile/melee combat families | hit/crit/miss/armor-pen/suppressive-fire/pass-by variants | `MissileWeapon.cs`, `Combat.cs` | Large pattern surface exists, but only a narrow subset is route-proven | Decompiled producers show the family volume; the current proven emit-route test set is much smaller.[^missile][^combat][^l2-emit] | emit-pattern-dict | **partial** |
| Reach / pass-through combat failures | `X cannot reach Y.` / `X's attack passes through Y!` | `Combat.cs` | Patternable, not emit-route-proven | Decompiled producers are stable; no emit-route L2 proof exists for these families today.[^combat][^l2-emit] | emit-pattern-dict | **partial** |
| Bleeding start/stop and nose/hemorrhage phrasing | `One of X wounds stops bleeding.` / `X nose begins bleeding.` / `X brain begins to hemorrhage.` | `Bleeding.cs`, `Nosebleed.cs`, `SunderMind.cs` | Dictionary patterns exist, but emit-route proof is missing | Current dictionary includes multiple bleeding/nosebleed patterns; decompiled producers show stable families; emit-route L2 proof does not extend to them yet.[^messages-bleed][^status][^l2-emit] | emit-pattern-dict | **partial** |
| HP lose / recover exact forms | `You lose N HP.` / `You recover N HP.` | emit-message status/feedback route | Active patterns exist, but emit-route proof is missing | Active emit-message patterns exist in the current dictionary for both forms, but the audit found no emit-route L2 proof for them.[^messages-hp][^l2-emit] | emit-pattern-dict | **partial** |
| Harvest family | `You harvest ...` / `There is nothing left to harvest.` | `Harvestable.cs` | Patternable, not route-proven | Decompiled producer family is stable enough for emit patterns, but the repo does not currently prove the route end-to-end.[^harvest][^l2-emit] | emit-pattern-dict | **partial** |
| Say/yell outer frames | `The X yells, '...'.` / `X says, '{{|...}}'.` | `Chat.cs`, zealot/E-Ros family | Outer frame is partially patternable, payload remains separate | Emit-message yell patterns exist and `Player-prev.log` shows an outer yell frame translating via `GameObjectEmitMessageTranslationPatch`, but the broader say/yell family is not fully proven by tests.[^messages-yell][^runtime-yell][^chat] | emit-pattern-dict | **partial** |
| Bleeding self-damage tick | `You take 1 damage from bleeding.` | emit-message status route | Second-person self-damage shape is currently missing | The repo dictionary includes `^(.+) takes (\\d+) damage from bleeding...` but not the `^You take ...` form; `Player-prev.log` records repeated `no pattern` hits for this exact message under `GameObjectEmitMessageTranslationPatch`.[^messages-bleed][^runtime-bleed] | emit-pattern-dict | **missing** |
| Door open/close/obstruction failures | `You cannot open ...` / `... cannot be closed with ... in the way.` | `Door.cs` | No current proof of coverage; likely uncovered | Decompiled producers are stable, but this family is outside the current proven emit set and not clearly backed by current dictionary/test evidence.[^door] | emit-pattern-dict | **missing** |
| Power/access/tag-driven device messages | `GameText.VariableReplace(KeyObjectAccessMessage, ...)`, `PsychometryAccessMessage`, `AccessFailureMessage` | `PowerSwitch.cs`, `RemotePowerSwitch.cs`, `ForceProjector.cs` | Generic emit route does not guarantee these content-owned strings | Producers forward data/tag strings rather than one narrow stable skeleton, so coverage depends on upstream content rather than an owned emit family.[^passthrough] | producer-side-normalization | **missing** |
| Builder-heavy liquid/environment/procedural text | collection/purification/static-effect StringBuilder output | `LiquidVolume.cs`, `LiquidWarmStatic.cs`, `Physics.cs` | Generic emit route cannot honestly guarantee these shapes | These producers build messages incrementally with StringBuilder or mutable state, so the sink sees many procedural variants.[^dynamic-builders] | mid-pipeline-helper | **missing** |
| Quoted dialog payload inside emit frames | `'Who ventures into the Great Salt Desert...?'` and other conversation-tree lines | conversation/chat content routed through emit producers | Inner quote content is not owned by the generic emit route | The outer emit frame can translate while the inner quoted payload remains English/content-owned, showing this is a different localization pipeline.[^runtime-yell][^chat] | intentionally-unowned | **intentionally-unowned** |
| Generic pass-through blueprint/tag messages | `EmitMessage(Message)` / `EmitMessage(GameText.VariableReplace(Message, ...))` | `GameObject.cs`, `Explores.cs`, `SpawnVessel.cs`, `LifeSaver.cs`, similar producers | Route is wired, but coverage depends on upstream content stability | These producers forward data-driven text instead of one owned skeleton family, so blanket emit coverage would over-claim.[^passthrough] | producer-side-normalization | **intentionally-unowned** |
| `"needs-harmony-patch"` families without an active owner | `... is exhausted`, `... is sealed`, `OUCH! You collide with ...`, `... heals for N hit points.` | pattern file only | Present in the dictionary, but not part of the current emit coverage guarantee | The runtime loader ignores `"route"`, but these rows are explicitly documented as needing their own patch and are absent from the current proven emit-route set, so they must not be counted as live emit coverage.[^route-blind][^messages-needs][^l2-emit] | producer-side-normalization or dedicated Harmony patch | **missing** |

## Current coverage boundary

The repository currently **succeeds** on the emit route in three narrow ways:

1. it owns the route mechanically for both emit entrypoints;
2. it has test-proven coverage for a small set of stable combat/environmental families;
3. it prevents mixed Japanese/English emit messages from being double-processed downstream.

The repository currently only **partially succeeds** when a family:

- has a good-looking regex in `messages.ja.json` but no emit-route L2 proof;
- is observed translating in runtime logs but is only indirectly backed by tests;
- has a stable outer English shell but too many producer-side variants to claim full coverage.

The repository currently **fails or remains uncertain** when the producer:

- emits a dynamic/tag-driven content string rather than a stable skeleton;
- builds the final text procedurally with StringBuilder or multiple conditional fragments;
- relies on a `"needs-harmony-patch"` row that has no active owner route;
- carries arbitrary quoted dialog payload where only the outer emit frame is patternable.

## Known gaps

1. **Projectile combat is under-proven.** It dominates the producer surface, but only a few families are emit-route-tested.
2. **Bleeding has an asymmetry gap.** Start/stop phrasing has patterns, but the second-person self-damage tick currently misses.
3. **Door/access/item-state families are not presently guaranteed.** Many look patternable, but the repo does not prove them.
4. **Data-driven access/tag messages sit outside the reliable emit boundary.** These need producer/content normalization rather than more sink regexes alone.
5. **Builder-heavy liquid/procedural messages want a helper layer.** They are too dynamic to audit honestly as simple pattern coverage.
6. **`needs-harmony-patch` rows remain a hard non-guarantee.** They document desired future ownership, not current tested coverage.

## Next steps

1. Add focused L2 emit-route tests for the highest-volume stable projectile families: incoming hit-with-roll, outgoing hit-with-damage, armor-penetration failures, and “shot goes wild”.
2. Add a second L2 emit-route matrix for status/effect families already represented in the dictionary: bleeding self-damage, bleed start/stop, nosebleed/hemorrhage, harvest, and freezing-effect damage.
3. Split data-driven emit producers into an explicit “content-owned / helper-owned” catalog so future work does not overuse sink-side regexes for blueprint/tag strings.
4. Either remove or separately track dead `"needs-harmony-patch"` rows that currently have no active owner path, so the dictionary stops overstating the live audit surface.

## Validation

Targeted validation for this audit:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'FullyQualifiedName~MessagePatternTranslatorTests|FullyQualifiedName~CombatAndLogMessageQueuePatchTests'
```

Current result: **114 tests, 0 failures, 0 skipped**.

[^emit-patch]: `Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs:17-105`
[^queue-patch]: `Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs:25-50`
[^helpers]: `Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs:215-252`
[^route-blind]: `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:199-217,251-306`
[^l1-repo]: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs:422-449`
[^l1-ordering]: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs:422-430`
[^l2-emit]: `Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs:486-697`
[^l2-mixed]: `Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs:554-589`
[^l2-hit-acid]: `Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs:591-697`
[^runtime-hit]: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log:631-649`
[^runtime-prev-hit]: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log:1399-1427,1776-1807`
[^runtime-acid]: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log:746-801`
[^runtime-bleed]: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log:1815-1834`
[^runtime-yell]: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log:1525-1526`
[^messages-acid]: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:157-165`
[^messages-bleed]: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:707-787`
[^messages-hp]: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:362-368`
[^messages-yell]: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:972-985`
[^messages-needs]: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:187-214,1732-1818`
[^missile]: `~/Dev/coq-decompiled/XRL.World.Parts/MissileWeapon.cs:1678-2291,2586-2590,3476-3476`
[^combat]: `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:686-699`
[^status]: `~/Dev/coq-decompiled/XRL.World.Effects/Bleeding.cs:333-333`; `~/Dev/coq-decompiled/XRL.World.Effects/Nosebleed.cs:93-133`; `~/Dev/coq-decompiled/XRL.World.Parts.Mutation/SunderMind.cs:319-335`
[^harvest]: `~/Dev/coq-decompiled/XRL.World.Parts/Harvestable.cs:316-358`
[^door]: `~/Dev/coq-decompiled/XRL.World.Parts/Door.cs:388-692`
[^chat]: `~/Dev/coq-decompiled/XRL.World.Parts/Chat.cs:182-210`
[^passthrough]: `~/Dev/coq-decompiled/XRL.World/GameObject.cs:14597-14615`; `~/Dev/coq-decompiled/XRL.World.Parts/PowerSwitch.cs:536-571,784-821`; `~/Dev/coq-decompiled/XRL.World.Parts/ForceProjector.cs:634-640`; `~/Dev/coq-decompiled/XRL.World.Parts/Explores.cs:72-72`; `~/Dev/coq-decompiled/XRL.World.Parts/SpawnVessel.cs:66-66`
[^dynamic-builders]: `~/Dev/coq-decompiled/XRL.World.Parts/LiquidVolume.cs:3479-3538`; `~/Dev/coq-decompiled/XRL.Liquids/LiquidWarmStatic.cs:417-421,827-849`; `~/Dev/coq-decompiled/XRL.World.Parts/Physics.cs:3589-3811`
