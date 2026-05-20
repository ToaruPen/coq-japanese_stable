# Issue 737 HSE Route Closure Audit

Date: 2026-05-19

## Scope

This report is the route-closure ledger for Issue #737. It covers the current
`history_generated_text` lane from the text-construction surface queue and
reconciles it with the previous HistoricStringExpander owner plan.

The scope is the full HSE / HistorySpice generated-text family, not only the
runtime samples currently attached to the issue. The implementation rule remains
unchanged: do not enable a generic `HistoricStringExpander.ExpandString` patch.
Closure must happen at a producer, owner, storage-time, or narrowly proven
display route.

## Evidence Used

Current static evidence was regenerated in the Issue #737 worktree:

```bash
just localization-coverage-map-check
just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable" /tmp/issue737-text-construction-inventory.json 30
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/issue737-text-construction-inventory.json \
  --format json \
  --include valuable \
  --limit 0 > /tmp/issue737-text-construction-policy.json
```

Observed current counts:

- `just localization-coverage-map-check`: passed.
- Text-construction inventory: `17,459` family records.
- Valuable queue entries: `2,641`.
  - `history_generated_text` lane: `78` entries.
  - `action_required`: `0`
  - `covered_by_owner_route`: `76`
  - `partial_coverage`: `0`
  - `runtime_required`: `2`

Runtime evidence comes from the sanitized `Player.log` excerpts attached to
Issue #737 on 2026-05-18T22:03:21Z. The local current `Player.log` in this
worktree session has a different line count and SHA-256, so it is not mixed into
this report.

The issue update at
`https://github.com/ToaruPen/coq-japanese_stable/issues/737#issuecomment-4482734172`
adds a second closure dimension: direct coverage for leaves selected from base
game `Base/HistorySpice.json`. That dimension is tracked separately in
`docs/reports/2026-05-19-issue-737-historyspice-vocabulary-coverage.md` and can
be regenerated with:

```bash
uv run python scripts/historyspice_vocabulary_coverage.py \
  "$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/HistorySpice.json"
```

Current reproduced vocabulary counts:

- `HistorySpice.json` leaf occurrences: `5653`
- unique leaf strings: `3897`
- HSE dictionary direct coverage: `3112 / 3897` (`79.86%`)
- all JSON dictionary direct coverage: `3218 / 3897` (`82.58%`)
- lowest visible focused groups include `spice.gossip.*` (`15.62%`),
  `spice.extradimensional.*` (`82.84%`), `spice.elements.*` (`100.00%`),
  `spice.instancesOf.*` (`98.12%`), and `spice.commonPhrases.*` (`99.30%`).
  `spice.items.*` is now directly covered at `68 / 80` (`85.00%`) after
  closing the `spice.items.blessing.*` component family; the remaining direct
  misses are Latin-like generated suffixes intentionally left as pass-through.
  `spice.gossip.*` is now directly covered at `5 / 32` (`15.62%`) after
  closing the `spice.gossip.leadIns.*` component family through the water-ritual
  buy-secret popup owner route; the remaining `27` direct misses are
  `spice.gossip.twoFaction.*` templates covered by journal observation pattern
  routes rather than direct component keys.
  `spice.elements.*` is now directly covered at `423 / 423` (`100.00%`) after
  closing the `spice.elements.jewels.*`, `spice.elements.chance.*`, and
  `spice.elements.circuitry.*` component families plus the non-conflicting
  `spice.elements.glass.*` HSE leaves and the full `spice.elements.ice.*`
  component family plus the non-conflicting `spice.elements.might.*` HSE
  leaves, the full `spice.elements.salt.*` component family, and the
  `spice.elements.scholarship.*`, `spice.elements.stars.*`,
  `spice.elements.time.*`, and `spice.elements.travel.*` component families;
  this covers the issue-comment
  jewel/ruby/sapphire/emerald/agate examples, the adjacent `shining visage`
  component route risk, previous top missing chance examples such as `lucky`,
  `random`, `chance itself`, `luck`, and `fate`, circuitry examples such as
  `analog`, `digital`, `silicon`, `circuits`, `transistors`, and
  `soldering together the children`, and glass examples such as `clear`,
  `prismatic`, `prisms`, `mirrors`, `transparent visage`, and
  `glass-swept knolls`, plus ice examples such as `frigid`, `frosty`,
  `glacial`, `wintry`, `snow`, `frost`, `rime`,
  `made a solitary trek through a lifeless tundra`, and
  `in the thin and *var* air`, and might examples such as `colossal`,
  `potent`, `commanding`, `zetachrome`, `crysteel`,
  `got into a tavern brawl`, `trekked across a field of skulls and bones`,
  `a great battle was won`, and `looming with *var* presence`, plus salt
  examples such as `salt-spangled`, `briny water`, `pinch of salt`,
  `dram of brine`, `spice root`,
  `trekked through a lifeless salt pan and stumbled on a mysterious monolith`,
  and `upon chewing the *var* leaf`, plus scholarship/stars/time/travel
  examples such as `clockwork tools`, `data disks`, `stardust`, `meteorite`,
  `atomic clock`, `forgotten seconds of one's life`, `foreign`, `altimeter`,
  `astrolabe`, `broke bread with a pilgrim`, and
  `in view of odd and *var* sands`.
  `spice.cooking.terrain.*` is now directly covered at `290 / 290`
  (`100.00%`), `spice.cooking.recipeNames.*` is now directly covered at
  `524 / 531` (`98.68%`), and `spice.cooking.*` is now directly covered at
  `817 / 838` (`97.49%`).
  `spice.commonPhrases.cooking.*` and `spice.commonPhrases.recipes.*` are now
  directly covered as one cooking-adjacent common-phrase unit, including
  process leaves such as `frying`, `steaming`, `grilling`, `pickling`, and
  `fermenting`, and recipe/meal leaves such as `recipes`, `dishes`, `meals`,
  `food`, `cuisine`, `mess`, `victual`, and `servings`.
  `spice.commonPhrases.ruins.*`, `spice.commonPhrases.shire.*`, and
  non-conflicting `spice.commonPhrases.bodyOfWater.*` leaves are now directly
  covered as one landscape common-phrase unit, including `wastes`, `marsh`,
  `moor`, `quagmire`, `shire`, `hearth`, `hold`, `ocean`, `lake`, `lagoon`,
  and `surf`; `drink` remains excluded because its existing UI action ownership
  is not the HSE body-of-water sense.
  Annals status common-phrase families are now directly covered as one
  HSE component unit, including royal succession leaves such as
  `ascended to the throne`, `took the crown`, and `seized the gilded scepter`,
  penalty/status leaves such as `drawn and quartered`, `exiled`, `imprisoned`,
  `launched into orbit`, `ordained`, `foretold`, `glorious`, `lauded`,
  `revered`, and `renowned`, and marriage leaves such as `marriage`, `union`,
  `matrimony`, `nuptial`, and `wedlock`.
  `spice.commonPhrases.bequeathed.*`, `spice.commonPhrases.retrieve.*`, and
  `spice.commonPhrases.restore.*` are now directly covered as one
  transfer/recovery common-phrase unit, including `bequeathed`, `bestowed`,
  `entrusted`, `granted`, `retrieve`, `recover`, `fetch`, `salvage`,
  `procure`, `rehabilitate`, `rescue`, `revive`, `rejuvenate`, and
  `win back`; `get` remains excluded because its existing UI action ownership
  is not the HSE retrieve/common phrase sense.
  `spice.commonPhrases.abhor.*`, `spice.commonPhrases.blessing.*`,
  `spice.commonPhrases.bygone.*`, and `spice.commonPhrases.calmly.*` are now
  directly covered as one sentiment/descriptor common-phrase unit, including
  `abhor`, `detest`, `denounce`, `dishonor`, `favor`, `comfort`, `erstwhile`,
  `once`, `old`, `calmly`, `gently`, and `quietly`; existing HSE keys cover
  `scorn`, `blessing`, `honor`, `boon`, `gift`, and `bygone`.
  `spice.commonPhrases.celebrate.*`, `spice.commonPhrases.celebrated.*`, and
  `spice.commonPhrases.celebration.*` are now directly covered as one
  celebration common-phrase unit, including `celebrate`, `remember`,
  `observe`, `extol`, `rejoice in`, `cried out in joy`,
  `drank themselves into stupors`, `told stories and renewed friendships`,
  `celebration`, `jubilee`, `gaiety`, and `jubilation`; existing HSE coverage
  keeps `joy` as the owner for that shared leaf.
  `spice.commonPhrases.challenge.*`, `spice.commonPhrases.chastisement.*`, and
  `spice.commonPhrases.coalition.*` are now directly covered as one
  conflict/coalition common-phrase unit, including `challenge`, `provoke`,
  `aggrieve`, `chastisement`, `chastening`, `rebuking`, `coalition`,
  `alliance`, `confederacy`, `league`, `conspiracy`, and `federation`;
  existing HSE coverage keeps `<^.curse.!random>`, `union`, and `party` as
  owners for those shared leaves.
  `spice.commonPhrases.composed.*`, `spice.commonPhrases.congregated.*`, and
  `spice.commonPhrases.conquered.*` are now directly covered as one
  creation/gathering/conquest common-phrase unit, including `composed`,
  `invented`, `fashioned`, `imagined`, `congregated`, `gathered`,
  `flocked together`, `massed`, `conquered`, and `subjugated`; existing HSE
  coverage keeps `consecrated`, `annexed`, and `discovered` as owners for
  those shared leaves.
  `spice.commonPhrases.corrupt.*` and `spice.commonPhrases.crowned.*` are now
  directly covered as one corruption/coronation common-phrase unit, including
  `corrupt`, `fraudulent`, `venal`, `debauched`, `base`, `perfidious`,
  `knavish`, `treacherous`, `crowned`, `declared`, and `proclaimed`.
  `spice.commonPhrases.defied.*` and `spice.commonPhrases.demonstrate.*` are
  now directly covered as one defiance/demonstration common-phrase unit,
  including `flouted`, `mocked`, `spurned`, `thwarted`, `scorned`, `eluded`,
  `demonstrate`, `exhibit`, `prove`, `display`, and `evince`; existing HSE
  coverage keeps `defied` as the owner for that shared leaf.
  `spice.commonPhrases.depravity.*` and `spice.commonPhrases.despots.*` are
  now directly covered as one depravity/despot common-phrase unit, including
  `depravity`, `degeneracy`, `wickedness`, `perversion`, `despots`, `lords`,
  `aristocrats`, `leaders`, `magistrates`, and `shepherds`; existing HSE
  coverage keeps `decay` as the owner for that shared leaf.
  `spice.commonPhrases.door.*`, `spice.commonPhrases.embraced.*`, and
  `spice.commonPhrases.emerged.*` are now directly covered as one
  door/emergence common-phrase unit, including `door`, `portal`, `entryway`,
  `egress`, `gate`, `hatch`, `embraced`, `forgotten`, `materialized`, and
  `sprang forth`; existing HSE coverage keeps `accepted`, `adopted`, and
  `emerged` as owners for those shared leaves.
  `spice.commonPhrases.enacting.*`, `spice.commonPhrases.entropist.*`,
  `spice.commonPhrases.entwined.*`, and `spice.commonPhrases.epic.*` are now
  directly covered as one enacting/entwined/epic common-phrase unit, including
  `enacting`, `setting into motion`, `putting into place`, `entropist`,
  `entwined`, `braided`, `embracing`, `beautiful`, and `unsurpassed`;
  existing HSE coverage keeps `bewitching` and `sublime` as owners for those
  shared leaves.
  `spice.commonPhrases.eternally.*`, `spice.commonPhrases.family.*`,
  `spice.commonPhrases.fate.*`, `spice.commonPhrases.festival.*`, and
  `spice.commonPhrases.find.*` are now directly covered as one
  time/family/festival/find common-phrase unit, including `eternally`,
  `forever`, `always`, `children`, `progeny`, `fortune`, `festival`, `feast`,
  `holiday`, `locate`, and `pinpoint`; existing HSE coverage keeps `family`,
  `kith`, `clan`, `brood`, `kinfolk`, `folk`, `tribe`, `fate`, `carnival`,
  `jubilee`, and `find` as owners for those shared leaves.
  `spice.commonPhrases.finesse.*` and `spice.commonPhrases.foes.*` are now
  directly covered as one finesse/foes common-phrase unit, including
  `finesse`, `agility`, `skill`, `artfulness`, `artistry`, `dexterity`,
  `prowess`, `deftness`, `foes`, and `enemies`.
  `spice.commonPhrases.folks.*`, `spice.commonPhrases.forever.*`,
  `spice.commonPhrases.fromThenOn.*`, and `spice.commonPhrases.ghost.*` are
  now directly covered as one people/time/ghost-title common-phrase unit,
  including `folks`, `beings`, `for all time`, `now and forever`,
  `in perpetuity`, `from then on`, `from that day forth`, `devil`, and
  `wraith`; existing HSE coverage keeps `people`, `forever`,
  `for the rest of <entity.name>'s life`, `ghost`, `shade`, and `spectre` as
  owners for those shared leaves.
  `spice.commonPhrases.gift.*`, `spice.commonPhrases.grave.*`, and
  `spice.commonPhrases.greatly.*` are now directly covered as one
  gift/grave/greatly common-phrase unit, including `grant`, `dower`, `deep`,
  `grave`, `joyous`, `heartfelt`, `greatly`, `mightily`, `much`, and
  `immensely`; existing HSE coverage keeps `gift`, `favor`, and `boon` as
  owners for those shared leaves.
  `spice.commonPhrases.group.*`, `spice.commonPhrases.hark.*`, and
  `spice.commonPhrases.harm.*` are now directly covered as one
  group/hark/harm common-phrase unit, including `group`, `organization`,
  `group of friends`, `group of lovers`, `hark`, `attend`, `attention`,
  `pay heed`, `harm`, `abuse`, `undermine`, and `wrong`; existing HSE coverage
  keeps `sect`, `party`, `cabal`, and `<^.adventurer.!random>` as owners for
  those shared leaves.
  `spice.commonPhrases.hearth.*`, `spice.commonPhrases.helping.*`, and
  `spice.commonPhrases.historic.*` are now directly covered as one
  hearth/helping/historic common-phrase unit, including `hearthstone`, `haunt`,
  `seat`, `roost`, `helping`, `assisting`, `aiding`, `historic`,
  `influential`, `illustrious`, and `imperial`; existing HSE coverage keeps
  `hearth`, `home`, and `celebrated` as owners for those shared leaves.
  `spice.commonPhrases.hold.*`, `spice.commonPhrases.honoring.*`, and
  `spice.commonPhrases.horror.*` are now directly covered as one
  hold/honoring/horror common-phrase unit, including `hide`, `contain`,
  `honoring`, `defending`, `loving`, `horror`, `abomination`, `shame`,
  `anathema`, and `atrocity`; existing HSE coverage keeps `hold` as the owner
  for that shared leaf.
  `spice.commonPhrases.humble.*`, `spice.commonPhrases.hunter.*`, and
  `spice.commonPhrases.importance.*` are now directly covered as one
  humble/hunter/importance common-phrase unit, including `humble`, `quiet`,
  `modest`, `gentle`, `stalker`, `assassin`, `importance`, `value`, and
  `significance`; existing HSE coverage keeps `<^.sanctity.!random>` as the
  owner for that shared symbolic leaf.
  `spice.commonPhrases.inHonorOf.*`, `spice.commonPhrases.inauguration.*`,
  and `spice.commonPhrases.inspired.*` are now directly covered as one
  in-honor-of/inauguration/inspired common-phrase unit, including
  `to show their appreciation`, `inauguration`, `opening`, `founding`,
  `inspired`, `roused`, and `stirred`; existing HSE coverage keeps the
  placeholder-bearing in-honor-of variants as owners for those shared leaves.
  `spice.commonPhrases.interesting.*` and
  `spice.commonPhrases.intrepid.*` are now directly covered as one
  interesting/intrepid common-phrase unit, including `interesting`,
  `intriguing`, `delightful`, `fascinating`, `intrepid`, `courageous`,
  `gallant`, `lionhearted`, and `valiant`; existing HSE coverage keeps `bold`
  as the owner for that shared leaf.
  `spice.commonPhrases.itWasDiscovered.*` and the adjective alternatives in
  `spice.commonPhrases.kind.*` are now directly covered as one discovery/kind
  common-phrase unit, including `it was discovered`, `the people learned`,
  `the people of Qud learned`, `generous`, `gracious`, `courteous`, `lenient`,
  and `cordial`; the shared literal `kind` is left to route-context handling
  because `spice.commonPhrases.kin.*` also emits it as a noun.
  `spice.commonPhrases.larvae.*` and `spice.commonPhrases.laws.*` are now
  directly covered as one larvae/laws common-phrase unit, including `fry`,
  `maggots`, `laws`, `doctrine`, `statutes`, `edicts`, `ordinances`, and
  `injunctions`; existing HSE coverage keeps `larvae`, `grub`, and `worms`
  as owners for those shared lifecycle leaves.
  `spice.commonPhrases.learned.*` and `spice.commonPhrases.learnedOf.*` are
  now directly covered as one learned/learned-of common-phrase unit, including
  `learned of`, `learned about`, `found out about`, `came upon`, `learned`,
  `determined`, `found out`, and `ascertained`; existing HSE coverage keeps
  `discovered` and `gathered` as owners for those shared discovery leaves.
  `spice.commonPhrases.learning.*`, `spice.commonPhrases.listen.*`, and
  `spice.commonPhrases.liberated.*` are now directly covered as learning/listen
  and liberated common-phrase units, including `learning`, `discovering`,
  `hearing of`, `becoming acquainted with`, `listen`, `hear me`,
  `mind what I say`, `liberated`, and `freed`; existing HSE coverage keeps
  `hark` and `rescued` as owners for those shared leaves.
  `spice.commonPhrases.lostInTavern.*` and `spice.commonPhrases.lost.*` are
  now directly covered as one lost/lost-in-tavern common-phrase unit, including
  `in a game of dice`, `to a local thief`, `to a local pickpocket`,
  `in a foolhardy bet`, `lost`, `vanished`, `moldered`, `desolate`, and
  `extinct`; `lost` is intentionally also owned in the HSE scoped dictionary so
  the HistorySpice route does not depend on a non-HSE display-name dictionary.
  `spice.commonPhrases.love.*` and `spice.commonPhrases.lovers.*` are now
  directly covered as one love/lovers common-phrase unit, including `revere`,
  `cherish`, `venerate`, `esteem`, `treasure`, `pay homage to`, and `lovers`;
  existing HSE coverage keeps `love`, `honor`, `worship`, and
  `<^.betrothed.!random>` as owners for those shared leaves.
  `spice.commonPhrases.luckily.*` and `spice.commonPhrases.marvel.*` are now
  directly covered as one luckily/marvel common-phrase unit, including
  `luckily`, `by the grace of fate`, `fortuitously`, `by chance`, `marvel`,
  `stare`, `be awed`, and `stand in awe`.
  `spice.commonPhrases.might.*`, `spice.commonPhrases.misuse.*`, and
  `spice.commonPhrases.morality.*` are now directly covered as one
  might/misuse/morality common-phrase unit, including `might`, `power`,
  `misuse`, `morality`, `decency`, `chastity`, `godliness`, `principles`, and
  `the moral code`; `might` is intentionally also owned in the HSE scoped
  dictionary so the HistorySpice route does not depend on a broad world-mods
  dictionary.
  `spice.commonPhrases.mug.*` and `spice.commonPhrases.noble.*` are now
  directly covered as one mug/noble common-phrase unit, including `mug`,
  `stein`, `ewer`, `jug`, `cup`, `noble`, `virtuous`, and `honorable`;
  existing HSE coverage keeps `skin`, `bladder`, `canteen`, and `glass` as
  owners for those shared leaves.
  `spice.commonPhrases.object.*`, `spice.commonPhrases.observe.*`,
  `spice.commonPhrases.occasion.*`, and `spice.commonPhrases.odious.*` are now
  directly covered as object/observe/occasion and odious common-phrase units,
  including `object`, `mark`, `regard`, `behold`, `read`, `occasion`, `odious`,
  `wicked`, `devilish`, `villainous`, `fiendish`, and `nefarious`; existing
  HSE coverage keeps `observe`, `ceremony`, `affair`, `union`, `abominable`,
  `degenerate`, and `foul` as owners for those shared leaves.
  `spice.commonPhrases.onlooker.*` and `spice.commonPhrases.picks.*` are now
  directly covered as one onlooker/picks common-phrase unit, including
  `watcher`, `beholder`, `onlooker`, `witness`, `picks`, `culls`, `winnows`,
  and `plucks`.
  `spice.commonPhrases.pigfarm.*`, `spice.commonPhrases.plague.*`, and
  `spice.commonPhrases.plan.*` are now directly covered as one
  pigfarm/plague/plan common-phrase unit, including `ranch`, `pasture`,
  `plague`, `plan`, `scheme`, `idea`, `stratagem`, `ploy`, and `ruse`;
  existing HSE coverage keeps `<^.farm.!random>`, `curse`, and `vex` as owners
  for those shared leaves.
  `spice.commonPhrases.practice.*`, `spice.commonPhrases.pretender.*`, and
  `spice.commonPhrases.prized.*` are now directly covered as one
  practice/pretender/prized common-phrase unit, including `practice`, `art`,
  `pretender`, `prized`, `precious`, `cherished`, and `treasured`; existing HSE
  coverage keeps `claimant` and `aspirant` as owners for those shared leaves.
  `spice.commonPhrases.profanity.*`, `spice.commonPhrases.prohibited.*`,
  `spice.commonPhrases.protect.*`, and `spice.commonPhrases.protection.*` are
  now directly covered as profanity/prohibited and protect/protection
  common-phrase units, including `profanity`, `obscenity`, `blasphemy`,
  `impiety`, `irreverence`, `banned`, `prohibited`, `outlawed`, `defend`,
  `safeguard`, `keep safe`, `preserve`, `protection`, `support`,
  `encouragement`, `furtherance`, and `patronage`; existing HSE coverage keeps
  `protect` as the owner for that shared leaf.
  `spice.commonPhrases.puff.*` and `spice.commonPhrases.ravaged.*` are now
  directly covered as one sensory/ravaging-action common-phrase unit, including
  `puff`, `wisp`, `noseful`, `sniff`, `ravaged`, `rampaged through`,
  `pillaged`, `plundered`, `wreaked havoc on`, and `laid waste to`.
  `spice.commonPhrases.remember.*`, `spice.commonPhrases.rife.*`, and
  `spice.commonPhrases.rituals.*` are now directly covered as memory,
  prevalence, and ritual/cultural-practice common-phrase units, including
  `recall`, `rife`, `rampant`, `prevalent`, `reigning`, `widespread`, `rites`,
  `rites of passage`, `customs`, `practices`, and `ceremonies`; existing HSE
  coverage keeps `remember` and `rituals` as owners for those shared leaves.
  `spice.commonPhrases.rogue.*`, `spice.commonPhrases.sacked.*`,
  `spice.commonPhrases.savior.*`, `spice.commonPhrases.scourge.*`,
  `spice.commonPhrases.slaughtered.*`, and `spice.commonPhrases.vanquished.*`
  are now directly covered as one conflict/rescue/victory common-phrase unit,
  including `rogue`, `bandit`, `trickster`, `criminal`, `sacked`, `destroyed`,
  `burned down`, `savior`, `liberator`, `defender`, `scourge`, `terror`,
  `pest`, `sorrow`, `slaughtered`, `persecuted`, `vanquished`, `routed`,
  `subdued`, and `triumphed over`; existing HSE coverage keeps `nefarious`,
  `pillaged`, `ravaged`, `bane`, `woe`, and `conquered` as owners for those
  shared leaves.
  `spice.commonPhrases.strange.*`, `spice.commonPhrases.suspiciously.*`,
  `spice.commonPhrases.tamed.*`, `spice.commonPhrases.thankful.*`,
  `spice.commonPhrases.warning.*`, `spice.commonPhrases.wild.*`,
  `spice.commonPhrases.woe.*`, and `spice.commonPhrases.wonder.*` are now
  directly covered as one descriptor/emotion/warning common-phrase unit,
  including `weird`, `curious`, `rare`, `marvelous`, `uncanny`,
  `suspiciously`, `tentatively`, `warily`, `carefully`, `anxiously`, `tamed`,
  `pacified`, `gentled`, `brought to heel`, `thankful`, `grateful`,
  `much obliged`, `warning`, `lesson`, `admonition`, `example`,
  `forewarning`, `untamed`, `feral`, `savage`, `barbaric`, `misery`, `gloom`,
  `anguish`, `agony`, and `astonishment`; existing HSE coverage keeps
  `strange`, `subdued`, `wild`, `woe`, `sorrow`, `shame`, `torment`,
  `wonder`, `awe`, and `reverence` as owners for those shared leaves.
  `spice.commonPhrases.secluded.*`, `spice.commonPhrases.services.*`,
  `spice.commonPhrases.society.*`, `spice.commonPhrases.spouse.*`, and
  `spice.commonPhrases.task.*` are now directly covered as one
  civic/social/work common-phrase unit, including `secluded`, `small`,
  `remote`, `services`, `service`, `assistance`, `work`, `labor`, `culture`,
  `civic life`, `social order`, `spouse`, `partner`, `mate`, `task`, `errand`,
  `job`, `project`, `charge`, and `stint`; existing HSE coverage keeps
  `quiet`, `society`, and `companion` as owners for those shared leaves.
  `spice.commonPhrases.treasures.*`, `spice.commonPhrases.treating.*`, and
  `spice.commonPhrases.supports.*` are now directly covered as one
  value/diplomacy/support common-phrase unit, including `treasures`,
  `secrets`, `riches`, `pearls`, `mysteries`, `striking a deal`,
  `conferring`, `supports`, `protects`, `promotes`, `champions`, and `cheers`;
  existing HSE coverage keeps `treating` as the owner for that shared leaf.
  `spice.commonPhrases.shortHearth.*`, `spice.commonPhrases.starapplefarm.*`,
  `spice.commonPhrases.yard.*`, and `spice.commonPhrases.yearsAgo.*` are now
  directly covered as one place/time common-phrase unit, including `holme`,
  `orchard`, `grove`, `yard`, `fold`, `quadrangle`, `quad`, `years ago`,
  `beyond the gulf of time`, `back when the musa was perpetually ripe`,
  `early in the days after the reign of Resheph`, and `long ago`; existing HSE
  coverage keeps `home`, `<^.farm.!random>`, and `square` as owners for those
  shared leaves, while placeholder-bearing years-ago templates remain
  route/template owned rather than exact dictionary leaves.
  `spice.commonPhrases.saw.*`, `spice.commonPhrases.scion.*`,
  `spice.commonPhrases.stamped.*`, and `spice.commonPhrases.ways.*` are now
  directly covered as one discovery/lineage/marking/customs common-phrase unit,
  including `saw`, `seed`, `stamped`, `painted`, `engraved`, `embossed`,
  `etched`, `ways`, `habits`, `priorities`, and `manner`; existing HSE
  coverage keeps `found`, `discovered`, `child`, `lamb`, `heir`, `scion`,
  `kin`, `babe`, `heiress`, `progeny`, `sprout`, `adorned`, `carved`,
  `customs`, and `culture` as owners for those shared leaves. The
  `painted` / `engraved` duplicate baseline intentionally separates HSE prose
  from UI display-name color-tag output.
  `spice.commonPhrases.settle.*`, `spice.commonPhrases.settled.*`,
  `spice.commonPhrases.traveling.*`, `spice.commonPhrases.trek.*`,
  `spice.commonPhrases.trekked.*`, `spice.commonPhrases.whileTraveling.*`,
  `spice.commonPhrases.visited.*`, `spice.commonPhrases.stretches.*`, and
  `spice.commonPhrases.voice.*` are now directly covered as one
  settlement/travel/voice common-phrase unit, including `settle`, `visit`,
  `dwell`, `lodge`, `take root`, `take up residence`, `settled`, `roosted`,
  `lodged`, `resided`, `took up residence`, `visiting`, `roaming`, `trek`,
  `journey`, `travel`, `go`, `hike`, `wander`, `trekked`, `journeyed`,
  `traveled`, `voyaged`, `while traveling`, `during a trek`,
  `during an expedition`, `while on an expedition`, `while on a trek`,
  `visited`, `stretches`, `voice`, `utter`, `say`, and `sound`; existing HSE
  coverage keeps `traveling`, `as <entity.subjectPronoun> rode`, and
  `<^.trekked.!random> to` as owners for those shared leaves.
  The remaining `spice.commonPhrases.*` direct misses are the route-local
  function-word leaves `in`, `throughout`, `throughout the entirety of`,
  `through`, and `around`, plus the placeholder template `*var* twin`; these
  are route grammar/template reconstruction concerns rather than broad exact
  dictionary leaves. The visible `allThroughout` RampageRegion/Bey Lah frames
  and `sultanClone` faked-death frames are now covered by route-specific
  annals patterns and capture reconstruction, leaving no broad leaf promotion
  needed for those connector/template strings.
  `spice.instancesOf.abdicate.*`, `spice.instancesOf.abdicated.*`,
  `spice.instancesOf.aboveAllElse.*`,
  `spice.instancesOf.afterTumultuousYears.*`, and
  `spice.instancesOf.approach.*` are now directly covered as one
  abdication/protocol/approach instance component unit, including
  `abdicate the throne`, `take an extended sabbatical`, `step down`,
  `abdicated the throne`, `died under mysterious circumstances`,
  `was assassinated`, `above all else`, `come what may`,
  `as long as you are respectful`, `per our custom`,
  `after several tumultuous years`, `approach`, `meet`, `begin`, `match`,
  `come at`, `surround`, `threaten`, and `accost`. This raises
  `spice.instancesOf.*` direct coverage to `192 / 479` (`40.08%`) while
  leaving remaining instance leaves grouped for later route/family passes.
  `spice.instancesOf.bless.*`, `spice.instancesOf.bodyPartMaimed.*`, and
  `spice.instancesOf.brokeFaithWith.*` are now directly covered as one
  blessing/maiming/faith-breaking instance component unit, including
  `bless`, `thank`, `exalt`, `give thanks for`, `praise`, `honor`,
  `maimed`, `dismembered`, `crushed`, `flattened`, `severed`, `punctured`,
  `broke faith with`, `betrayed`, `committed treason against`,
  `broke trust with`, and `deceived`. Existing HSE dictionary-set coverage
  keeps `honor`, `dismembered`, and `severed` as shared-leaf owners rather
  than duplicating those keys. This raises `spice.instancesOf.*` direct
  coverage to `206 / 479` (`43.01%`).
  `spice.instancesOf.chantedOrSang.*`, the fixed leaf in
  `spice.instancesOf.comeClose.*`, `spice.instancesOf.commonFolk.*`,
  `spice.instancesOf.criedOut.*`, and `spice.instancesOf.curse.*` are now
  directly covered as one speech/common-folk/curse instance component unit,
  including `chanted`, `sang`, `shouted`, `crooned`, `yodeled`, `roared`,
  `come, close!`, `commoners`, `common folk`, `plebians`, `rabble`, `herd`,
  `masses`, `cried out`, `bellowed`, `curse`, `a blight upon`, and
  `a curse upon`. Greeting/adjective/noun templates inside `comeClose.*`
  remain route/template-owned rather than exact dictionary leaves. This raises
  `spice.instancesOf.*` direct coverage to `221 / 479` (`46.14%`).
  `spice.instancesOf.deadlyLiquids.*` is now directly covered as one deadly
  liquid component unit, including `lava`, `acid`, `neutron flux`,
  `black ooze`, `green goo`, `brown sludge`, `asphalt`, and `molten wax`.
  Existing HSE dictionary-set coverage keeps `lava`, `acid`, `neutron flux`,
  and `asphalt` as shared-leaf owners rather than duplicating those keys. This
  raises `spice.instancesOf.*` direct coverage to `225 / 479` (`46.97%`).
  `spice.instancesOf.dearOnes.*` and `spice.instancesOf.desire.*` are now
  directly covered as one dear-ones/desire instance component unit, including
  `friends`, `lovers`, `children`, `cohorts`, `comrades`, `desire`, `want`,
  `need`, `covet`, `require`, `yearn for`, `am in need of`, `have use for`,
  `must have`, and `must get a hold of`. Existing HSE dictionary-set coverage
  keeps `lovers` and `children` as shared-leaf owners rather than duplicating
  those keys. This raises `spice.instancesOf.*` direct coverage to
  `238 / 479` (`49.69%`).
  `spice.instancesOf.disparaged.*`, the fixed leaf in
  `spice.instancesOf.fakedDeath.*`, `spice.instancesOf.dwellOrWork.*`,
  `spice.instancesOf.fate.*`, and `spice.instancesOf.flockedTo.*` are now
  directly covered as one social-action/fate/movement instance component unit,
  including `thrown off a cliff`, `humiliated at a banquet`,
  `with the clever use of a lifelike hologram`, `dwell`, `live`, `work`,
  `toil`, `labor`, `fate`, `chance`, `the way the musa peels`, `misfortune`,
  `flocked to`, `gathered in droves at`, `amassed at`, and
  `herded in droves to`; placeholder-bearing disparagement and faked-death
  templates remain route/template-owned. This raises `spice.instancesOf.*`
  direct coverage to `250 / 479` (`52.19%`).
  `spice.instancesOf.forAllTime.*` and `spice.instancesOf.forestPlaces.*` are
  now directly covered as one time/forest-place instance component unit,
  including `for all time`, `for all eternity`, `again`, `ever again`, `glen`,
  `glade`, `dell`, `dale`, `vale`, `gorge`, `meadow`, `bosk`, `grove`,
  `wood`, `weep`, `weald`, and `root`. This raises `spice.instancesOf.*`
  direct coverage to `262 / 479` (`54.70%`).
  `spice.instancesOf.groupMurdered_By.*`,
  the fixed leaf in `spice.instancesOf.groupMurdered_NotBy.*`,
  `spice.instancesOf.have.*`, `spice.instancesOf.haveYouHeardOf.*`,
  `spice.instancesOf.holdDear.*`, and
  `spice.instancesOf.ifYouWouldDoTheSame.*` are now directly covered as one
  punishment/possession/appeal instance component unit, including
  `sacrificed`, `burned at the stake`, `buried alive`,
  `drawn and quartered`, `mummified`, `beheaded`,
  `killed after cooking a rancid meal for`, `have`, `acquire`,
  `get a hold of`, `obtain`, `procure`, `snag`, `have you heard of`,
  `are you aware of`, `are you acquainted with`,
  `have you been introduced to`, `hold dear`, `cherish`,
  `value so highly`, `if you would do the same`,
  `if you would do it too`, and `if you would do it yourself`; the remaining
  group-murder placeholder template and route-local furniture prepositions are
  not promoted as exact dictionary leaves. This raises `spice.instancesOf.*`
  direct coverage to `279 / 479` (`58.25%`).
  `spice.instancesOf.illness.*`, `spice.instancesOf.justice.*`,
  `spice.instancesOf.kindred.*`, and `spice.instancesOf.leansIn.*` are now
  directly covered as one illness/justice/kinship/proximity instance component
  unit, including `depression`, `gout`, `consumption`, `brain rust`,
  `ironshank`, `scurvy`, `brain mites`, `leprosy`, `existential despair`,
  `justice`, `love`, `truth`, `equality`, `parity`, `faith`, `grace`,
  `virtue`, `honor`, `benefience`, `kindred`, `sibling`, `kinsmen`,
  `kinswomen`, `kinsfolk`, `sib`, `leans in`, `comes close`, `leans forward`,
  and `whispers`; existing HSE coverage keeps `brother` and `sister` as
  shared-leaf owners. This raises `spice.instancesOf.*` direct coverage to
  `301 / 479` (`62.84%`).
  `spice.instancesOf.letItAlwaysBeSo.*`, `spice.instancesOf.lifesave.*`,
  `spice.instancesOf.lostFaithIn.*`, `spice.instancesOf.murdered.*`,
  `spice.instancesOf.ofCourse.*`, and `spice.instancesOf.overTime.*` are now
  directly covered as one affirmation/lifesave/murder/time instance component
  unit, including `let it always be so`, `may that never change`,
  `with cybernetic surgery`, `with astral projection`,
  `by a pact with highly entropic beings`, `lost faith in`,
  `lost interest in`, `renounced`, `rejected`, `stabbed to death`,
  `gunned down`, `poisoned`, `pushed off a cliff`,
  `murdered under mysterious circumstances`, `assassinated after disparaging`,
  `over time`, `over the years`, `as the years passed`, `eventually`, and
  `in time`; existing HSE coverage keeps shared leaves such as `shanked`,
  `eaten alive`, `shot`, `ate alive`, `assassinated`, `of course`,
  `naturally`, `undoubtedly`, `obviously`, and `indeed`. This raises
  `spice.instancesOf.*` direct coverage to `324 / 479` (`67.64%`).
  `spice.instancesOf.religion*`, `spice.instancesOf.profanity.*`,
  `spice.instancesOf.recently.*`, and `spice.instancesOf.reemerged.*` are now
  directly covered as one religion/profanity/recency/reemergence instance
  component unit, including `priest`, `heretic`, `pontiff`, `monk`, `cleric`,
  `pagan`, `anchorite`, `priestess`, `apostate`, `pious`, `devout`,
  `heretical`, `godly`, `moral`, `saintly`, `schismatic`, `dissident`,
  `godliness`, `god`, `divinity`, `piety`, `Gjaus`, `holiness`, `profanity`,
  `cruelty`, `blasphemy`, `filth`, `foulness`, `vulgarity`, `recently`,
  `just a while ago`, `a short while ago`, `the other day`, `reemerged`,
  `appeared anew`, `emerged anew`, `reappeared`, `celebrated`, `rejoiced at`,
  and `reveled at`; existing HSE coverage keeps `mud`, `pig`, `snout`,
  `virtue`, and `faith` as shared-leaf owners. This raises
  `spice.instancesOf.*` direct coverage to `359 / 479` (`74.95%`).
  `spice.instancesOf.reward.*`, `spice.instancesOf.royal.*`,
  `spice.instancesOf.seceded.*`, `spice.instancesOf.speakTo.*`,
  `spice.instancesOf.stepDown.*`, and adjacent authored place/apple leaves are
  now directly covered as one reward/royal/secession/place/stepdown instance
  component unit, including `reward`, `pay you for`, `compensate you for`,
  `divine`, `imperial`, `sovereign`, `holy`, `deific`, `kingly`, `queenly`,
  `sand`, `salt`, `turf`, `loam`, `ground`, `soil`, `seceded`, `separated`,
  `segregated themselves`, `insulated themselves`, `sequestered themselves`,
  `left`, `speak to`, `talk to`, `find`, `vault`, `crypt`, `temple`, `shrine`,
  `sanctum`, `reactor`, `core`, `fruit`, `apple`, `red`, `sweet`,
  `step down`, `abdicate`, and `surrender power`; existing HSE coverage keeps
  `adopted` as a shared-leaf owner. This raises `spice.instancesOf.*` direct
  coverage to `376 / 479` (`78.50%`).
  `spice.instancesOf.tar.*`, `spice.instancesOf.thank.*`,
  `spice.instancesOf.tinyBodyPart.*`, `spice.instancesOf.trembleBefore.*`,
  `spice.instancesOf.tutor.*`, `spice.instancesOf.tutorAdj.*`, and
  `spice.instancesOf.twoToTen.*` are now directly covered as one
  tar/thank/body/tremble/tutor/number instance component unit, including
  `resin`, `I'm grateful to`, `I bow down to`, `I smile on`, `kiss`, `nail`,
  `finger`, `fear`, `quiver at`, `tremble before`, `dread`, `shun`,
  `be in awe of`, `tutor`, `sophist`, `mentor`, `lecturer`, `augur`, `sage`,
  `iconoclast`, `wise`, `erudite`, `controversial`, `cerebral`, `profound`,
  `methodical`, `two`, `four`, `five`, `six`, `seven`, `eight`, `nine`, and
  `ten`; existing HSE coverage keeps shared leaves such as `tar`, `asphalt`,
  `goop`, `glue`, `thank`, `bless`, `praise`, `hair`, `skin`, `scholar`,
  `scientist`, `philosopher`, `historian`, `scribe`, `shrewd`, `learned`, and
  `three`. This raises `spice.instancesOf.*` direct coverage to
  `410 / 479` (`85.59%`).
  `spice.instancesOf.unfortunately.*`, `spice.instancesOf.unseated.*`,
  `spice.instancesOf.venerated.*`, `spice.instancesOf.villageActivityAdj.*`,
  `spice.instancesOf.warrior*`, `spice.instancesOf.willYou.*`,
  `spice.instancesOf.wouldLikeTo.*`, and `spice.instancesOf.yeGodless.*` are
  now directly covered as one unfortunate/warrior/request/exclamation instance
  component unit, including `unfortunately`, `sadly`, `unseated`, `ousted`,
  `deposed`, `dethroned`, `lifted up on chairs`,
  `thrown into the air joyfully`, `venerated as idols`,
  `treated to a delightful feast`, `trade`, `artistic`, `monetary`,
  `spiritual`, `festive`, `safety`, `musical`, `architectural`,
  `technological`, `warrior`, `champion`, `mercenary`, `duelist`,
  `swordfolk`, `axefolk`, `daggerfolk`, `gunfolk`, `macefolk`, `fearsome`,
  `militant`, `knightly`, `merciless`, `fierce`, `brutish`, `death`, `dying`,
  `the void`, `mortality`, `battle`, `adversaries`, `bravery`, `ferocity`,
  `courage`, `valor`, `violence`, `wardenship`, `bloodshed`, `war`,
  `combat`, `will you`, `would you`, `what do you say`, `would like to`,
  `would love to`, `need to`, `must`, `ye Godless`, `ye Heathens`,
  `ye Skeptics`, and `ye Doubters`. This raises `spice.instancesOf.*` direct
  coverage to `470 / 479` (`98.12%`), leaving only route/template grammar
  leaves: `of *var* *var2*`, furniture prepositions, year prepositions, and
  `drowned in a lake of *liquid*`.
  The furniture-death and liquid-drowning residuals are now covered at the
  annals route grammar layer rather than as broad direct leaves:
  `JournalPatternTranslator` translates expanded `got ... stuck
  (in|under|inside|behind) ...` captures with Japanese position particles, and
  expanded `drowned in a lake of {liquid}` captures with HSE component lookup
  for the liquid slot. The remaining raw placeholder/preposition leaves stay
  classified as template grammar, not fixed vocabulary.

