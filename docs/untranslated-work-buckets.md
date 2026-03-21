# 未翻訳バケット分類メモ

issue-29 の目的は、`Player.log` に残っている未翻訳テキストを
`asset-solvable` と `logic-required` に分離し、次の実装作業が再調査なしで始められる状態にすることです。

- 観測ソース:
  - macOS: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
  - Windows: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log`
  - Linux: `~/.config/unity3d/Freehold Games/CavesOfQud/Player.log`
- 観測時刻: `2026-03-14 08:42 JST` 前後
- 実装根拠:
  - `Mods/QudJP/Assemblies/src/Translator.cs` - `Translator.Translate(...)` は exact-match のみ
  - `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs` - regex/template による pattern 変換
  - `Mods/QudJP/Assemblies/src/Patches/CharGenLocalizationPatch.cs`
  - `Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs`
  - `Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs`
  - `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs`
- 対象外:
  - `HistoricStringExpander` 系は `docs/procedural-text-status.md` のとおり `intentionally disabled`
  - 疑似グラフィックや単独ショートカットラベルは観測ノイズとして除外済み

## 判定ルール

- `asset-solvable`
  - 現在の route で受け取る文字列が安定 key、固定ラベル、または regex/template で吸収できる
  - 次作業は dictionary / XML / pattern 追加で完結する
- `logic-required`
  - 現在の route で最終合成済み文字列や埋め込み値入り文字列を受け取る
  - exact-match key を増やしてもスケールしないため、分解・補間・route 追加が要る

## 実作業の進め方

- `Player.log` は backlog そのものではなく、未訳の「証拠」と「意味的な塊」を見つけるために使う
- 現在の missing-key log は `Route > detail > detail` 形式の context を出す。一次 route は patch 名で維持しつつ、field / itemType / collection / method などの detail を後ろに積む
- fixed label / atomic noun / message-log template のように route 上でそのまま閉じるものだけを log から直接追加する
- `logic-required` に入った時点で、以後の主戦場は `Player.log` ではなく upstream source 調査へ移す
- つまり残差を 1 件ずつ消すのではなく、`GameText.VariableReplace(...)`, `Popup.ShowConversation(...)`, `GetDisplayNameEvent`, `DescriptionBuilder` のような生成点を押さえてまとめて潰す
- `Player.log` の役割は、修正後にどの意味塊がまだ漏れているかを再観測すること
- 通常の未訳観測は手動操作で行い、`scripts/verify_inventory.py` は inventory / equipment 表示検証のためにだけ使う
- 一般的な未訳観測で自動化を使う場合は、char-gen から inventory 各タブまで遷移して `Player.log` を採取できることを前提にし、現行の inventory-only 自動化はそのまま流用しない
- `MainMenuLocalizationPatch` / `CharGenLocalizationPatch` で出る単独ショートカット、checkbox 記号、疑似グラフィック (`[Space]`, `[Esc]`, `[R]`, `[Delete]`, `[ ][n]`, `[■][n]` など) は、原則として観測ノイズ扱いにする

## Route Inventory

| Route | 現在の受け取り方 | いま問題になる理由 | 現時点の判断 |
| --- | --- | --- | --- |
| `CharGenLocalizationPatch` | Postfix で `__result` を `UITextSkinTranslationPatch.TranslatePreservingColors(...)` に流す | char-gen の固定ラベルと、値入り readout / 長文ブロックが同じ route に混在している | 両クラスが混在 |
| `GetDisplayNamePatch` | `GetDisplayNameEvent.GetFor(...)` の返り値を後処理する | 単独名詞と、生成済み称号つき display name が同じ route に入る | 両クラスが混在 |
| `GetDisplayNameProcessPatch` | `GetDisplayNameEvent.ProcessFor(...)` の返り値を後処理する | `GetDisplayNamePatch` と同じく、最終 display name を受け取る | 両クラスが混在 |
| `UITextSkinTranslationPatch` | `UITextSkin.SetText(string)` の sink で色コード除去後に `Translator.Translate(...)` を呼ぶ | 固定 UI ラベルと動的 readout が同じ sink に落ちる | 両クラスが混在 |
| `MessagePatternTranslator` | `MessageLogPatch` から regex/template translation を受ける | raw key 追加ではなく pattern 追加で潰すべき message-log が残っている | 基本は `asset-solvable` |

## `asset-solvable`

### 1. Character creation の固定ラベル・固定候補名

- 主 route: `CharGenLocalizationPatch`
- 性質: 固定フロー文言、固定カテゴリ名、固定候補名、固定 cybernetics 名
- `Player.log` 例:
  - `Player.log:181` - `光学バイオスキャナ (Face)`
  - `Player.log:196` - `<none>`
  - `Player.log:325` - `Stinger (Confusing Venom)`
  - `Player.log:344` - `Choose Variant`
- ノイズとして除外する例:
  - `Player.log` 上の `[R]`, `[Delete]`, `[ ][n]`, `[■][n]`, `[1pts]`, `[2pts]`
- 判断理由:
  - これらは値埋め込みのない安定文字列で、現行 route の exact-match 辞書追加で対処できる
  - `CharGenLocalizationPatch` 自体は final string を受けるが、少なくともこの塊は asset 作業として切り出せる
- 次作業:
  - char-gen 用 dictionary / XML の追補
  - cybernetics 名、mutation variant 名、固定カテゴリ名の follow-up issue 化

### 2. `UITextSkin` に流れている固定 UI ラベル

- 主 route: `UITextSkinTranslationPatch`
- 性質: sink-only だが、文字列そのものは固定ラベル
- `Player.log` 例:
  - `Player.log:2750` - `SKILLS`
  - `Player.log:2754` - `JOURNAL`
  - `Player.log:2755` - `QUESTS`
  - `Player.log:2756` - `REPUTATION`
  - `Player.log:3027` - `Sort Options`
- 判断理由:
  - route は sink でも、文字列は安定 key なので raw entry の追加で吸収できる
  - upstream route 分解は将来の改善候補だが、この塊の解消自体は asset work で進められる
- 次作業:
  - `UITextSkinTranslationPatch` 残差用 dictionary エントリ追加
  - skills / journal / reputation 画面の固定見出しを別 issue へ分離

### 3. Message log の pattern 不足

- 主 route: `MessagePatternTranslator`
- 性質: 内容は procedural でも、message-log route 上では regex/template で吸収できる deterministic message
- `Player.log` 例:
  - `Player.log:2685` - `Notes: Damur`
  - `Player.log:2686` - `You embark for the caves of Qud.`
  - `Player.log:2711` - `On the 5th of Ut yara Ux, you arrive at the village of Damur and fungus patch...`
- 判断理由:
  - これらは raw exact-match key ではなく `MessagePatternTranslator` の regex/template で吸収するのが正しい
  - 同じ内容が後段 sink で `UITextSkinTranslationPatch` にも見える (`Player.log:3070`, `Player.log:3071`) が、分類上の主 route は `MessagePatternTranslator` とみなす
  - 村到着文の長文版は `PopupTranslationPatch` にも出る (`Player.log:2708`, `Player.log:2712`) ため、message-log pattern 追加だけで全文字列の route が閉じるわけではない
  - つまり「到着文は procedural か?」への答えは yes だが、issue-29 の分類では `message-log の到着文 template` と `popup の長文到着文` を分けて扱う
- 次作業:
  - `messages.ja.json` の pattern 追加
  - 旅程ログ、地名付き note、到着メッセージを pattern work として分離
  - popup 側に残る長文到着文は別 route の logic / asset 判断を維持したまま個別に追う

### 4. Atomic display-name terms

- 主 route: `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`
- 性質: 単独の item / creature / role 名として成立している表示名
- `Player.log` 例:
  - `Player.log:480` - `螺旋角`
  - `Player.log:491` - `夢の煙`
  - `Player.log:504` - `奇妙な遺物`
  - `Player.log:545` - `鉄のメイス`
- 判断理由:
  - これらは final display name route に現れているが、文字列自体は atomic で個別訳語を asset として置ける
  - adjective/base-name の再構成が不要な単独名詞は asset 側へ寄せてよい
- 次作業:
  - display-name dictionary の未登録語を補完
  - composed title を除いた単独名詞だけを follow-up に分離

## `logic-required`

### 1. Char-gen counters / numeric readouts

- 主 route: `CharGenLocalizationPatch`
- 性質: 値埋め込み済みカウンタ、属性配分 readout、summary line
- `Player.log` 例:
  - `Player.log:331` - `Points Remaining: 12`
  - `Player.log:354` - `Points Remaining: 8`
  - `Player.log:372` - `Points Remaining: 0`
  - `Player.log:452` - `Strength: 14 ...`
- 判断理由:
  - 値ごとに exact-match key を増やしても終わらない
  - `CharGenLocalizationPatch` が受ける時点で final string になっているため、format-aware translation か分解が必要
- 次作業:
  - counter / summary line を token 化または template 化する patch 方針を別 issue 化

### 2. Char-gen bullet block / 長文説明ブロック

- 主 route: `CharGenLocalizationPatch`
- 補助観測 route: `UITextSkinTranslationPatch`
- 性質: perk bullet block、mutation description、cybernetics 説明などの multiline / mixed fragment text
- `Player.log` 例:
  - `Player.log:217` - `ù +2 Ego ...` (`CharGenLocalizationPatch`)
  - `Player.log:333` - `You have two heads...` (`UITextSkinTranslationPatch`)
  - `Player.log:348` - `You emit a ray of frost...` (`CharGenLocalizationPatch`)
  - `Player.log:381` - `You replenish yourself by absorbing sunlight through your hearty green skin...` (`UITextSkinTranslationPatch`)
- 判断理由:
  - 同じ char-gen 系説明が `CharGenLocalizationPatch` と sink 側の `UITextSkinTranslationPatch` にまたがって観測されており、どちらにせよ一塊の最終文字列として届いている
  - 固定 key 追加だけでは variation を吸収しにくく、block 内の stat 行、説明文、改行構造を分解して再構成する必要がある
- 次作業:
  - bullet / paragraph 単位での分割ポイントを特定する patch work
  - 説明文パーツを structured translation に寄せる follow-up issue / PR task 化

### 3. Display-name composition

- 主 route: `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`
- 性質: 生成名、称号、素材、base name、figurine などが結合済みの display name
- `Player.log` 例:
  - `Player.log:513` - `瑪瑙 手袋屋 のフィギュリン`
  - `Player.log:534` - `スナップジョーの軍主`
  - `Player.log:566` - `Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ`
- 判断理由:
  - 現在の patch は final composed string を受けてから exact-match translation している
  - 既存の adjective / noun 辞書があっても、組み立て後の語順は日本語として再構成が必要
- 次作業:
  - adjective/base-name/title の分解点を持つ display-name patch の追加または既存 patch 拡張
  - figurine / relic / legendary title を composition-aware に扱う別 issue 化

### 4. Dynamic `UITextSkin` residuals

- 主 route: `UITextSkinTranslationPatch`
- 性質: reputation 文、skill point 表示、level/readout、要件行のような sink-only dynamic UI
- `Player.log` 例:
  - `Player.log:2746` - `Level: 1 ¯ HP: 21/21 ¯ XP: 0/220 ¯ Weight: 411#`
  - `Player.log:2986` - `The villagers of Abal don't care about you...`
  - `Player.log:3001` - `The villagers of Dubamor don't care about you...`
  - `Player.log:3082` - `Skill Points (SP): 0`
