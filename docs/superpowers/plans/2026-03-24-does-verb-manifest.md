# 2026-03-24 does-verb manifest

Built from `Mods/QudJP/Localization/Dictionaries/messages.ja.json` (`route = does-verb`, 157 patterns), `docs/candidate-inventory.json`, decompiled source under `~/Dev/coq-decompiled/`, plus `omo-explore`/`omo-oracle` review.

Note: I kept the user-requested `emit-message-overlap` class as a table value, but per the oracle review it behaves more like a provenance flag than a true implementation destination.

## Summary counts

- `message-frame-normalizable`: **78**
- `does-composition-specific`: **66**
- `emit-message-overlap`: **3**
- `needs-harmony-patch`: **10**

## Recommended implementation order

1. **Existing message-frame overlaps / no-new-C# quick wins**: move the rows that already map to `DidX`/`MessageFrame` producers into `Mods/QudJP/Localization/MessageFrames/verbs.ja.json` first (`stunned`, `open`, `shies away from you`, `starts to glitch`).
2. **True Does() rows that normalize cleanly**: migrate the large fixed-tail families (`are + status`, `begin flying`, `fall/return to the ground`, `have/share/teach/press/detach/...`) behind a new Does producer seam that reuses `MessageFrameTranslator` tier2/tier3.
3. **EmitMessage overlaps**: reclassify the small set of `emit-message-overlap` rows out of the does bucket and assign them to owner-specific `EmitMessage` producers (`MagazineAmmoLoader`, `MissileWeapon`).
4. **Does-composition-specific families**: group stable but more complex families (`ownership/theft`, `encoded/imprint`, `kick/reflect`, `see no reason`, long social/negation clauses) into family translators.
5. **Harmony-patch leftovers**: patch the remaining non-Does or bespoke producers directly (`Exhausted`, `DeathGate`, popup-only failures, compound multi-verb cases such as `beeps loudly and flashes a warning glyph`).

## Quick wins: patterns that can move to `verbs.ja.json` without new C# code

- `42` — `^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stunned!$` → `XRL.World.Effects/Stun.cs:90`
- `43` — `^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stunned\.$` → `XRL.World.Effects/Stun.cs:90`
- `44` — `^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stunned$` → `XRL.World.Effects/Stun.cs:90`
- `254` — `^(?:The |the |[Aa]n? )?(.+?) (?:is|are) open[.!]?$` → `XRL.World.Parts/ExtradimensionalHunterSummoner.cs:113`
- `360` — `^(?:The |the |[Aa]n? )?(.+?) (?:shies|shy) away from you[.!]?$` → `XRL.World.Parts/BlinkOnDamage.cs:18`
- `478` — `^(?:The |the |[Aa]n? )?(.+?) (?:starts|start) to glitch[.!]?$` → `XRL.Liquids/LiquidWarmStatic.cs:564`

## Full manifest

