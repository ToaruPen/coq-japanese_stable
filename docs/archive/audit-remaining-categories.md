# Remaining Localization Quality Audit

Scope: research-only audit of the 20 files listed in the request. No source files were edited. Findings below were consolidated from direct file inspection, targeted searches, and group-specific subagent audits, with suspect claims rechecked before inclusion.

## 1. Summary by group

| Group | Files | Summary |
| --- | --- | --- |
| A — Mutations & Abilities | `mutation-descriptions.ja.json`, `mutation-ranktext.ja.json`, `Mutations.jp.xml`, `HiddenMutations.jp.xml`, `ActivatedAbilities.jp.xml` | The biggest risks are in `mutation-ranktext.ja.json`: English residue left inside rank text, one severe mechanics corruption in Electrical Generation, and repeated terminology drift between XML names, long descriptions, and rank text. `ActivatedAbilities.jp.xml` is mostly readable, but a few lines are clearly MT-like or duplicated. |
| B — Quests & Journal | `Quests.jp.xml`, `ui-journal.ja.json`, `ui-journal-chronology.ja.json`, `ui-journal-kithkin.ja.json`, `journal-patterns.ja.json` | Overall quality is decent, but Group B has the highest density of glossary/proper-noun problems. `Quests.jp.xml` also contains one duplicated quest metadata block and several late-game quest typos that would be visible to players. |
| C — Combat & Messages | `messages.ja.json`, `ui-messagelog.ja.json`, `ui-messagelog-leaf.ja.json`, `ui-messagelog-world.ja.json`, `ui-messagelog-combat.ja.json`, `verbs.ja.json` | `messages.ja.json` itself is populated and largely usable, but `ui-messagelog.ja.json` still mixes legacy fragment-style entries with full-sentence entries. The most serious issue is a clear copy-paste mistranslation for “begin bleeding from another wound”. |
| D — Gospels | `world-gospels.ja.json` | The core token corpus is mostly Japanese, but the tail of the file includes malformed or non-functional procedural entries, broken action markers, and a few glossary violations (`イーター`, `クダ`). This group has the most placeholder-level risk. |
| E — Other | `bodypart-position.ja.json`, `templates-variable.ja.json`, `ui-auto-generated.ja.json` | `bodypart-position` is structurally clean. `templates-variable` has dynamic-token handling risk where English verb templates were hardcoded into fixed Japanese verbs. `ui-auto-generated.ja.json` still contains many user-facing untranslated labels and one garbled option value (`0|ì`). |

## 2. Top 50 critical findings across all groups

