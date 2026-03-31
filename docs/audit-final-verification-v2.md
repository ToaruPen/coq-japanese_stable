# Final translation quality verification v2

- Date: 2026-03-31
- Scope: `Mods/QudJP/Localization/` only
- Mode: audit only (no localization asset edits)

## Summary

| Check | Result | Notes |
| --- | --- | --- |
| Check 1: Glossary consistency | **FAIL** | 16/20 glossary targets passed as-is; 4 targets still have user-visible mismatches. |
| Check 2: Mod name consistency | **FAIL** | `gesticulating` passes, but `masterwork`, `spring-loaded`, and `scaled` are still inconsistent across the three required locations. |
| Check 3: Structural integrity | **PASS** | `xmllint --noout` passed for all 35 XML files; JSON parsing passed for all 56 JSON files. |
| Check 4: Translation quality spot check | **FAIL** | Overall quality is medium-high, but sampled entries still contain untranslated fragments and a few awkward templates. |

## Check 1: Glossary consistency

### Pass

- **喰らう者** — PASS. `食らう者` and non-exempt `イーター` hits: 0. `メモリー・イーター` exemption did not produce any other visible leftovers.
- **喰らう者の墓所** — PASS. `喰らう者の墓` (without `所`) hits: 0.
- **ベテスダ・スーサ** — PASS. `ベセスダ` hits: 0.
- **メカニマス教団 / 教徒** — PASS. `メカニミスト` and bare `メカニマス教` hits: 0.
- **グリット・ゲート** — PASS. `グリットゲート` hits: 0.
- **バラサラム** — PASS. `バラトラム` / `バラスルム` hits: 0.
- **ゼータクローム** — PASS. `ジータクローム` / `ゼタクロム` hits: 0.
- **フラーレン** — PASS. `フレライト` hits: 0.
- **ドロマド商団** — PASS. `ドロマド商人` hits: 0.
- **ギルシュ** — PASS. `ガーシュ` hits: 0.
- **クッド** — PASS for user-visible strings. Raw `Qud` remains only in IDs, class names, contexts, and English keys/patterns; no visible `text` / `template` values with raw `Qud` were found.
- **神殿** — PASS. `神社` hits: 0.
- **ディヴラック** — PASS. `ディヴヴラフ` / `ディヴラク` hits: 0.
- **ビヴィラ** — PASS. `ヴィヴィラ` hits: 0.
- **スヴァーディム** — PASS. `スヴァルディム` hits: 0.
- **ブライトシェオル** — PASS. `ブライトシオル` / `ブライトシール` hits: 0.

### Remaining issues

1. **工匠** — FAIL
   - `Mods/QudJP/Localization/Conversations.jp.xml:4812`
   - `修理工菌` が残存。Pax Klanq / tinker-role 文脈なので、今回の glossary rule 上は NG。

2. **生命の樹チャヴァ** — FAIL
   - Allowed/non-blocking exception: `モジュロの月階段と生命の木` は書名として残っており、`docs/glossary.csv` 側にも別語彙として存在するため除外。
   - Blocking visible mismatches:
     - `Mods/QudJP/Localization/Conversations.jp.xml:367` — `〈生命の木〉`
     - `Mods/QudJP/Localization/HiddenConversations.jp.xml:574,590,...` — `生命の木の冠`
     - `Mods/QudJP/Localization/Quests.jp.xml:389,391` — `生命の木`
     - `Mods/QudJP/Localization/ObjectBlueprints/WorldTerrain.jp.xml:549` — `生命の木ハヴァ`（`樹` ではないうえ、`ハヴァ` も表記不整合）

3. **モグラヤイ** — FAIL
   - `Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml:2304`
   - `モグライ各地` が残存。

4. **パクス・クランク** — FAIL
   - `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:13277`
   - `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:13281`
   - `パックス・クランク` が 2 件残存。

## Check 2: Mod name consistency

Required locations checked:
- `Mods/QudJP/Localization/Mods.jp.xml`
- `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json`
- `Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json`