- 判断理由:
  - 同じ template に地名や数値が埋まるため、raw key 追加は維持不能
  - `UITextSkinTranslationPatch` は sink でしかないので、format-aware handling か upstream route 分解が要る
- 次作業:
  - reputation / stat line / requirement line を route 別に upstream patch へ切り出す
  - それまでの暫定対応をするなら template translation 層を追加する

### 4.1. `skills/powers` の color / markup drift

- 主 route: `UITextSkinTranslationPatch`
- 補助 route: `ColorAwareTranslationComposer`, `TextShellReplacementRenderer`
- 性質: 未訳 leaf ではなく、翻訳後の skill label や description 行で色・markup の保持が崩れる表示回帰
- 2026-03-21 時点の観測:
  - screenshot evidence: skills / powers 画面で `廃品漁り` など一部 skill 名の色が周辺行とずれて見える
  - 同系統の render owner 候補:
    - `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs`
    - `Mods/QudJP/Assemblies/src/ColorAwareTranslationComposer.cs`
    - `Mods/QudJP/Assemblies/src/TextShellReplacementRenderer.cs`
- 判断理由:
  - これは `Melee` / `Melee Weapons` のような未訳 leaf とは別 family であり、辞書追加では閉じない
  - 翻訳結果の text 自体ではなく、色 span / TMP wrapping / replacement render の保持境界を確認する必要がある