## Runtime-Proven Gaps

These are the first implementation targets because they have concrete visible
runtime evidence in Issue #737.

| Surface | Runtime evidence | Current closure status |
| --- | --- | --- |
| Campfire preserve result | `You preserved:` final output still contains `Some`, `into`, and `serving` even after the popup route fires. | Fixed in the preserve owner route family and the matching message-log frame. Source/result captures now use the display-name/component route, color restoration is preserved for `&y` / `&r` message-log output, and preserve quantity terms such as `serving(s)` / `dram(s)` are localized. |
| Campfire meal ingredients | `CampfireDescribeMealTranslationPatch` fires but ingredient captures such as `glass berries`, `nip of joined paprika`, and `chameleon horn` remain English. | Fixed in the campfire meal-description / ingredient owner family by routing bare ingredient fragments through component phrase reconstruction and adding scoped component coverage for the observed cooking ingredients. |
| Campfire ate popup | Static HSE source `spice.cooking.ate[0]` expands to `You eat the meal.` from `Campfire.CookFromIngredients`, `CookFromRecipe`, and `CookPresetMeal`. | Already covered by the existing popup message pattern `^You eat the meal\.$` in `messages.ja.json`, with L1 `MessagePatternTranslatorTests` and L2 `PopupShowTranslationPatchTests`. It is intentionally not promoted to a new exact dictionary leaf because the route is already owned by the popup pattern path. |
| Sultan history header/body/date | Category `Sultan Histories` translates, but `HISTORY OF ...`, annal body text, and `On the 22nd of Tishru i Ux` remain English or mixed. The issue update includes `Early in 3476 BR, after murdering a popular rival with malicious soldering, the sultan of Qud disappeared. Because of ウーヒム IVの shining visage, she was chosen as the successor.` | Fixed in the journal/sultan-history owner route family: `JournalSultanNote` / `JournalVillageNote` display-time translation, `HISTORY OF {name}` headers, standalone date lines, the runtime-proven Abdicate successor annal body, storage-time `JournalAPI.AddAccomplishment` text/mural/gospel variants, accepted annals patterns, and HSE component reconstruction are covered. |
| Journal map-note location text | Date line in a map note translates, but `Stargazerhome` and `5 parasangs east and 5 parasangs south of ...` remain English. | Fixed in the map-note display/storage owner route by translating generated settlement components and map distance lines. |
| Generated relationship/title fragments | `leader of the シャッガンナ Pest Flock` leaks inside an otherwise translated journal line. | Fixed as part of the generated journal relationship-title owner family. The route translates `leader of the {faction}` to `{faction}の指導者` in both pattern captures and already-translated accomplishment display text without adding a whole observed phrase leaf. |