| Mod | Expected | Result | Evidence |
| --- | --- | --- | --- |
| `masterwork` | `傑作` | **FAIL** | `Mods.jp.xml:19` is `傑作`, but `world-mods.ja.json:390,395` and `ui-displayname-adjectives.ja.json:370` still use `名工品`. |
| `spring-loaded` | `バネ仕掛け` | **FAIL** | `Mods.jp.xml:59` and `world-mods.ja.json:685` use `バネ仕掛け`, but `ui-displayname-adjectives.ja.json:485` still uses `バネ仕掛けの`. |
| `gesticulating` | `蠢く` | **PASS** | `Mods.jp.xml:52`, `world-mods.ja.json:270`, `ui-displayname-adjectives.ja.json:295,1100` all agree on `蠢く`. |
| `scaled` | `鱗状の` | **FAIL** | `Mods.jp.xml:55` and `ui-displayname-adjectives.ja.json:450,1220` use `鱗状の`, but `world-mods.ja.json:580,585` still uses `鱗装甲`. |

## Check 3: Structural integrity

- XML validation: **PASS** (`xmllint --noout` on all 35 XML files)
- JSON validation: **PASS** (JSON parse on all 56 JSON files)
- Result: no structural blockers detected.

## Check 4: Translation quality spot check

Method:
- Deterministic random sample (`seed=20260331`)
- 20 entries each from:
  - `Conversations.jp.xml`
  - `ObjectBlueprints/Items.jp.xml` (`Description@Short` only)
  - `Dictionaries/world-gospels.ja.json`
  - `Dictionaries/messages.ja.json`

### Overall ratings

| File | Average | Summary |
| --- | --- | --- |
| `Conversations.jp.xml` | **3.90/5** | Generally strong voice and terminology, but sampled untranslated tokens (`pest`, `-elseing`) remain. |
| `ObjectBlueprints/Items.jp.xml` | **4.15/5** | Strongest sample set; vivid item prose with only minor terminology wobble. |
| `Dictionaries/world-gospels.ja.json` | **4.00/5** | Mostly solid token work, but a few placeholder-ish or overly fragmentary entries remain. |
| `Dictionaries/messages.ja.json` | **3.95/5** | Broadly usable, though a few combat templates are still awkward in Japanese. |

### Conversations.jp.xml samples

1. **Mak** — **4/5** — `俺が誰か？ マクだ。それ以上はやらん。甘い舌を、くだらん問いに無駄遣いはせん。` — 自然で口調も合う。
2. **Nuntu** — **4/5** — `私個人の善意だけで動けるなら受け入れたかもしれない。しかし残念ながら、私は心や頭よりも職務を優先する。私の民からの信頼が厚ければ、考えを改…` — 職務優先の論理が自然。
3. **BaseConversation** — **4/5** — `=hermit=、もう二度と邪魔しないと誓う。` — 定型として安定。
4. **Eskhind** — **4/5** — `何を言っているかって？ 孤立した村を老いた支配者が握り続ける仕組みのことよ。外の世界を見た者は徹底的に忘れさせる。住民がどれほど明日を望ん…` — 説明文は自然だがやや硬い。
5. **Meyehind** — **4/5** — `{{emote|*メイエヒンドの肩の力が少し抜ける。*}} そう。その通り、ケンドレン。 それに、事件そのものとは関係ない。冷たい伝統と、…` — 感情描写が自然。
6. **Thah** — **4/5** — `話し合いと熟慮の末、合意に至った。スリンスの新しい家はベイ・ラーだ。世界へ挨拶しはじめた隠れ里には馴染みがある。これは、独りじゃない、穏や…` — 文意は通るが一部やや詩的で硬い。
7. **Agyra** — **4/5** — `礼を言う、アギラ。` — 短い応答として自然。
8. **Barathrum** — **5/5** — `クライマー設計は電磁石で惑星磁場と結合し、トルクを生み出す仕組みだ。だがスピンドル自身が干渉磁場を生み出しており、おそらく不法な上昇を防ぐ…` — 専門用語と固有名詞の統一が良い。
9. **Barathrum** — **4/5** — `その顔……清められている。礼を言う、=name=。つい先日パラジウム礁で回収したポリジェルを受け取ってくれ。` — 短文で自然。
10. **Tzedech** — **4/5** — `{{emote|*鈴が骨を震わせる警鐘を鳴らす。*}} 嘲りか？ 挑発か？ これだけのことをしておいて、よくも交信を試みられるものだ。` — 挑発の口調が伝わる。
11. **Agyra** — **4/5** — `=ifplayerplural:君たちは:そなたは=世界を変えた、=factionaddress:Mopango=。この奇妙な存在たちは何…` — 格調と敬称が安定。
12. **Nuntu** — **5/5** — `猿のように静まり、思索せよ、旅人よ。私の村キャクキャへようこそ。温めたキノコ酒をどうぞ！` — 村人の歓迎文として自然。
13. **Dadogom** — **4/5** — `ほう。私に触れさせてくれるか？` — 短い確認文として自然。
14. **Keh** — **4/5** — `アイヴァ。売り払ったのだろう。誰にも話すな。任務を終えていない者に報酬は与えられんのだ、わかってくれ。` — 会話として自然。
15. **Barathrum** — **4/5** — `そう言ってくれると信じていた、=name=。信号を記録したディスクを渡しておこう、念のためにな。それと、死の印を手に入れたときに使う入れ墨…` — 指示文として明瞭。
16. **Sixshrew** — **2/5** — `どうして pest と呼ばれている？` — `pest` が未訳。
17. **Tammuz** — **4/5** — `{{emote|*タムズは静まり、動かない。*}}` — 演出文として自然。
18. **ManyEyes** — **3/5** — `MAQQOM YD。南域、SALUM ヨリ 自由。` — 意図的な断片調だが可読性は落ちる。
19. **HindrenAfflicted** — **5/5** — `はぁぁぁぁぁ……。` — 息遣い表現として十分。
20. **TauNoLonger** — **2/5** — `ああ。 そうか。-elseing を助けてくれたことは感謝するが、道を塞ぐなら新たな問題を生む。そうだろう？ 君のことは知りたくない。 生…` — `-elseing` が未訳でノイズ。