- 次作業:
  - `skills/powers` 画面で色崩れする source family を `Player.log` と screenshot で再特定する
  - `UITextSkinTranslationPatch` と `ColorAwareTranslationComposer` のどちらで span ownership が崩れるかを切り分ける
  - 必要なら `TextShellReplacementRenderer` の TMP 設定同期を `skills/powers` 表示に限定して調整する

### 4.5. Stateful liquid-container display names

- 主 route: `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`, `InventoryLocalizationPatch`
- 性質: base item name に、液量・空/半量・液体名などの状態が後段で合成された container 表示
- `Player.log` 例:
  - `Player.log:244` - `水袋（空） [empty]`
  - `Player.log:247` - `水袋（半量） [32 drams of fresh water]`
  - `Player.log:252` - `巡礼者のワイン袋 [9 drams of wine]`
- 判断理由:
  - base game では `HalfFullWaterskin`, `EmptyWaterskin`, `PilgrimWineWaterskin`, `Empty Canteen` などの variant object は存在するが、`DisplayName` は持たず `LiquidVolume` / `InitialLiquid` だけを変えている
  - そのため、状態つき容器名を XML の static `DisplayName` で後付けすると、後段の `[empty]` / `[32 drams of ...]` と二重管理になり、混在表示を起こしやすい
  - この系統は fixed asset ではなく、容器名・状態・液体名・量の合成点を扱う `logic-required` で処理すべき