## Previous Owner Plan Reconciliation

`docs/reports/2026-05-17-historic-string-expander-owner-plan.md` records many
HSE owner families as already covered by focused patches and tests. The current
queue still marks many of those same families as `action_required`,
`runtime_required`, or `likely_true_gap`; this is partly an overlay gap and
partly a real runtime-route gap.

Initial interpretation:

- Cooking recipe names, meal descriptions, cookbook titles, dynamic quest
  conversations, generated quest text, village wall/canvas descriptions,
  village reveal descriptions, sultan reveal descriptions, tombstones, urns,
  crypt plaques, relic names, pseudo-relic names, item naming, psychic hunter
  names, dimensions, farm names, merchant advertisements, broadcast-power
  occlusion reasons, and temple inscriptions all have claimed owner-route
  coverage in the previous plan. Each matching queue row must be either moved to
  `covered_by_owner_route` with concrete evidence or reopened if current tests
  do not prove the emitted shape.
- The runtime samples show that covered owner families can still miss sibling
  display/storage routes. In particular, campfire preserve output and
  sultan-history display text are real gaps even though adjacent HSE owners
  exist.
- `TextFilters.Angry` and `TextFilters.Lallated` remain a separate runtime
  follow-up tracked by Issue #726 and should not block HSE generated-prose route
  closure unless fresh evidence promotes them. Static analysis resolves
  `TextFilters.Filter` callers to `Preacher` and conversation `TextFilter`,
  direct `TextFilters.Angry` callers to `StyledStatus`, and `Lallated` data
  assignment to `DomesticatedSlave`; those are speech/status transformation
  routes rather than fixed HSE generated-prose owners.