### Items.jp.xml (Short) samples

1. **Lead Slug** — **4/5** — `鉛を鋳造して円柱状に固め、先端を削いで尖らせた弾塊だ。` — 説明が明瞭。
2. **Security Card** — **4/5** — `符号化された金属紙の短冊で、錠機構に差し込むと一部の扉を開けられる。` — 用途が分かりやすい。
3. **Sooty Smock** — **4/5** — `光を呑む虚無の色をした炭の火孔がエプロンを穴だらけにし、星が排中律へと疾るさまを見た詭弁家の夢を映している。` — 詩的だが雰囲気は合う。
4. **Cudgel6th** — **4/5** — `高炉が惑星の潮汐エネルギーを吸い上げ、炭素を太陽温度まで熱して六角の鋳型に流し込む。鋳型を摘み上げる翡翠の鉗子が蒸散し、緑の煌めき――バナ…` — 素材描写が豊か。
5. **BaseTierFeet3_DV** — **4/5** — `高所の空気圧ダクトを菱形に敷き詰めていたゴム断熱材が真夜中に切り剥がされ、仕立屋の盟約に運び込まれてパンタフルの一組に仕立てられた。` — 背景描写が自然。
6. **Garbage** — **4/5** — `老いて軋む世界の破片が時間点から削ぎ落とされ、現在の間際へと圧し潰されている。見覚えのかけらがいくつか残る――ガラスのレンズ、インクのセリ…` — 瓦礫感の描写が良い。
7. **ColdGrenade3** — **4/5** — `厚い金属ジャケットが炸薬を包み、クイックリリースのレバーで締め付けられている。触れると冷たい。` — 簡潔で明瞭。
8. **Quartzfur Hat** — **4/5** — `棱晶を散らした肌を死んだ猿から剥ぎ取り、鋸で帽子の形に切り出した。` — 素材感が伝わる。
9. **Tri-Hologram Bracelet** — **4/5** — `桟がバングルの留め金を三本の帯に分け、コバルトのボタンが四本目を召喚する。` — 短く機能的。
10. **Pump Shotgun** — **5/5** — `クロムのリブの下にナッツウッドのフォアエンドが垂れ下がり、ポンプを引けば恍惚のクリックが迸る。` — 武器らしい手触りがある。
11. **Burrowing Claws** — **4/5** — `鍬状の掌と長い爪が土を緩めて掘り、掻き出すように形作られている。` — 用途が直感的。
12. **Dazzle Cheek** — **4/5** — `幾何学が仮面の結晶ダイオードを泳ぎ、入眠幻覚の調べでほほ笑む表情を刻む。` — 表現が鮮やか。
13. **FulleriteFist** — **5/5** — `拳をフラーレン製の塊に置換し、殴打の破壊力を最大まで引き上げる。` — 語彙と用語が安定。
14. **IntravenousPort** — **4/5** — `体内に埋め込んだ多重点滴口。薬剤や滋養を即時循環させる。` — 医療機器として自然。
15. **Leather Whip** — **4/5** — `革をリングノットで編み上げて長い鞭身へとうねらせ、クラッカーの先端には血の染みが付いている。` — 鞭の描写が明瞭。
16. **Smiling Sun Mask** — **5/5** — `太陽が石鹸石に笑みを刻み、赭で飾った頬と歯が高く誇らしげに張り出す。真昼の天幕が落とす影は、台地にもモグラヤイ以北にも存在しないほど葉に満…` — 世界観と地名整合が良い。
17. **Ulnar Stimulators** — **4/5** — `導電メッシュでぴたりと肌に貼り付く手袋が、手を高性能へと電撃で駆り立てる。` — 機能説明が自然。
18. **BaseTierFace4** — **3/5** — `喰らう者たちが自分の顔に縫い付ける術を失ってなお、仮面は生活の中で聖なる地位を保ち続けた。この仮面も例外ではない。` — `喰らう者たち` の複数化がやや気になる。
19. **Steel Hammer** — **4/5** — `戦いで穿たれた棒から、磨かれた鋼の球根がふくらみ出ている。` — 簡潔で自然。
20. **ReactiveTraumaPlate** — **5/5** — `衝撃を受けた瞬間に硬化するトラウマプレート。内臓損傷を防ぐ。` — 簡潔で分かりやすい。