| Group | File | Line/Key | Current Japanese | Issue | Category | Suggested Fix |
| --- | --- | --- | --- | --- | --- | --- |
| A | `Mutations.jp.xml` | L22 / `Metamorphosis` | `変態` | Mutation name reads as the colloquial “pervert” sense in UI; too risky for a player-facing label. | 誤訳 | `変容` or `変成` |
| A | `Mutations.jp.xml` | L110 / `Blinking Tic` | `瞬移` | Over-shortened biome adjective; reads like an internal abbreviation rather than shipped text. | 文体不統一 | Use a full form such as `瞬間移動の` or another established adjective form |
| A | `ActivatedAbilities.jp.xml` | L18 / `CommandSwoopAttack` | `地上の目標へ急降下して攻撃し、1ターンで攻撃し、1ターンで上空へ戻る。` | Duplicate `攻撃し` makes the sentence visibly broken. | 機械翻訳痕跡 | `地上の目標へ急降下して攻撃し、その後上空へ戻る。` |
| A | `mutation-descriptions.ja.json` | L285 / `mutation:Photosynthetic Skin` | `{{rules|1 day}}` | English remains inside a rules tag and will render as-is. | 未翻訳テキスト | `{{rules|1日}}` |
| A | `mutation-descriptions.ja.json` | L285 / `mutation:Photosynthetic Skin` | `{{w|コンソーシアム・オブ・ファイタ}}` | Glossary term `Consortium of Phyta` is mistransliterated and inconsistent with existing repo usage. | 固有名詞不統一 | Use the glossary form, e.g. `{{w|フィタ連合}}` or the chosen canonical full form |
| A | `mutation-descriptions.ja.json` | L355 / `mutation:Stinger (Confusing Venom)` | `能動アビリティ「Sting」で確実に命中・貫通する。` | Ability name left in English. | 未翻訳テキスト | Translate the ability name consistently, e.g. `能動アビリティ「刺突」` |
| A | `mutation-descriptions.ja.json` | L360 / `mutation:Stinger (Paralyzing Venom)` | `能動アビリティ「Sting」で確実に命中・貫通する。` | Same untranslated ability name. | 未翻訳テキスト | `能動アビリティ「刺突」` |
| A | `mutation-descriptions.ja.json` | L365 / `mutation:Stinger (Poisoning Venom)` | `能動アビリティ「Sting」で確実に命中・貫通する。` | Same untranslated ability name. | 未翻訳テキスト | `能動アビリティ「刺突」` |
| A | `mutation-ranktext.ja.json` | L2861 / `mutation:Burrowing Claws:rank:1` | `爪s destroy walls after 4 penetrating hits.` | English source residue was left inside the shipped rank text. Repeats across all 10 ranks. | 未翻訳テキスト | Remove the English line and keep the Japanese explanation only |
| A | `mutation-ranktext.ja.json` | L2861 / `mutation:Burrowing Claws:rank:1` | `爪s are also a short-blade class natural weapon that deal {{rules|1d2}} base damage to non-walls.` | Second English residue line left in the same entry; repeats across all 10 ranks. | 未翻訳テキスト | Remove the English line and keep the Japanese explanation only |
| A | `mutation-ranktext.ja.json` | L3611 / `mutation:Electrical Generation:rank:1` | `チャージ1点ごとに4d1000ダメージを与える。...最大1チャージあたり1000体まで。` | Mechanics text is corrupted: the numbers/units were reordered into nonsense. | 誤訳 | Align with the description logic, e.g. `1000チャージごとに1d4ダメージ` and `1000チャージごとに最大1体へ連鎖` |
| B | `Quests.jp.xml` | L113-L117 / `A Signal in the Noise` | `Accomplishment/Hagiograph/Gospel` all mirror `A Canticle for Barathrum` | Entire quest metadata block is duplicated from the previous quest and no longer matches the actual quest meaning. | 誤訳 | Replace with signal/decoding-specific accomplishment, hagiograph, and gospel text |
| B | `Quests.jp.xml` | L324 / `Tomb of the Eaters` quest text | `食らう者の墓` | Glossary term should be `喰らう者の墓所`. Wrong kanji and missing `所`. | 固有名詞不統一 | `喰らう者の墓所` |
| B | `Quests.jp.xml` | L328 / `Tomb of the Eaters` step text | `食らう者の墓の北西墓地にいる友好的なタレット、ヴィヴィラ...` | Two glossary problems in one line: `食らう者の墓` and `ヴィヴィラ` (Vivira). | 固有名詞不統一 | `喰らう者の墓所...ビヴィラ` |
| B | `Quests.jp.xml` | L348 / `Tomb of the Eaters` accomplishment | `食らう者の墓から帰還し...` | Same glossary mismatch for Tomb of the Eaters. | 固有名詞不統一 | `喰らう者の墓所から帰還し...` |
| B | `Quests.jp.xml` | L359 / `Ascend the Tomb and Cross into Brightsheol` | `ブライトシールへ渡る方法を探す。` | Proper noun typo; should be `ブライトシェオル`. | 固有名詞不統一 | `ブライトシェオルへ渡る方法を探す。` |
| B | `Quests.jp.xml` | L371 / `Landing Pads` | `スリンとを受け入れさせる。` | `Slynth` is misspelled as `スリンと`. | 固有名詞不統一 | `スリンスを受け入れさせる。` |
| B | `Quests.jp.xml` | L377 / `Landing Pads` | `タアに話しかけ、スリンとを招集して決断させる。` | Two proper nouns drift at once: `タア` and `スリンと`. | 固有名詞不統一 | `タハに話しかけ、スリンスを招集して決断させる。` |
| B | `Quests.jp.xml` | L402 / `Pax Klanq, I Presume?` | `ケテルへ戻り、ディヴヴラフと話す。` | Dyvvrach spelling is off. | 固有名詞不統一 | `ディヴラックと話す。` |
| B | `Quests.jp.xml` | L39 / `Raising Indrix` | `アマランサイン・プリズムを取り戻し...` | Glossary uses `黒いガラス`; this line breaks that canon while the same quest already uses `黒いガラスの欠片` at L30. | 固有名詞不統一 | Use the glossary-approved form consistently |
| B | `Quests.jp.xml` | L242 / `Pax Klanq, I Presume?` | `パックス・クランク` | Glossary canonical form is `パクス・クランク`. | 固有名詞不統一 | `パクス・クランク` |
| B | `ui-journal.ja.json` | L210 / `TerrainJoppa` | `TerrainJoppa` | Internal ID is still visible in Japanese assets. The adjacent `TerrainKyakukya` and `TerrainPalladiumReef 3l` entries show the same pattern. | 未翻訳テキスト | Replace with player-facing location names if these are ever surfaced |
| B | `ui-journal.ja.json` | L450 | `ショーマーのレインウォーターは、ブライトシェオルはスピンドルの頂に住むセラフの夢だと主張している。` | Double `は` makes the sentence grammatically broken. | 文体不統一 | `ショーマーのレインウォーターは、ブライトシェオルがスピンドルの頂に住むセラフの夢だと主張している。` |
| B | `ui-journal.ja.json` | L480 | `スヴァルディムのゴエク、マク、ギーウブ...` | Multiple proper nouns drift from glossary forms (e.g. `スヴァーディム`, `ジーブ`). | 固有名詞不統一 | Normalize the names to glossary-approved forms |
| B | `ui-journal-chronology.ja.json` | L55 | `生命の樹チャヴァ` | Glossary uses `生命の木チャヴァ`. | 固有名詞不統一 | `生命の木チャヴァ` |
| B | `ui-journal-chronology.ja.json` | L150 | `タァから奇跡の彫像を授かった。` | `Thah` is inconsistent with glossary form `タハ`. | 固有名詞不統一 | `タハから奇跡の彫像を授かった。` |
| B | `ui-journal-chronology.ja.json` | L255 | `喰らう者の墓` | Missing the glossary-approved `所`. | 固有名詞不統一 | `喰らう者の墓所` |
| B | `ui-journal-chronology.ja.json` | L280 | `スウィリング・ヴァスト` | Glossary-approved location name is `大呑の淵`. | 固有名詞不統一 | `大呑の淵` |
| B | `journal-patterns.ja.json` | L51 | `大塩砂漠モグライィ` | Glossary spelling drift for `Moghra'yi`. | 固有名詞不統一 | `モグラヤイ` |
| B | `Quests.jp.xml` | L340 | `ジャミョ` | `Gyamyo` spelling does not match glossary form `ギャミオ`. | 固有名詞不統一 | `ギャミオ` |
| C | `ui-messagelog.ja.json` | L225 / `{target} begin bleeding from another wound!` | `装備していません：` | Clear copy-paste mistranslation; wrong message entirely. | 誤訳 | Replace with an additional-bleeding message, e.g. `{target}は別の傷口からも出血し始めた！` |
| C | `ui-messagelog.ja.json` | L125 | `よろめかせた：` | Fragment-only translation depends on legacy concatenation and reads unnaturally on its own. | 文体不統一 | Convert this route to a full-sentence message or remove the legacy fragment entry |
| C | `ui-messagelog.ja.json` | L150 | `射撃武器！` | Another fragment-only entry; if surfaced independently, it is meaningless. | 文体不統一 | Replace with a complete sentence such as `適切な射撃武器を装備していない！` |
| C | `ui-messagelog.ja.json` | L155 | `{{r|外した：` | Color-tagged fragment is incomplete and relies on external concatenation. | 文体不統一 | Replace with a full combat miss sentence |
| C | `ui-messagelog.ja.json` | L165 | `（あなたを攻撃、武器：` | Parenthetical fragment is not natural Japanese as a standalone message. | 文体不統一 | Convert to a full descriptive line instead of a join fragment |
| C | `ui-messagelog-leaf.ja.json` | L233 | `あなたはQudの洞窟へ旅立った。` | `Qud` remains untranslated. | 固有名詞不統一 | `あなたはクッドの洞窟へ旅立った。` |
| C | `messages.ja.json` | L1058 | `パクス・クランクとバラスルム派の助けを借りて{0}を建造した。` | `Barathrumites` was mistranscribed as `バラスルム派`. | 固有名詞不統一 | `バラサラム派` |
| C | `ui-messagelog-combat.ja.json` | L140 | `{target}の装甲を貫通できなかった。（バグ回避）` | Internal note `（バグ回避）` would leak into player-facing text. | 機械翻訳痕跡 | Remove the note and keep only the actual combat message |
| C | `ui-messagelog-world.ja.json` | L67 | `ThawZone 例外` | Technical term is still exposed in English. | 未翻訳テキスト | Use a normalized Japanese error label, e.g. `ゾーン解凍例外` if this string is meant to be localized |
| D | `world-gospels.ja.json` | L80 | `<spice.commonPhrases.suspiciously.!random>とあたりを見回し、<spice.instancesOf.leansIn.!random>*` | Closing `*` survives, but the opening action marker was lost. | プレースホルダー破損 | Restore the paired action marker or remove both markers safely |
| D | `world-gospels.ja.json` | L85 | `<spice.commonPhrases.suspiciously.!random>とあたりを見回す*` | Same one-sided action marker break. | プレースホルダー破損 | Restore the opening `*` or remove the marker pair safely |
| D | `world-gospels.ja.json` | L5650 | `イーターは<entity.orgPrincipleStash>の神性を称えた` | Glossary term should be `喰らう者`. | 固有名詞不統一 | `喰らう者は...` |
| D | `world-gospels.ja.json` | L5860 | `*itemName.an*を*bodyPartName.a*義肢として使おうとした` | The source key includes `*bodyPartName*`, but the translated text drops the actual body-part noun. | プレースホルダー破損 | Reintroduce the missing dynamic token in a natural Japanese order |
| D | `world-gospels.ja.json` | L6245 | `<spice.history.gospels.>（乗り物）` | Malformed spice tag plus translator note/comment text; looks non-functional as shipped. | プレースホルダー破損 | Replace with a valid spice path or a real translatable phrase |
| D | `world-gospels.ja.json` | L6255 | `<spice.history.gospels.[時代].vehicle.!random>が...` | Translator note `[時代]` was left inside the procedural tag and will break lookup. | プレースホルダー破損 | Replace `[時代]` with the actual era key (`EarlySultanate`/`LateSultanate` etc.) |
| D | `world-gospels.ja.json` | L6415 | `クダの民` | Glossary form is `クッド`. | 固有名詞不統一 | `クッドの民` |
| E | `templates-variable.ja.json` | L16 | `=subject.T=は日光の中で日光浴し、滋養に満ちた光を吸収した。` | The source uses dynamic verb markers (`=verb:bask=`, `=verb:absorb=`), but the translation hardcodes finite verbs and removes verb templating. | プレースホルダー破損 | Preserve the variable-template behavior or move this route upstream so conjugation stays dynamic |
| E | `ui-auto-generated.ja.json` | L89 | `Adventuring` | User-facing category label is still fully untranslated. | 未翻訳テキスト | `冒険` or another approved category label |
| E | `ui-auto-generated.ja.json` | L814 | `Wait a number of turns` | Command label remains untranslated. | 未翻訳テキスト | `指定ターン数待機` |
| E | `ui-auto-generated.ja.json` | L1389 | `0|ì` | Garbled option value strongly suggests encoding or source-copy corruption. | プレースホルダー破損 | Recheck the original upstream string and replace with the intended glyph/value |