- 運用ルール:
  - 状態つき liquid container variant に対して static XML の `Render DisplayName` は追加しない
  - 既存の variant override は cleanup 対象として扱う
- 次作業:
  - `empty` / `{n} drams of {liquid}` / 容器ベース名の template 化ポイントを特定する
  - liquid container 系の表示は route-aware template handling に寄せる

### 5. Procedural / generated naming and lore

- 主 route: `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`, 一部 `UITextSkinTranslationPatch`
- 性質: procedural name、generated title、歴史系 naming の出力
- `Player.log` 例:
  - `Player.log:488` - `Sithithythoth`
  - `Player.log:499` - `Iondanna`
  - `Player.log:522` - `Baaraahaaaaah`
- 判断理由:
  - 固定 asset で網羅する対象ではなく、生成パイプラインのどこで日本語化するかを先に決める必要がある
  - `HistoricStringExpander` 自体は現状 blind spot なので、この分類では「現在 observable な generated name が display-name route に漏れてきた部分」を logic-required とみなす
- 次作業:
  - generated naming を扱う route の切り分け
  - playability を壊さない再導入条件は `docs/procedural-text-status.md` を参照

## Follow-Up Split

## 2026-03-21 Active Implementation TODO

- scope
  - `shrine/history` の自動生成文は今回の作業スコープから除外する
- reputation
  - `FactionsStatusScreenTranslationPatch` の自己再入と repeated translation をさらに減らす
  - `FactionsLine` の `detailsText/detailsText2/detailsText3` を評判タブ幅の中で自動折り返しにする
  - 未対応 topic leaf (`ruins`, `Girsh lairs`, `sultan tomb inscriptions`, `Resheph's healer Rebekah`, `workshop`) を dedicated route で閉じる
- character status / mutation
  - `Mutated Human Tinker` のような genotype + calling title を dedicated route で処理する
  - `Level: n ¯ HP: a/b ¯ XP: c/d ¯ Weight: w#` の上段 status 行を 1 family として処理する
  - live mutation detail の `description + This rank + Next rank` を dedicated route で閉じる
  - 属性 help の color / markup 崩れを dedicated help family で直す