## Runtime-Required Static Evidence

The remaining `runtime_required` rows are still statically analyzable. They are
not "unknown producer" rows; they are `static_owner_identified` plus
`runtime_sample_required`.

Current Roslyn probes over `~/dev/coq-decompiled_stable` resolve the relevant
owners without candidate or unresolved hits:

```bash
just semantic-probe --method Angry --owner XRL.Language.TextFilters --limit 20
just semantic-probe --method Lallated --owner XRL.Language.TextFilters --limit 20
just semantic-probe --method Filter --owner XRL.Language.TextFilters --limit 30
```

Observed static results:

- `TextFilters.Angry`: `3` resolved matching owner hits, `0` candidate,
  `0` unresolved.
  - `XRL.Language/TextFilters.cs:51` dispatches `"Angry"` from
    `TextFilters.Filter`.
  - `XRL.World.Capabilities/StyledStatus.cs:19` and `:20` directly transform
    styled status `Name` and `Value`.
- `TextFilters.Lallated`: `1` resolved matching owner hit, `0` candidate,
  `0` unresolved.
  - `XRL.Language/TextFilters.cs:57` dispatches `"Lallated"` from
    `TextFilters.Filter`.
  - `XRL.World.Parts/DomesticatedSlave.cs:24` and `:31` assign the
    `"Lallated"` filter to `Preacher` and `ConversationScript`.
- `TextFilters.Filter`: `2` resolved matching owner hits, `0` candidate,
  `0` unresolved.
  - `XRL.World.Parts/Preacher.cs:159` filters preacher line text before
    `EmitMessage` / particle text.
  - `XRL.World.Conversations.Parts/TextFilter.cs:38` filters conversation text
    during `PrepareTextLateEvent`.

These rows should remain `runtime_required` until fresh player-log evidence
shows the concrete final text emitted by those speech/status transformation
routes. Static evidence can name the owner and next runtime target, but it does
not prove the final visible string is translated, because the selected source
text, filter id, extras/noise, display sink, and patch order are runtime data.

## Completion Audit

Current status for the Issue #737 objective:

| Requirement | Current evidence | Status |
| --- | --- | --- |
| Audit the full current `history_generated_text` queue, not only the attached runtime samples. | Current regenerated policy output has `78` `history_generated_text` entries: `76` `covered_by_owner_route`, `2` `runtime_required`, `0` `action_required`, and `0` `partial_coverage`. | Satisfied for static queue classification. |
| Fix runtime-proven campfire cooking leaks at owner routes. | `CampfirePreserveTranslationPatch`, `CampfireRollIngredientsTranslationPatch`, `CampfireDescribeMealTranslationPatch`, `CampfireCookFromIngredientsTranslationPatch`, `CookingRecipeDisplayNameTranslationPatch`, message-log frame coverage, and focused L1/L2/L2G tests cover preserve frames, ingredient fragments, cook templates, recipe display names, and `spice.cooking.ate[0]` popup output. | Satisfied by owner-route tests; needs fresh runtime log for deployed closeout. |
| Fix runtime-proven journal/sultan/map-note generated residues at owner routes. | Journal storage/display patches, accepted annals patterns, component reconstruction, map-note generated-location handling, and L1/L2/L2G tests cover the attached sultan history, date, relationship-title, and location-distance samples. | Satisfied by owner-route tests; needs fresh runtime log for deployed closeout. |
| Close the additional static route-grammar residues found after the first audit. | `VillageProverb` `proverbs` / `proverbsCoda` final-output forms, `allThroughout` RampageRegion/Bey Lah frames, and `sultanClone` faked-death captures are now covered by focused `annals-patterns.ja.json` entries plus `JournalPatternTranslator` capture reconstruction. | Satisfied by focused L1 tests; needs fresh runtime log for deployed closeout. |
| Treat `Runtime required` rows as statically analyzable where possible. | Current semantic probes resolve `TextFilters.Angry`, `TextFilters.Lallated`, and `TextFilters.Filter` with `0` candidate and `0` unresolved hits. The owners are speech/status transformation routes, not untraced HSE generated-prose routes. | Satisfied for static owner identification. |
| Confirm no newer Issue #737 runtime-proven leak was added after the current implementation scope. | `gh issue view 737 --comments` currently shows two comments. The last update is `2026-05-18T22:19:38Z`, and it is the HSE vocabulary audit already reflected in the coverage report. | Satisfied as of this audit. |
| Prove the fixed routes in a fresh deployed runtime log. | `just deploy-mod` succeeded again at `2026-05-19 20:33 JST`, and the deployed `QudJP.dll` SHA-256 matches the worktree DLL (`e7ca108e00cc4b3d564fa30b190a47bf3d3a5526be0d91df81733f4a8440c910`). The current local `Player.log` is not post-sync evidence: modified `2026-05-18 11:38:06 JST`, `3673` lines, SHA-256 `b3241421a01d7c78d0bc5104cee92c4d5b30eb444d0bf00023d6f2d75fa29346`. | Not satisfied; runtime closeout remains pending. |

Therefore the current implementation can claim static route audit closure and
owner-route test coverage for all runtime-proven Issue #737 gaps, but it should
not be marked as fully runtime-closed until a fresh deployed run produces a new
`Player.log` for the affected campfire and journal routes.

## Runtime Closeout Checklist

Use this checklist only with a `Player.log` whose modification time is after the
latest `just deploy-mod` run, or whose QudJP build marker matches the build
being verified. The current local log is stale and must not be used as success
or regression proof for this worktree.

The post-sync runtime check should confirm:

- QudJP starts without QudJP-owned compile errors, `MODWARN`, or exceptions in
  the affected route families.
- Campfire meal-description output no longer exposes the original English
  ingredient fragments `glass berries`, `nip of joined paprika`, or
  `chameleon horn`.
- Campfire preserve output no longer exposes the original preserve-frame
  residue `Some`, ' into ', or `serving` in the `You preserved:` message-log
  and popup outputs.
- Sultan/journal history output no longer exposes the original generated
  header/body/date residue `HISTORY OF`, `with malicious soldering`,
  `shining visage`, or `On the 22nd of Tishru i Ux` for the same route family.
- Journal map-note output no longer exposes the original generated-location
  residue `Stargazerhome` or English `parasangs ... of ...` distance phrasing.
- Generated relationship/title output no longer exposes `leader of the ...`
  inside otherwise translated journal lines.
- `TextFilters.Angry` and `TextFilters.Lallated` rows stay
  `runtime_required` unless the fresh log contains concrete owner-route output
  for those speech/status transformations.

The absence of a sample in a fresh log is not enough to prove the route fixed;
it only leaves that route unobserved. A runtime closeout needs either observed
translated output for the affected route or a fresh log that deliberately
replays the attached Issue #737 scenarios.

Suggested first-pass closeout commands:

```bash
LOG="${QUDJP_PLAYER_LOG:-}"
if [ -z "$LOG" ]; then
  case "$(uname -s)" in
    Darwin)
      LOG="$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log"
      ;;
    Linux)
      for candidate in \
        "$HOME/.local/share/CavesOfQud/Player.log" \
        "$HOME/.config/CavesOfQud/Player.log"; do
        if [ -e "$candidate" ]; then
          LOG="$candidate"
          break
        fi
      done
      ;;
  esac
fi

just issue737-runtime-closeout
stat -f '%Sm %z %N' -t '%Y-%m-%d %H:%M:%S %Z' "$LOG"
wc -l "$LOG"
shasum -a 256 "$LOG"

rg -n \
  'You preserved|glass berries|nip of joined paprika|chameleon horn|HISTORY OF|with malicious soldering|shining visage|On the 22nd of Tishru i Ux|Stargazerhome|parasangs .* of |leader of the ' \
  "$LOG"

uv run python scripts/triage_untranslated.py --log "$LOG" \
  --output /tmp/issue737-post-sync-triage.json

just issue737-runtime-closeout-strict
uv run python scripts/issue737_runtime_closeout.py \
  --log "$LOG" \
  --min-mtime "2026-05-19T20:33:00+09:00" \
  --deployed-mod-root "$HOME/Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP" \
  --output /tmp/issue737-runtime-closeout-with-deploy.json
```

The `rg` command is expected to be interpreted with route context: a hit on
`You preserved` alone is not a failure, while hits that still contain `Some`,
' into ', or `serving` in the preserve frame remain a failure for this issue.
The dedicated closeout checker reports `stale` when the log predates the
deployment, `failed` when original Issue #737 residue is still observed,
`unobserved` when the fresh log does not replay every affected route, and
`passed` only when all non-TextFilters closeout checks are observed without the
original residue. Use `just issue737-runtime-closeout` for inspection because it
writes the JSON report even for `stale` or `unobserved` evidence; use
`just issue737-runtime-closeout-strict` as the completion gate because it exits
non-zero unless the status is `passed`. The TextFilters rows intentionally
remain `runtime_required` unless the fresh log contains concrete owner-route
output for those speech or status transformations.

When a deployed mod path is supplied, the closeout checker also records SHA-256
comparisons for the runtime-bearing files relevant to this issue:
`Assemblies/QudJP.dll`, `Scoped/historyspice-common.ja.json`,
`annals-patterns.ja.json`, and `journal-patterns.ja.json`. A clean fresh log
with a deployment hash mismatch reports `deployment_mismatch` instead of
`passed`.

## Queue Ledger

Decision meanings:

- `runtime_gap`: visible runtime evidence currently proves a gap related to this
  route family or its immediate display/storage consumer.
- `covered_by_owner_route`: current implementation and tests prove the
  producer/owner route named in the row is covered.
- `covered_overlay_stale`: previous owner-plan coverage likely exists, but the
  policy overlay still needs evidence-backed update.
- `owner_audit`: needs producer/display ownership review before any patch or
  policy update.
- `follow_up`: intentionally tracked outside Issue #737 unless fresh runtime
  evidence changes the scope.