## 3. Pattern-level findings per group

### Group A

- `mutation-ranktext.ja.json` still contains a repeated English-residue pattern. `Burrowing Claws` is the clearest case, and the same file also shows mechanics corruption in `Electrical Generation`.
- Mutation naming style is not fully stabilized across XML display names and dictionary prose. `変態`, `瞬移`, and the `Consortium of Phyta` line are the most visible examples.
- `mutation-descriptions.ja.json` and `mutation-ranktext.ja.json` drift on terminology and round/turn phrasing. Even where both are Japanese, the wording does not always stay aligned.

### Group B

- Proper-noun drift is the main Group B problem. The affected files repeatedly diverge from glossary spellings in late-game quest and chronology content.
- `Quests.jp.xml` contains one content-level duplication (`A Signal in the Noise` reusing the previous quest’s metadata block), so this is not just a terminology pass issue.
- `ui-journal.ja.json` still contains untranslated internal IDs (`Terrain*`) and at least one syntactically broken sentence (`...は、...は...`).

### Group C

- `ui-messagelog.ja.json` still mixes fragment-era message assembly with newer full-sentence routes. This causes several entries to read like dangling pieces rather than complete Japanese messages.
- One message is plainly wrong (`begin bleeding from another wound` -> `装備していません：`), so Group C is not only stylistic; it includes a hard semantic regression.
- Combat/message dictionaries also leak internal notes or English terms (`（バグ回避）`, `ThawZone`), which should not ship into player-visible text.