### world-gospels.ja.json samples

1. **<spice.instancesOf.murdered.!random> for *sacredThing*** — **4/5** — `*sacredThing*のために<spice.instancesOf.murdered.!random>` — 語順が自然。
2. **<^.immense.!random> <spice.2Dshapes.!random>** — **4/5** — `<^.immense.!random><spice.2Dshapes.!random>` — 連結前提だが問題は小さい。
3. **company** — **3/5** — `隊` — 文脈次第だがやや限定的。
4. **<^.pinchOf.!random> <spice.adjectives.!random> <^.terrain.General.someIngredients.!random>** — **4/5** — `<spice.adjectives.!random>な<^.terrain.General.someIngredients.!random…` — テンプレ断片として自然。
5. **<spice.cooking.recipeNames.foods.!random>** — **2/5** — `料理名` — `料理名` は仮置き感が強い。
6. **<^.void.!random> of *DimensionSymbol*** — **4/5** — `*DimensionSymbol*の<^.void.!random>` — 構文が自然。
7. **cistern** — **4/5** — `貯水槽` — 明瞭。
8. **moss** — **4/5** — `コケ` — 自然。
9. **sacred** — **5/5** — `聖なる` — 自然。
10. **<^.groupMurdered_By.!random> by** — **3/5** — `<^.groupMurdered_By.!random>で` — 助詞片だけでやや不安。
11. **flux** — **5/5** — `フラックス` — 定着訳で良い。
12. **lost <entity.possessivePronoun> prized *item* when thieves <spice.history.gospels.CommittedWrongAgainstSultan.LateSultanate.!random>** — **4/5** — `盗賊が<spice.history.gospels.CommittedWrongAgainstSultan.LateSultanate.!…` — 語順自然。
13. **to our great <spice.commonPhrases.woe.!random>** — **4/5** — `大いなる<spice.commonPhrases.woe.!random>となった` — 断片として機能。
14. **<spice.instancesOf.groupMurdered.!random> *factionName*** — **4/5** — `*factionName*を<spice.instancesOf.groupMurdered.!random>` — 自然。
15. **silky** — **4/5** — `絹のような` — 自然。
16. **oval** — **5/5** — `楕円形` — 自然。
17. **pistachio** — **5/5** — `ピスタチオ` — 自然。
18. **<spice.history.gospels.LateSultanate.worshipObject.!random> <spice.commonPhrases.worship.!random>** — **4/5** — `<spice.history.gospels.LateSultanate.worshipObject.!random>への<spice.c…` — 自然。
19. **berries** — **4/5** — `ベリー` — 自然。
20. **toasted** — **4/5** — `こんがり焼いた` — 自然。