| Queue status | Family | Count | Decision |
| --- | --- | ---: | --- |
| covered_by_owner_route | `XRL.World.ZoneBuilders/Village.cs::Village.BuildZone` | 232 | `BuildZone` HSE pet origin-story output flows into `AddVillagerConversation` and is covered by `VillagePetConversationTranslationPatch` plus L1/L2/L2G coverage; associated village gospel/era history storage routes are covered by existing historic narrative patches |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.BuildZone` | 229 | `BuildZone` HSE pet origin-story output flows into `AddVillagerConversation` and is covered by `VillagePetConversationTranslationPatch` plus L1/L2/L2G coverage; associated village gospel/era history storage routes are covered by existing historic narrative patches |
| covered_by_owner_route | `XRL.World/ZoneManager.cs::ZoneManager.SetActiveZone` | 117 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch`, `JournalEntryDisplayTextPatch`, finite SetActiveZone journey accomplishment patterns, and the shared covered `JournalAPI.AddAccomplishment` route |
| covered_by_owner_route | `XRL.World.Parts/SultanRegion.cs::SultanRegion.FireEvent` | 97 | `SultanRegionRevealDescriptionTranslationPatch` covers successful `SultanReveal` generated `Description.Short` frames, with L1 translator, L2 owner-route, and L2G target-resolution coverage |
| covered_by_owner_route | `XRL.World.Parts/Tombstone.cs::Tombstone.GenerateTombstone` | 87 | `covered_by_owner_route` through memorial intro/death-cause patches plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.getAVillageWall` | 81 | `covered_by_owner_route` through `VillageWallDescriptionTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.getAVillageWall` | 81 | `covered_by_owner_route` through `VillageWallDescriptionTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs::addQuestConversationToGiver` | 76 | `covered_by_owner_route` through `DynamicQuestConstructorConversationTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.cs::addQuestConversationToGiver` | 70 | `covered_by_owner_route` through `DynamicQuestConstructorConversationTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs::addQuestConversationToGiver` | 62 | `covered_by_owner_route` through `DynamicQuestConstructorConversationTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/EaterCryptPlaque.cs::EaterCryptPlaque.GeneratePlaque` | 57 | `covered_by_owner_route` through `EaterCryptPlaqueTextTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/Campfire.cs::Campfire.CookFromIngredients` | 52 | `covered_by_owner_route` through preserve popup/message-log frames, `DescribeMeal` / `RollIngredients` owner routes, recipe-created/metabolize owner popups, `CookingRecipe.GetDisplayName` grammar, journal accomplishment routing, and existing `spice.cooking.ate[0]` popup-pattern coverage |
| covered_by_owner_route | `XRL.World/Reputation.cs::Reputation.Modify` | 52 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch`, journal/annal patterns, and L2 storage-time coverage for became-loved accomplishment/mural/gospel variants |
| covered_by_owner_route | `XRL.World.Skills.Cooking/CookingRecipe.cs::CookingRecipe.GenerateRecipeName` | 49 | `covered_by_owner_route` through cooking recipe display-name owner patches plus L1/L2/L2G coverage, including component reconstruction, suffix forms, and route-local recipe preposition grammar for `with` / `inside of` / `on top of` / `in` / `over` without adding broad function-word leaves |
| covered_by_owner_route | `XRL.World.Parts/EaterUrn.cs::EaterUrn.GenerateUrn` | 47 | `covered_by_owner_route` through memorial intro and Markov corpus routes plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelic` | 45 | `covered_by_owner_route` through relic description addendum/name routes plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/VillageTerrain.cs::VillageTerrain.FireEvent` | 35 | `covered_by_owner_route` through `VillageTerrainRevealDescriptionTranslationPatch` plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World/DynamicQuestConversationHelper.cs::DynamicQuestConversationHelper.appendQuestCompletionSequence` | 35 | `covered_by_owner_route` through `DynamicQuestConversationTranslationPatch`, explicit intro-choice coverage, and L2/L2G tests |
| covered_by_owner_route | `XRL.World.Encounters/DimensionManager.cs::DimensionManager.InitializeFaction` | 32 | `covered_by_owner_route` through dimension generated-name patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/Village.cs::Village.generateWarden` | 30 | `covered_by_owner_route` through village leader conversation owner patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.generateWarden` | 30 | `covered_by_owner_route` through village leader conversation owner patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `Qud.API/JournalAPI.cs::JournalAPI.AddAccomplishment` | 28 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch`, display-time journal routes, accepted annals patterns, Resheph/annals fixture coverage, dynamic quest/opening/body/mutation/village accomplishment variants, Bey Lah `allThroughout` route grammar, and Issue #737 sultan/date/map-note/generated-title samples |
| covered_by_owner_route | `XRL.World.Encounters/DimensionManager.cs::DimensionManager.GenerateMoreDimensions` | 28 | `covered_by_owner_route` through extra-dimension name patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/Body.cs::Body.Dismember` | 28 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` body dismemberment text/mural/gospel patterns plus existing `BodyTranslationPatch` popup owner coverage |
| covered_by_owner_route | `XRL.Names/NameStyle.cs::NameStyle.Generate` | 27 | `covered_by_owner_route` for localized XML templatevars through `Naming.jp.xml` and `NamingXmlTests` |
| covered_by_owner_route | `XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateSeekerHunters` | 26 | `covered_by_owner_route` through psychic hunter title patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/Village.cs::Village.generateMayor` | 25 | `covered_by_owner_route` through village leader conversation owner patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.generateMayor` | 25 | `covered_by_owner_route` through village leader conversation owner patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/Campfire.cs::Campfire.RollIngredients` | 24 | `covered_by_owner_route` through `CampfireRollIngredientsTranslationPatch`, ingredient-fragment reconstruction, quantity grammar, L1/L2 tests, and direct `spice.cooking.terrain.*` coverage at `290 / 290` |
| covered_by_owner_route | `XRL.World.Parts/Campfire.cs::Campfire.CookFromRecipe` | 23 | `covered_by_owner_route` through `CampfireCookFromRecipeTranslationPatch` and L2 coverage for the `spice.cooking.ate[0]` popup flow plus the recipe menu line / out-of-ingredients popup owner route, with recipe display and meal-description routes covering sibling cook text |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateEndEvent` | 23 | display-route coverage through `JournalEntryDisplayTextPatch` / `JournalTextTranslator` for `JournalSultanNote`, with focused coda branch patterns in `annals-patterns.ja.json` and L2 coverage |
| covered_by_owner_route | `XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateExtradimensionalSoloHunters` | 22 | `covered_by_owner_route` through psychic hunter title patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Capabilities/ItemNaming.cs::ItemNaming.NameItem` | 21 | `covered_by_owner_route` through pseudo-relic/generated item-name routes plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/AnimatorSpray.cs::AnimatorSpray.HandleEvent` | 21 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` story patterns for imbue-life accomplishment/mural/gospel variants plus existing animator popup owner coverage |
| covered_by_owner_route | `XRL.World.Parts/GivesRep.cs::GivesRep.HandleEvent` | 21 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` and existing L1/L2 coverage for water-sib, single-combat, and murder-method accomplishment/mural/gospel variants |
| covered_by_owner_route | `XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateExtradimensionalSoloDeviant` | 21 | `covered_by_owner_route` through psychic hunter title patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.NameRuinsSite` | 20 | source-owner `QudHistoryFactoryNameRuinsSiteTranslationPatch` translates generated ruins-site modifier frames after HSE expansion; proper roots and `some forgotten ruins` remain pass-through to preserve downstream semantics |
| covered_by_owner_route | `XRL.World.Parts/RandomAltarBaetyl.cs::RandomAltarBaetyl.GenerateItem` | 20 | `covered_by_owner_route` through pseudo-relic generated-name route plus L2/L2G coverage |
| covered_by_owner_route | `XRL.UI/StatusScreen.cs::StatusScreen.BuyRandomMutation` | 19 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` mutation text/mural/gospel patterns plus existing `StatusScreenPopupTranslationPatch` mutation popup coverage |
| covered_by_owner_route | `XRL.World.Parts/Cookbook.cs::Cookbook.GenerateCookbook` | 19 | `covered_by_owner_route` through cookbook display-name owner patches plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World/Faction.cs::Faction.GenerateHeirloom` | 19 | `covered_by_owner_route` through pseudo-relic generated-name route plus L2/L2G coverage |
| covered_by_owner_route | `XRL.Names/SettlementNames.cs::SettlementNames.GenerateFarmNameInner` | 18 | `covered_by_owner_route` through settlement farm-name patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/DynamicQuestSignpostConversation.cs::DynamicQuestSignpostConversation.HandleEvent` | 18 | `covered_by_owner_route` through `DynamicQuestSignpostConversationTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/GenerateFriendOrFoe_HEB.cs::GenerateFriendOrFoe_HEB.replacePlaceholders` | 18 | source-owner `FriendOrFoeReasonTranslationPatch` translates HEB reason frames after HSE placeholder expansion plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.GenerateCultName` | 17 | storage-owner `QudHistoryFactoryGenerateCultNameTranslationPatch` translates stored `cultName` frames after HSE expansion using component dictionaries plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/RachelsTombstone.cs::RachelsTombstone.GenerateTombstone` | 17 | `covered_by_owner_route` through memorial intro/death-cause patches plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/VillageSurface.cs::VillageSurface.CheckReveal` | 16 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` village visit/founded/prohibition text/mural/gospel patterns |
| covered_by_owner_route | `XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs::fabricateFindASpecificItemQuest` | 13 | `covered_by_owner_route` through `DynamicQuestGeneratedQuestTextTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Capabilities/ItemNaming.cs::ItemNaming.GenerateRelicStyleName` | 11 | `covered_by_owner_route` through item-naming generated-name patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelicName` | 11 | `covered_by_owner_route` through relic generated-name patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/LocateRelicQuestManager.cs::LocateRelicQuestManagerSystem.CheckCompleted` | 10 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` plus production journal patterns for historic relic completion text/mural/gospel variants |
| covered_by_owner_route | `XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs::fabricateInteractWithAnObjectQuest` | 10 | `covered_by_owner_route` through `DynamicQuestGeneratedQuestTextTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.getAVillageCanvas` | 10 | `covered_by_owner_route` through village wall/canvas description patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.getAVillageCanvas` | 10 | `covered_by_owner_route` through village wall/canvas description patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World/VillageDynamicQuestContext.cs::VillageDynamicQuestContext.getQuestItemNameMutation` | 10 | `covered_by_owner_route` through `VillageDynamicQuestItemNameMutationTranslationPatch` plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/BroadcastPowerReceiver.cs::BroadcastPowerReceiver.HandleEvent` | 8 | `covered_by_owner_route` through broadcast-power occlusion reason patch plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs::System.FinishEntry` | 8 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch`; text/mural patterns now cover the finite `QuestableVerb` set from Base `ObjectBlueprints/Furniture.xml`, and the existing completion gospel pattern covers the commissioned object story |
| covered_by_owner_route | `XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemNounRoot` | 7 | source-owner `QudHistoryHelpersItemNameTranslationPatch` translates generated `Blessing of {root}`, `{root}'s Blessing`, and `{root} Blessing` item-name frames after HSE expansion using component dictionaries plus L1/L2/L2G tests; root+suffix pseudo-names remain pass-through |
| covered_by_owner_route | `XRL.Annals/VillageProverb.cs::VillageProverb.Generate` | 7 | storage-route coverage through `AddVillageGospelsTranslationPatch` / `HistoricNarrativeDictionaryWalker` for the `proverb` entity property, with all current `proverbs` / `proverbsCoda` final-output templates covered by focused `annals-patterns.ja.json` entries and L1/L2 coverage |
| covered_by_owner_route | `XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_TwoFactions` | 7 | storage-time `JournalObservationAddTranslationPatch` translates generated gossip prose through journal patterns plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/MerchantRevealer.cs::MerchantRevealer.GenerateMerchantLocation` | 7 | `covered_by_owner_route` through merchant advertisement patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/OpeningStory.cs::OpeningStory.AddAccomplishment` | 6 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` and production journal patterns for opening arrival text/mural/gospel variants |
| covered_by_owner_route | `XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::FindASiteDynamicQuestManagerSystem.CheckCompleted` | 6 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` plus production journal patterns for located-site completion text/mural/gospel variants |
| covered_by_owner_route | `XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemAdjRoot` | 5 | source-owner `QudHistoryHelpersItemNameTranslationPatch` translates generated trailing blessing item-name frames after HSE expansion using component dictionaries plus L1/L2/L2G tests; outer caller-added composite suffixes remain separate route risk |
| covered_by_owner_route | `XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal` | 5 | `covered_by_owner_route` for the preset-meal route through `CampfireCookPresetMealTranslationPatch`, with the shared `spice.cooking.ate[0]` popup surface still covered by the existing anchored popup pattern |
| covered_by_owner_route | `XRL.World.Parts/Campfire.cs::Campfire.DescribeMeal` | 5 | `covered_by_owner_route` through `CampfireDescribeMealTranslationPatch`, all `spice.cooking.cookTemplate` frame tests, translated `RollIngredients` output, and component reconstruction for runtime-proven and non-observed ingredient variants |
| covered_by_owner_route | `XRL.World.Parts/TempleDedicationPlaque.cs::TempleDedicationPlaque.GenerateInscription` | 5 | `covered_by_owner_route` through temple dedication inscription patch plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestManager.cs::FindASpecificItemDynamicQuestManagerSystem.CheckCompleted` | 5 | `covered_by_owner_route` through `JournalAccomplishmentAddTranslationPatch` plus production journal patterns for recovered-item completion text/mural/gospel variants |
| covered_by_owner_route | `XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelicNameByRegion` | 4 | `covered_by_owner_route` through relic generated-name patch plus L2/L2G coverage |
| covered_by_owner_route | `XRL.Annals/ImportedFoodorDrink.cs::ImportedFoodorDrink.generateFactionName` | 3 | source-owner `ImportedFoodOrDrinkFactionNameTranslationPatch` translates generated `Cult of the {root}` / `{root} Cult` faction-name frames after HSE expansion using existing HSE component dictionaries plus L1/L2/L2G tests |
| runtime_required | `XRL.Language/TextFilters.cs::TextFilters.Angry` | 3 | `follow_up` Issue #726: static owner evidence exists, but this speech/status transformation route still requires owner-specific runtime output evidence |
| runtime_required | `XRL.Language/TextFilters.cs::TextFilters.Lallated` | 3 | `follow_up` Issue #726: static owner evidence exists, but this speech/noise transformation route still requires owner-specific runtime output evidence |
| covered_by_owner_route | `XRL.World/RelicGenerator.cs::RelicGenerator.GenerateSpindleNegotiationRelic` | 3 | component wrapper into `GenerateRelic`; downstream relic name/description owner patches cover visible output plus L2/L2G coverage |
| covered_by_owner_route | `XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItem` | 2 | source helper coverage through `QudHistoryHelpersItemNameTranslationPatch`; no in-scope Annals caller was found, so this closes the helper route without claiming an additional visible consumer |
| covered_by_owner_route | `XRL.World.Parts/GenerateFriendOrFoe.cs::GenerateFriendOrFoe.replacePlaceholders` | 2 | source-owner `FriendOrFoeReasonTranslationPatch` translates exact/static and `$noun` reason frames after HSE placeholder expansion plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_OneFaction` | 2 | storage-time `JournalObservationAddTranslationPatch` covers the only decompiled call path into `JournalAPI.AddObservation` plus L1/L2/L2G coverage |
| covered_by_owner_route | `XRL.World/RelicGenerator.cs::RelicGenerator.SelectElement` | 2 | component token feeding item naming, pseudo-relic, faction heirloom, and baetyl relic routes; downstream owner patches cover visible output plus L2/L2G coverage |
| covered_by_owner_route | `XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.GenerateSultanateYearName` | 1 | source-owner `SultanateYearNameTranslationPatch` translates `Year of the {Adj} {Noun}` after HSE expansion plus L1/L2/L2G coverage |