### Group D

- The first large block of `world-gospels.ja.json` is mostly Japanese, but the later Annals-oriented region contains malformed spice tags, translator notes embedded in tags, and entries that look like stubs rather than functional procedural text.
- Several lines lose only one side of a paired marker (`*...*`), which is especially risky because the surrounding string still looks “mostly correct” until runtime formatting breaks.
- Proper nouns drift here too, especially `イーター` and `クダ`, but the higher-severity issue is placeholder/procedural integrity.

### Group E

- `ui-auto-generated.ja.json` has a clear pattern gap: titles are mostly localized, but many option values and command labels remain in English.
- `templates-variable.ja.json` is structurally valid, yet at least one route hardcodes verbs that were dynamic in the source template. That is a localization-quality issue even if the string remains grammatically readable.
- `bodypart-position.ja.json` is clean enough to deprioritize, aside from semantic collapsing (`Fore`/`Front`, `Hind`/`Rear`) if the game ever needs those distinctions in Japanese.

## 4. Glossary proper noun mismatches

| English term | Glossary / canonical form | Observed variant | File / line |
| --- | --- | --- | --- |
| Tomb of the Eaters | `喰らう者の墓所` | `食らう者の墓`, `喰らう者の墓` | `Quests.jp.xml` L324/L328/L348, `ui-journal-chronology.ja.json` L255 |
| Brightsheol | `ブライトシェオル` | `ブライトシール` | `Quests.jp.xml` L359 |
| Pax Klanq | `パクス・クランク` | `パックス・クランク` | `Quests.jp.xml` L242/L245/L247/L259/L409 |
| Consortium of Phyta | glossary form in `docs/glossary.csv` | `コンソーシアム・オブ・ファイタ` | `mutation-descriptions.ja.json` L285 |
| Dyvvrach | `ディヴラック` | `ディヴヴラフ` | `Quests.jp.xml` L402 |
| Vivira | `ビヴィラ` | `ヴィヴィラ` | `Quests.jp.xml` L328 |
| Gyamyo | `ギャミオ` | `ジャミョ` | `Quests.jp.xml` L340 |
| Thah | `タハ` | `タア`, `タァ` | `Quests.jp.xml` L377/L383, `ui-journal-chronology.ja.json` L150 |
| Chavvah, the Tree of Life | `生命の木チャヴァ` | `生命の樹チャヴァ` | `ui-journal-chronology.ja.json` L55/L190 |
| Swilling Vast | `大呑の淵` | `スウィリング・ヴァスト` | `ui-journal-chronology.ja.json` L280 |
| Moghra'yi | `モグラヤイ` | `モグライィ` | `journal-patterns.ja.json` L51 |
| Svardym | `スヴァーディム` | `スヴァルディム` | `ui-journal.ja.json` L480 |
| Geeub | `ジーブ` | `ギーウブ` | `ui-journal.ja.json` L480 |
| Qud | `クッド` | `Qud`, `クダ` | `ui-messagelog-leaf.ja.json` L233, `world-gospels.ja.json` L6415/L6420 |
| Barathrumites | `バラサラム派` | `バラスルム派` | `messages.ja.json` L1058 |
| Amaranthine Prism | glossary form in `docs/glossary.csv` | `アマランサイン・プリズム` | `Quests.jp.xml` L39 |

## Notes

- `ui-journal.ja.json` also contains adjacent untranslated IDs `TerrainKyakukya` (L215) and `TerrainPalladiumReef 3l` (L220); they were omitted from the glossary table above because they are untranslated internal IDs rather than proper nouns translated to the wrong Japanese.
- `world-gospels.ja.json` contains more than one malformed Annals/stub block near L6245-L6425. Only the clearest representatives were included in the Top 50 table; that region should be reviewed as a batch.