| idx | pattern (abbrev) | route | producer file:line | seam class | proposed destination |
| ---: | --- | --- | --- | --- | --- |
| 39 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) exhausted!$` | `does-verb` | `XRL.World.Effects/Exhausted.cs:49` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 40 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) exhausted\.$` | `does-verb` | `XRL.World.Effects/Exhausted.cs:49` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 41 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) exhausted$` | `does-verb` | `XRL.World.Effects/Exhausted.cs:49` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 42 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stunned!$` | `does-verb` | `XRL.World.Effects/Stun.cs:90` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 43 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stunned\.$` | `does-verb` | `XRL.World.Effects/Stun.cs:90` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 44 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stunned$` | `does-verb` | `XRL.World.Effects/Stun.cs:90` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 45 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stuck!$` | `does-verb` | `XRL.World/GameObject.cs:15587` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 46 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stuck\.$` | `does-verb` | `XRL.World/GameObject.cs:15587` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 47 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) stuck$` | `does-verb` | `XRL.World/GameObject.cs:15587` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 48 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) sealed!$` | `does-verb` | `XRL.World.Parts/DeathGate.cs:68` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 49 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) sealed\.$` | `does-verb` | `XRL.World.Parts/DeathGate.cs:68` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 50 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) sealed$` | `does-verb` | `XRL.World.Parts/DeathGate.cs:68` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 51 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) jammed!$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:236` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 52 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) jammed\.$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:236` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 53 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) jammed$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:236` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 54 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) empty!$` | `does-verb` | `XRL.World.Parts/FabricateFromSelf.cs:321` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 55 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) empty\.$` | `does-verb` | `XRL.World.Parts/FabricateFromSelf.cs:321` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 56 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) empty$` | `does-verb` | `XRL.World.Parts/FabricateFromSelf.cs:321` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 57 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unresponsive!$` | `does-verb` | `XRL.World.Parts/Windup.cs:94` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 58 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unresponsive\.$` | `does-verb` | `XRL.World.Parts/Windup.cs:94` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 59 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unresponsive$` | `does-verb` | `XRL.World.Parts/Windup.cs:94` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 167 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no room for more (.+?)!$` | `does-verb` | `XRL.World.Parts/BioAmmoLoader.cs:444` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 168 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no room for more (.+?)\.$` | `does-verb` | `XRL.World.Parts/BioAmmoLoader.cs:444` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 169 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no room for more (.+?)$` | `does-verb` | `XRL.World.Parts/BioAmmoLoader.cs:444` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 173 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) nothing to say!$` | `does-verb` | `XRL.World.Parts/ConversationScript.cs:464` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 174 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) nothing to say\.$` | `does-verb` | `XRL.World.Parts/ConversationScript.cs:464` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 175 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) nothing to say$` | `does-verb` | `XRL.World.Parts/ConversationScript.cs:464` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 227 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:harvests\|harvest) ((?:two\|three\|fo…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 228 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:harvests\|harvest) ((?:two\|three\|fo…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 229 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:harvests\|harvest) (?:a \|an )?(.+?) …` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 230 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:harvests\|harvest) (?:a \|an )?(.+?)[…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 247 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) utterly unresponsive[.!]?$` | `does-verb` | `XRL.World.Parts/ConversationScript.cs:281` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 248 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) fully charged!$` | `does-verb` | `XRL.UI/TradeUI.cs:1604` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 249 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) no longer your follower[.!]?$` | `does-verb` | `XRL.World.Capabilities/Wishing.cs:4179` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 250 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) now your follower[.!]?$` | `does-verb` | `XRL.World.Capabilities/Wishing.cs:4198` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 251 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) ripped from your body!$` | `does-verb` | `XRL.World.Effects/Budding.cs:89` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 252 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) pulled toward something[.!]?$` | `does-verb` | `XRL.World.Parts.Mutation/MagneticPulse.cs:288` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 253 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) pulled toward (?:the \|a \|a…` | `does-verb` | `XRL.World.Parts.Mutation/MagneticPulse.cs:288` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 254 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) open[.!]?$` | `does-verb` | `XRL.World.Parts/ExtradimensionalHunterSummoner.cs:113` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 255 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) not bleeding[.!]?$` | `does-verb` | `XRL.World.Parts/Campfire.cs:1643` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 256 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) already fully loaded[.!]?$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:361` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 257 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) already full of (.+?)[.!]?$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:361` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 258 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) already full[.!]?$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:361` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 259 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) turned off[.!]?$` | `does-verb` | `XRL.World.Parts/Campfire.cs:1259` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 260 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) extinguished by (?:the \|a \…` | `does-verb` | `XRL.World.Parts/Campfire.cs:1936` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 261 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) covered in sticky goop!$` | `does-verb` | `XRL.World.Parts/FixitSpray.cs:81` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 262 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) covered in (.+?)[.!]?$` | `does-verb` | `XRL.World.Parts/FixitSpray.cs:81` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 263 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) immune to conventional treat…` | `does-verb` | `XRL.World.Parts/FungalInfection.cs:77` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 264 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) lost in the goop!$` | `does-verb` | `XRL.World.Parts/GelatenousPalmProperties.cs:33` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 265 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) still starting up[.!]?$` | `does-verb` | `XRL.World.Parts/ITeleporter.cs:282` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 266 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) still attuning[.!]?$` | `does-verb` | `XRL.World.Parts/QuickenMind.cs:171` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 267 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) still cooling down[.!]?$` | `does-verb` | `XRL.World.Parts/Teleprojector.cs:238` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 268 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) dead still[.!]?$` | `does-verb` | `XRL.World.Parts/QuickenMind.cs:173` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 269 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) in no need of repairs[.!]?$` | `does-verb` | `XRL.World.Parts/VehicleRepair.cs:127` | `message-frame-normalizable` | verbs.ja.json Tier3 via new Does seam |
| 270 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unable to consume tonics[.!]…` | `does-verb` | `XRL.World.Parts/Tonic.cs:99` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 271 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) too large for you to engulf[…` | `does-verb` | `XRL.World.Parts/Engulfing.cs:481` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 272 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) too tenuously anchored in th…` | `does-verb` | `XRL.World.Parts.Mutation/TemporalFugue.cs:132` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 273 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) too difficult to traverse vi…` | `does-verb` | `XRL.World.Parts/Physics.cs:2593` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 274 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) out of your telekinetic rang…` | `does-verb` | `Qud.API/EquipmentAPI.cs:197` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 275 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unconvinced by your pleas, b…` | `does-verb` | `XRL.World/ProselytizationSifrah.cs:424` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 276 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) unconvinced by your pleas[.!…` | `does-verb` | `XRL.World/ProselytizationSifrah.cs:419` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 277 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) sympathetic, but unable to j…` | `does-verb` | `XRL.World/ProselytizationSifrah.cs:432` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 278 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) offended by your impertinenc…` | `does-verb` | `XRL.World/ProselytizationSifrah.cs:413` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 279 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) encoded with an imprint of t…` | `does-verb` | `XRL.World.Parts/ITeleporter.cs:243` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 280 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) encoded with an imprint (?:o…` | `does-verb` | `XRL.World.Parts/ITeleporter.cs:247` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 281 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) encoded with the imprint of …` | `does-verb` | `XRL.World.Parts/ITeleporter.cs:266` | `does-composition-specific` | ITeleporter popup-owner helper (regex/source drift: vibrational plane) |
| 282 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) engaged in melee combat and …` | `does-verb` | `XRL.UI/TradeUI.cs:370` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 283 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) engaged in hand-to-hand comb…` | `does-verb` | `XRL.World.Parts/ConversationScript.cs:305` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 284 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:is\|are) on fire and (?:is\|are) too …` | `does-verb` | `XRL.UI/TradeUI.cs:376` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 299 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:does\|do) nothing[.!]?$` | `does-verb` | `XRL.UI/TradeUI.cs:441` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 300 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) nothing to trade[.!]?$` | `does-verb` | `XRL.UI/TradeUI.cs:441` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 301 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no limbs that can be amput…` | `does-verb` | `XRL.World.Parts.Skill/Physic_AmputateLimb.cs:106` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 302 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no limbs[.!]?$` | `does-verb` | `XRL.World.Anatomy/BodyPart.cs:2517` | `needs-harmony-patch` | owner-specific AddPlayerMessage/Popup patch (or exact dictionary if fixed leaf) |
| 303 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) (?:already )?boosted immun…` | `does-verb` | `XRL.World.Parts/Campfire.cs:1870` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 304 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) no drain[.!]?$` | `does-verb` | `XRL.World.Effects/LifeDrain.cs:204` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 305 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:has\|have) been hacked[.!]?$` | `does-verb` | `XRL.World.Parts/TemplarPhylactery.cs:142` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 306 | `^(?:The \|the \|[Aa]n? )?(?!You)(.+?) (?:has\|have) no more ammo!$` | `does-verb` | `XRL.World.Parts/MagazineAmmoLoader.cs:353` | `emit-message-overlap` | owner-specific EmitMessage producer patch/family |
| 307 | `^(?:The \|the \|[Aa]n? )?(?!You)(.+?) (?:has\|have) left your party[.!]…` | `does-verb` | `XRL.World.Quests/AscensionSystem.cs:444` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 308 | `^(?:The \|the \|[Aa]n? )?(.+?) merely (?:clicks\|click)[.!]?$` | `does-verb` | `XRL.World.Parts/ElectricalDischargeLoader.cs:294` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 309 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:vibrates\|vibrate) slightly[.!]?$` | `does-verb` | `XRL.World.Parts/Stopsvaalinn.cs:579` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 311 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:seems\|seem) to have taken on new qua…` | `does-verb` | `XRL.UI/TinkeringScreen.cs:717` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 312 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:seems\|seem) utterly impervious to yo…` | `does-verb` | `XRL.World.Parts.Mutation/Beguiling.cs:173` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 313 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shares\|share) a recipe with you[.!]?$` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualBuySecret.cs:51` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 314 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shares\|share) some gossip with you[.…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualBuySecret.cs:56` | `message-frame-normalizable` | verbs.ja.json Tier3 via new Does seam |
| 315 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shares\|share) the location of (.+?)[…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualBuySecret.cs:69` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 316 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shares\|share) an event from the life…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualBuySecret.cs:75` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 317 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shares\|share) the recipe for (.+?)[.…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualCookingRecipe.cs:122` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 318 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:teaches\|teach) you to craft the item…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualTinkeringRecipe.cs:81` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 319 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:teaches\|teach) you to craft (.+?)[.!…` | `does-verb` | `XRL.World.Conversations.Parts/WaterRitualTinkeringRecipe.cs:81` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 320 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:teaches\|teach) you (.+?)[.!]?$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 321 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:returns\|return) to the ground[.!]?$` | `does-verb` | `XRL.World.Anatomy/BodyPart.cs:3583` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 322 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:showers\|shower) sparks everywhere[.!…` | `does-verb` | `XRL.World.Effects/RealityStabilized.cs:802` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 323 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:emits\|emit) a shower of sparks!$` | `does-verb` | `XRL.World.Effects/RealityStabilized.cs:802` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 324 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:emits\|emit) a grinding noise[.!]?$` | `does-verb` | `XRL.World.Parts/ModLiquidCooled.cs:510` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 325 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:loosens\|loosen)\. Your AV decreases …` | `does-verb` | `XRL.World.Parts.Mutation/Carapace.cs:226` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 326 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:loosens\|loosen)[.!]?$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 327 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:says\|say), '(.+?)'[.!]?$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 329 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:resists\|resist) your life drain!$` | `does-verb` | `XRL.World.Effects/LifeDrain.cs:204` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 330 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:resists\|resist) your shield slam[.!]…` | `does-verb` | `XRL.World.Parts.Skill/Shield_Slam.cs:170` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 331 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:starts\|start) to fizz hungrily[.!]?$` | `does-verb` | `XRL.World.Parts/GraveMoss.cs:62` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 332 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:starts\|start) to gleam with an unear…` | `does-verb` | `XRL.World.Parts/PetGloaming.cs:107` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 333 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:presses\|press) your activation panel…` | `does-verb` | `XRL.World.Effects/Asleep.cs:331` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 334 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:presses\|press) (.+?)'s activation pa…` | `does-verb` | `XRL.World.Effects/Asleep.cs:331` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 335 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:detaches\|detach) from you!$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 336 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:detaches\|detach) from (?:the \|a \|a…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 337 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:cleaves\|cleave) through your (.+?)[.…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:32` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 338 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:cleaves\|cleave) through (?:the \|a \…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:32` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 339 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:impales\|impale) (?:himself\|herself\…` | `does-verb` | `XRL.World.Parts/MissileWeapon.cs:2093` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 340 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) at you, but the kick pas…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:32` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 341 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) at you, but you hold you…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:48` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 342 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) you backwards[.!]?$` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:65` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 343 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) (?:the \|a \|an )?(?!you…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:65` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 344 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) at (?:the \|a \|an )?(.+…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:32` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 345 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:kicks\|kick) at (?:the \|a \|an )?(.+…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:48` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 346 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:reflects\|reflect) (\d+) damage back …` | `does-verb` | `XRL.World.Parts/ReflectDamage.cs:58` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 347 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:reflects\|reflect) (\d+) damage back …` | `does-verb` | `XRL.World.Parts/ReflectDamage.cs:58` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 348 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:goes\|go) into sleep mode[.!]?$` | `does-verb` | `XRL.World.Effects/Asleep.cs:142` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 349 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:winces\|wince)[.!]?$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 350 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:ignores\|ignore) (?:the \|a \|an )?(.…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 352 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:locks\|lock) firmly into the socket, …` | `does-verb` | `XRL.World.Parts/CursedCellSocket.cs:58` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 353 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:steps\|step) on (?:the \|a \|an )?(.+…` | `does-verb` | `XRL.World.Effects/RealityStabilized.cs:489` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 354 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:hums\|hum) for a moment, then powers …` | `does-verb` | `XRL.World.Parts/ITeleporter.cs:287` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 355 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:slouches\|slouch) in pacification and…` | `does-verb` | `XRL.World.Parts/NephalProperties.cs:301` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 356 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:beeps\|beep) loudly and (?:flashes\|f…` | `does-verb` | `XRL.World.Parts/NeutronFluxContainment.cs:210` | `needs-harmony-patch` | NeutronFluxContainment-specific Harmony patch/helper |
| 357 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:crumbles\|crumble) into beetles[.!]?$` | `does-verb` | `XRL.World.Parts/PetEitherOr.cs:177` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 358 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:atomizes\|atomize) and (?:recombines\…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 359 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:stops\|stop) gleaming[.!]?$` | `does-verb` | `XRL.World.Parts/PetGloaming.cs:97` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 360 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shies\|shy) away from you[.!]?$` | `does-verb` | `XRL.World.Parts/BlinkOnDamage.cs:18` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 361 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:ceases\|cease) floating near you[.!]?$` | `does-verb` | `XRL.World.Parts/PoweredFloating.cs:105` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 362 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:collapses\|collapse) under the pressu…` | `does-verb` | `XRL.World.Parts/QuantumRippler.cs:68` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 363 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:becomes\|become) magnetized!$` | `does-verb` | `XRL.World.Parts/MagnetizedApplicator.cs:56` | `message-frame-normalizable` | verbs.ja.json Tier2 via new Does seam |
| 364 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:vomits\|vomit) everywhere!$` | `does-verb` | `XRL.World.Effects/RealityStabilized.cs:802` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 365 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:attunes\|attune) to your physiology[.…` | `does-verb` | `XRL.World.Parts/Teleprojector.cs:143` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 367 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:joins\|join) you!$` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 368 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:gifts\|gift) you (?:a \|an \|the )?(.…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 369 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:ponies\|pony) up (\d+) drams? of (.+?…` | `does-verb` | `XRL.UI/TradeUI.cs:1338` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 370 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:shouts\|shout) KLANQ!$` | `does-verb` | `XRL.World.AI.GoalHandlers/PaxKlanqMadness.cs:119` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 372 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:sees\|see) no reason for you to amput…` | `does-verb` | `XRL.World.Parts.Skill/Physic_AmputateLimb.cs:207` | `does-composition-specific` | Amputate-limb family translator/helper |
| 373 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:needs\|need) to be hung up first[.!]?$` | `does-verb` | `XRL.World.Parts/Campfire.cs:1265` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 374 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:needs\|need) water in (?:it\|him\|her…` | `does-verb` | `XRL.UI/TradeUI.cs:1338` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 375 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:was\|were) cracked[.!]?$` | `does-verb` | `XRL.World.Effects/ShatteredArmor.cs:156` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 376 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:tries\|try) to engulf you, but (?:fai…` | `does-verb` | `XRL.World.Parts/Engulfing.cs:506` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 377 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:tries\|try) to engulf (?:the \|a \|an…` | `does-verb` | `XRL.World.Parts/Engulfing.cs:506` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 378 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:tries\|try) to (.+?) (?:a \|an \|the …` | `does-verb` | `XRL.World.Parts/Windup.cs:94` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 379 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:asks\|ask) about (?:his\|her\|its\|th…` | `does-verb` | `XRL.UI/ConversationUI.cs:468` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 435 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:fails\|fail) to penetrate your armor!$` | `does-verb` | `XRL.World.Parts/Combat.cs:1566` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 436 | `^(?:The \|the \|[Aa]n? )?(.+?)(?:'s\|s') suppressive fire (?:locks\|loc…` | `does-verb` | `XRL.World.Parts/MissileWeapon.cs:1840` | `emit-message-overlap` | owner-specific EmitMessage producer patch/family |
| 437 | `^(?:The \|the \|[Aa]n? )?(.+?)(?:'s\|s') flattening fire (?:drops\|drop…` | `does-verb` | `XRL.World.Parts/MissileWeapon.cs:1853` | `emit-message-overlap` | owner-specific EmitMessage producer patch/family |
| 453 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:unlocks\|unlock) (?:the \|a \|an )?(.…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 464 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:rifles\|rifle) through (?:the \|a \|a…` | `does-verb` | `XRL.World.Parts.Skill/Tactics_Kickback.cs:32` | `message-frame-normalizable` | verbs.ja.json Tier1/2/3 via new Does seam |
| 466 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:butchers\|butcher) (?:a \|an \|the )?…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 468 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:rips\|rip) (?:a \|an \|the )?(.+?) ou…` | `does-verb` | `XRL.World.Parts/CyberneticsButcherableCybernetic.cs:182` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 478 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:starts\|start) to glitch[.!]?$` | `does-verb` | `XRL.Liquids/LiquidWarmStatic.cs:564` | `message-frame-normalizable` | verbs.ja.json via existing MessageFrameTranslator |
| 503 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:infiltrates\|infiltrate) (?:the \|a \…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 504 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:eats\|eat) (?:a \|an \|the )?(.+?)[.!…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 505 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:applies\|apply) (?:a \|an \|the )?(.+…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |
| 508 | `^(?:The \|the \|[Aa]n? )?(.+?) (?:deploys\|deploy) (?:a \|an \|the )?(.…` | `does-verb` | `XRL.World.Parts.Mutation/ElectricalGeneration.cs:384` | `does-composition-specific` | dedicated Does family translator/helper at producer seam |

## Notes

- `281` is the clearest regex/source drift case: the current pattern says `present context`, but upstream `ITeleporter` now emits `present vibrational plane` at `XRL.World.Parts/ITeleporter.cs:266`.
- `39-41` (`exhausted`) and `48-50` (`sealed`) do not resolve to active `Does()` producers in the current inventory; they are better treated as owner-specific leaf/popup patches than as a reusable Does seam.
- `306`, `436`, and `437` are best treated as `EmitMessage` provenance overlaps, not genuine Does-owned families.