## Remaining Implementation Order

1. Classify high-priority missing `HistorySpice.json` leaves before adding broad
   vocabulary coverage. Visible cooking terrain/ingredient, recipe-name,
   element, item/relic, instance, and extradimensional groups now have
   family-level coverage; remaining direct misses are mostly route grammar,
   generated suffixes, symbolic placeholders, or non-vocabulary connector
   choices.
2. Add future translations by meaningful route, patch, family, or semantic
   component units rather than isolated leaf-by-leaf increments. For cooking
   recipe names, keep component reconstruction natural for Japanese and do not
   join dish-name components with Japanese middle dots.
3. Continue HSE vocabulary coverage for visible component leaves. Annal
   ownership for `JournalAPI.AddAccomplishment` is now covered by the
   storage-time patch, display-time journal routes, accepted annals patterns,
   Resheph/annals fixtures, and focused dynamic quest/opening/body/mutation/
   village accomplishment variants, including the Bey Lah `allThroughout`
   accomplishment frame. Remaining journal risk is primarily component
   vocabulary breadth rather than a missing AddAccomplishment route.
4. Continue updating `scripts/text_construction_surface_policy.py` only for rows
   whose current implementation and tests provide concrete evidence. This route
   closure pass moved the Issue #737 campfire runtime-gap rows through family-level owner
   coverage, promoted the journal runtime-gap row to `covered_by_owner_route`,
   and moved seventy-six HSE families to `covered_by_owner_route` based on
   existing producer patches, localized data ownership, and L1/L2/L2G coverage.
   No `history_generated_text` row remains `action_required` or
   `partial_coverage`; only the two TextFilters speech/status transformation
   rows remain `runtime_required`.
5. Re-run the text-construction queue after future owner-route changes and keep
   this report aligned with any remaining non-closed rows.

## Verification Gate

Focused route/family implementation units should run the relevant layer tests first, then
the static queue checks:

```bash
just test-l1
just test-l2
just test-l2g
just localization-coverage-map-check
just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable" /tmp/issue737-text-construction-inventory.json 30
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/issue737-text-construction-inventory.json \
  --format json \
  --include valuable \
  --limit 0
```

Current local verification rerun:

- `just test-l1`: passed, `3791` tests.
- `just test-l2`: passed, `3869` tests.
- `just test-l2g`: passed, `495` tests.
- `just localization-coverage-map-check`: passed.
- `just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable" /tmp/issue737-text-construction-inventory-current.json 30`: regenerated `17459` family records and `2641` queue entries.
- `uv run python scripts/text_construction_surface_policy.py --inventory /tmp/issue737-text-construction-inventory-current.json --format json --include valuable --limit 0`: reproduced `78` `history_generated_text` entries with `76` `covered_by_owner_route` and `2` `runtime_required`.
- `just semantic-probe --method Angry --owner XRL.Language.TextFilters --limit 20`: reproduced `3` resolved matching owner hits, `0` candidate, and `0` unresolved.
- `just semantic-probe --method Lallated --owner XRL.Language.TextFilters --limit 20`: reproduced `1` resolved matching owner hit, `0` candidate, and `0` unresolved.
- `just semantic-probe --method Filter --owner XRL.Language.TextFilters --limit 30`: reproduced `2` resolved matching owner hits, `0` candidate, and `0` unresolved for `TextFilters.Filter`; the broader same-name `System.Predicate<T>.Filter` hits are excluded from the owner match.
- `uv run python scripts/historyspice_vocabulary_coverage.py "$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/HistorySpice.json" --format json`: reproduced HSE direct coverage at `3112 / 3897`, all-JSON direct coverage at `3218 / 3897`, `spice.cooking.terrain.*` at `290 / 290`, `spice.cooking.recipeNames.*` at `524 / 531`, `spice.cooking.*` at `817 / 838`, `spice.items.*` at `68 / 80`, `spice.gossip.*` at `5 / 32`, `spice.elements.*` at `423 / 423`, `spice.commonPhrases.*` at `854 / 860`, `spice.instancesOf.*` at `470 / 479`, and `spice.extradimensional.*` at `111 / 134`.
- `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~JournalPatternTranslatorTests"`: passed, `47` tests, including furniture-stuck preposition, liquid-drowning, village-proverb, all-throughout rampage/Bey Lah, and sultan-clone faked-death capture coverage.
- `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~JournalEntryDisplayTextPatchTests"`: passed, `14` tests, including production annals-pattern coverage for expanded furniture-stuck and liquid-drowning HSE routes.
- `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~HistoricNarrativeTranslationPatchesTests"`: passed, `5` tests, including the storage-route handoff used by village proverb entity-property coverage.
- `uv run python scripts/validate_candidate_schema.py scripts/_artifacts/annals/candidates_pending.json`: passed, `166` candidate(s).
- `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~CookingRecipeDisplayNameTranslatorTests"`: passed, `223` tests, including the guard that generated dish-name components are not joined with Japanese middle dots.
- `uv run python -m pytest scripts/tests/test_historyspice_vocabulary_coverage.py scripts/tests/test_issue737_runtime_closeout.py scripts/tests/test_text_construction_surface_policy.py scripts/tests/test_check_markdown_reports.py -q`: passed, `135` tests. The runtime closeout checker has explicit guards that generic journal output cannot satisfy the sultan-history route, probe `source` fields are not mistaken for visible residue when translated/final output is clean, single-quoted and double-quoted visible fields are parsed consistently, escaped quoted and `\uXXXX` probe values are parsed without hiding later residue, `passed` requires every non-TextFilters route to be observed without Issue #737 residue, strict mode rejects stale or unobserved evidence as a completion gate, and optional deployed-mod hash checks prevent a clean log from closing against mismatched runtime files. The HistorySpice vocabulary test guards the family-level HSE component sets including the now-covered extradimensional realm/void/cult-form, illness/justice/kinship/proximity, affirmation/lifesave/murder/time, religion/profanity/recency/reemergence, reward/royal/secession/place/stepdown, tar/thank/body/tremble/tutor/number, and unfortunate/warrior/request/exclamation instance component families.
- `just issue737-runtime-closeout`: wrote `/tmp/issue737-runtime-closeout.json` with `status: stale` because the current local `Player.log` mtime is `2026-05-18T11:38:06.047502+09:00`, older than the required `2026-05-19T20:33:00+09:00`.
- `uv run python scripts/issue737_runtime_closeout.py --log "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log" --min-mtime "2026-05-19T20:33:00+09:00" --deployed-mod-root "$HOME/Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP" --output /tmp/issue737-runtime-closeout-with-deploy.json`: wrote `status: stale` with `deployment.status: passed`; the deployed hashes match the worktree for `QudJP.dll`, `historyspice-common.ja.json`, `annals-patterns.ja.json`, and `journal-patterns.ja.json`.
- `just issue737-runtime-closeout-strict`: failed as expected with `status: stale`; this remains the final completion blocker until fresh post-deploy runtime evidence is collected.
- `just localization-check`: passed.
- `just translation-token-check`: passed.
- `just release-note-check origin/main HEAD`: passed.
- `just python-check`: passed.
- `just python-test`: passed, `1187` tests and `1` skipped.
- `git diff --check`: passed.

Runtime closeout still requires a Rosetta game run after the `2026-05-19
20:33 JST` deploy and new `Player.log` evidence for the fixed route families.