### messages.ja.json samples

1. **^You gain (\d+) XP[.!]?$** — **4/5** — `あなたは経験値を{0}獲得した` — 自然だが語順に微修正余地。
2. **^Your base (.+?) is (.+?), modified to (.+?)\.\n\nYou may not raise an attribute above 100\.$** — **4/5** — `あなたの{t0}の基本値は{1}で、修正後は{2}だ。 能力値は100を超えて上げられない。` — 説明文として自然。
3. **^(?:The |the |[Aa]n? )?(.+?) (?:falls?|fell) to the ground\.$** — **4/5** — `{0}は地面に倒れた。` — 標準的で自然。
4. **^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stuck[.!]?$** — **4/5** — `{0}は動けなくなった。` — 意味は通る。
5. **^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't) seem to be working!$** — **4/5** — `{0}は機能していないようだ！` — 自然。
6. **^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't) have enough (.+?) to (.+?)\.$** — **4/5** — `{t0}は{t2}するのに十分な{t1}がない。` — 自然。
7. **^You don't have the required ingredient: (.+?)!$** — **4/5** — `必要な材料が足りない: {0}！` — 明瞭。
8. **^(.+?), [Tt]here's a ((?i:gathering|conclave|congregation|settlement|band|flock|society)) of (.+?) and their ((?i:folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan))\.$** — **3/5** — `{0}、{t2}とその{t3}の{t1}がある。` — 地文としてはやや硬い。
9. **^You hit (?:the |a |an )?(.+?) in a vital area[.!]?$** — **5/5** — `{0}の急所に命中した` — 自然。
10. **^No valid targets for (.+?)[.!]?$** — **4/5** — `{0}の有効な対象がない` — 自然。
11. **^You miss!$** — **5/5** — `攻撃は外れた！` — 自然。
12. **^(?:The |the |[Aa]n? )?(.+?) (?:isn't|aren't) working!$** — **4/5** — `{0}は作動していない！` — 自然。
13. **^(.+?) wounds? (.+)\.$** — **4/5** — `{0}が{1}を負傷させた。` — 自然。
14. **^(.+?)'s shot goes wild!$** — **4/5** — `{0}の射撃が逸れた！` — 自然。
15. **^(.+?) hits you (.+?) for (\d+) damage[.!]?(.*)$** — **2/5** — `{0}があなたに{1}命中、{2}ダメージ！{3}` — 格助詞配置が不自然。
16. **^(?:The |the |[Aa]n? )?(.+?) cannot be closed with you in the way(?: to the (.+?))?[.!]?$** — **4/5** — `あなたが邪魔で{0}を閉められない` — 自然。
17. **^(?:The )?(.+) misses you with their (.+?)[.!] \[(.+?) vs (.+?)\]$** — **4/5** — `{0}の{1}は外れた。[{2} vs {3}]` — 戦闘ログとして自然。
18. **^Something (?:critically )?hits (?:the |a |an )?(.+?) with (?:a |an |the )?(.+?)[.!]?$** — **4/5** — `何かが{1}で{0}に命中した` — 自然。
19. **^(.+?), ((?i:someone|somebody|a mysterious person|a child|a woman|a man|a baby|some group|some sect|some organization|some party|some cabal|some group of friends|some group of lovers|people|folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan)) ((?i:gather|come together|habitate together|cluster|assemble|live together)) to ((?i:profane|mock|scorn|violate|blaspheme)) (.+?)\.$** — **3/5** — `{0}、{t1}が{4}を{t3}ために{t2}。` — やや硬いが通る。
20. **^Your mental attack does not affect (.+?)\.$** — **5/5** — `あなたの精神攻撃は{0}に効かない。` — 自然。

## Final verdict

**Ready for L3 testing? — NO**

Blocking reasons:
1. Glossary consistency is not yet clean (`工匠`, `生命の樹チャヴァ`, `モグラヤイ`, `パクス・クランク`).
2. Mod-name consistency still fails for `masterwork`, `spring-loaded`, and `scaled` across the three required files.
3. Spot-check sampling still surfaced visible quality issues (`pest`, `-elseing`, placeholder-ish `料理名`, and at least one awkward combat template).

Recommended next step before L3:
- Do one narrow cleanup pass for the remaining glossary/mod-name mismatches, then rerun this exact audit.