- chargen
  - mutation long description (`sleep gas`, `electromagnetic pulse`, `nearsighted`) を `description + rank text` family で閉じる
  - 既に改善済みの raw bullet / `Points Remaining` を回帰させない
- compare / status sink
  - `Strength Bonus Cap: {0}` を template family にする
  - `Weapon Class: ...` を template / exact family にする
  - `Perfect`, `Injured`, `Hostile`, `Average` の compare/status 行を sink family で閉じる
- skills / powers
  - `Melee`, `Melee Weapons` の section label を閉じる
  - `廃品漁り` など skills / powers 行の color / markup drift を、未訳 leaf とは別 family として調査・修正する
  - skill line translation で元の indent / visual hierarchy を落とさない
- popup / message
  - `Enter 送信`, `Esc キャンセル`, `Tab 長押しで決定`, `続ける` の localized no-op を徹底する
  - `MessagePatternTranslator` で article-aware pattern と brace / color balance を維持する
  - death wrapper family は `popup の個別 exact/template` と `message regex` の寄せ集めとして増やさず、`wrapper cause + killer slot` の shared family として再設計する
  - upstream family の owner は `message assembly` とみなし、`PopupTranslationPatch` と `MessagePatternTranslator` は同じ death family を使う route adapter として整理する
  - `killed by` だけでなく `bitten to death by` と `accidentally killed by` を同じ death wrapper TODO に含める
  - article 処理 (`a/an/the`) と killer 名の display-name reuse は death wrapper family の共通責務として 1 箇所に寄せる
  - 対象 family:
    - `You see a ... and stop moving.`
    - `The ... stands up.`
    - `You died ... killed by a ...`
    - `You died ... bitten to death by a ...`
    - `You were accidentally killed by ...`
    - `You hit ...`
    - `You miss ...`
    - `(.+?) yells, '...'`
- verification
  - 対象 L1/L2 テスト
  - `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
  - `/Users/toarupen/.local/bin/python3.12 scripts/sync_mod.py`
  - Rosetta での L3 実確認

### Asset implementation tasks

1. `CharGenLocalizationPatch` の固定ラベルと固定候補名を追加する
   - 対象: `光学バイオスキャナ (Face)`, `<none>`, `Stinger (Confusing Venom)`, `Choose Variant`
   - 根拠: `Player.log:181`, `Player.log:196`, `Player.log:325`, `Player.log:344`
   - 作業種別: dictionary 追加
2. `UITextSkinTranslationPatch` に残っている固定 UI ラベルを追加する
   - 対象: `SKILLS`, `JOURNAL`, `QUESTS`, `REPUTATION`, `Sort Options`, `MESSAGE LOG`
   - 根拠: `Player.log:2750`-`Player.log:2757`, `Player.log:3027`
   - 作業種別: dictionary 追加
3. `MessagePatternTranslator` の pattern 不足を埋める
   - 対象: `Notes: <place>`, `You embark for the caves of Qud.`, message-log 側の village-arrival template
   - 根拠: `Player.log:2685`, `Player.log:2686`, `Player.log:2711`
   - 作業種別: `messages.ja.json` への regex/template 追加
   - 注意: popup 長文 (`Player.log:2708`, `Player.log:2712`) はこの task に含めない
4. `GetDisplayNamePatch` / `GetDisplayNameProcessPatch` で見えている atomic display-name を補完する
   - 対象: `螺旋角`, `夢の煙`, `奇妙な遺物`, `鉄のメイス`
   - 根拠: `Player.log:480`, `Player.log:491`, `Player.log:504`, `Player.log:545`
   - 作業種別: display-name dictionary 追加

### Logic investigation tasks

1. char-gen counters / summary readouts の upstream を特定する
   - 対象: `Points Remaining: 12`, `Strength: 14 ...`
   - 根拠: `Player.log:331`, `Player.log:452`
   - 調査起点: `CharGenLocalizationPatch`, `UITextSkinTranslationPatch.ResolveObservabilityContext(...)`
2. char-gen bullet block / long descriptions の組み立て地点を切り分ける
   - 対象: `You have two heads...`, `You emit a ray of frost...`
   - 根拠: `Player.log:333`, `Player.log:348`, `Player.log:381`
   - 調査起点: `CharGenLocalizationPatch`, `UITextSkinTranslationPatch.ResolveObservabilityContext(...)`
3. composed display name の builder 分解点を洗う
   - 対象: `瑪瑙 手袋屋 のフィギュリン`, `Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ`
   - 根拠: `Player.log:513`, `Player.log:566`
   - 調査起点: `GetDisplayNameEvent.ProcessFor`, `DescriptionBuilder`
4. dynamic `UITextSkin` residuals の upstream route を棚卸しする
   - 対象: `Level: 1 ¯ HP: 21/21 ...`, `Skill Points (SP): 0`, reputation 文
   - 根拠: `Player.log:2746`, `Player.log:2986`, `Player.log:3082`
   - 調査起点: `Popup.ShowConversation(...)`, `ConversationUI.Render`, `Look.GenerateTooltipContent`, `GameText.VariableReplace`
   - 補足: `Skill Points (SP): 0` や `Attribute Points: 0` は `{val}` / `{0}` 系辞書資産と対応しており、`=...=` 系の `GameText` token family とは別ルートの可能性が高い
5. generated naming / lore outputs の生成経路を整理する
   - 対象: `Sithithythoth`, `Iondanna`, `Baaraahaaaaah`
   - 根拠: `Player.log:488`, `Player.log:499`, `Player.log:522`
   - 調査起点: `GetDisplayNameEvent`, `HistoricStringExpander` 周辺の blind spot

## 2026-03-21 Death Wrapper Family Refactor TODO

- family boundary
  - `death wrapper` は「一般動詞活用」ではなく `wrapper cause + killer slot` family として扱う
  - canonical family は `message assembly` 起点で定義し、`PopupTranslationPatch` と `MessagePatternTranslator` は同一 family の route adapter にする
  - 最初に管理する subfamily は `killed by`, `bitten to death by`, `accidentally killed by`
- implementation refactor
  - `PopupTranslationPatch` の `TryTranslateDeathPopup` / `TryTranslateBittenToDeathPopup` の重複を shared helper へ寄せる
  - killer 名の article 剥がし (`a/an/the`) と display-name route 再利用は death wrapper family 共通 helper に寄せる
  - popup 側の fixed template (`ui-popup.ja.json`) と message 側の regex/template (`messages.ja.json`) は別 asset のままでもよいが、family 定義と slot handling は 1 箇所に集約する
  - `ui-messagelog.ja.json` にだけ存在する `You were accidentally killed by {attacker}.` を death wrapper inventory に含め、popup/message 間で取りこぼす variant を洗う
- characterization tests
  - L1: death wrapper family ごとに `killer slot`, `article 有無`, `句点/感嘆符揺れ`, `already localized killer` を固定する
  - L2: popup route で `killed by` と `bitten to death by` の両方が shared helper を通って訳される回帰を追加する
  - L2: message route で `killed by`, `bitten to death by`, `accidentally killed by` が同じ family inventory に従う回帰を追加する
  - L2: popup と message で同じ killer source を与えたとき、killer 名の訳し方と article 除去が一致することを固定する
- evidence pointers
  - production:
    - `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs`
    - `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs`
    - `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
    - `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json`
    - `Mods/QudJP/Localization/Dictionaries/ui-messagelog.ja.json`
  - policy / upstream:
    - `docs/logic-required-policy.md`
    - `docs/ilspy-analysis.md`
    - `.sisyphus/notepads/coq-jp-roadmap/decisions.md`
    - `.sisyphus/evidence/task-9-Qud.UI.PopupMessage.cs`

### Investigation references

- upstream 調査の出発点は `docs/ilspy-analysis.md` を使う
- `HistoricStringExpander` の扱いは `docs/procedural-text-status.md` を維持する

## このメモで固定したこと

- 未翻訳バケットは `asset-solvable` と `logic-required` に分類した
- issue-29 で要求された route (`CharGenLocalizationPatch`, `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`, `UITextSkinTranslationPatch`, `MessagePatternTranslator`) をすべて明示した
- 具体例は current `Player.log` の行番号つきで記録した
- asset / logic の切り分けと主要 route の一次整理は完了した
