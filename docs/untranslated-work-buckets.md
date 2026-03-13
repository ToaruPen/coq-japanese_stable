# 未翻訳作業バケット整理

このメモは、`Player.log` に出た `missing key` / `no pattern` を「意味的な塊」で整理したものです。
文字列単位で潰すのではなく、どのゲーム系統をまとめて対応すべきかを判断するための作業メモです。

- 観測ソース: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- 観測時刻: `2026-03-14 08:42 JST` 前後
- 前提: `CharGenLocalizationPatch` は現在 `observable (heuristic)`、`MessageLogPatch` は `no pattern` 観測あり、`HistoricStringExpander` は意図的無効

## 1. Main Menu

対象:
- メインメニューの項目ラベル

代表例:
- `New Game`, `Records`, `Mods`, `Redeem Code`, `Modding Toolkit`, `Credits`

証跡:
- `Player.log:179`

対応単位:
- `MainMenuLocalizationPatch`
- main menu の左右オプション文字列をまとめて辞書追加

## 2. Character Creation - Game Mode / Start Flow

対象:
- ゲームモード選択
- プレイ方式選択
- build summary / customize の導線

代表例:
- `：ゲームモードを選択：`
- `チュートリアル`, `クラシック`, `ロールプレイ`, `放浪`, `デイリー`
- `：プレイ方式を選択：`
- `プリセット`, `新規作成`, `ランダム`, `ビルドライブラリ`, `前回のキャラクター`
- `：ビルドまとめ：`
- `Export Code to Clipboard`, `Save Build To Library`
- `：キャラクターをカスタマイズ：`, `Name: `, `<random>`

証跡:
- `Player.log:196`
- `Player.log:214`
- `Player.log:1050`
- `Player.log:1068`
- `Player.log:1072`

対応単位:
- `CharGenLocalizationPatch`
- char-gen menu / flow labels をひとまとまりで対応

## 3. Character Creation - Genotype / Calling / Subtype

対象:
- genotype 見出しと選択肢
- calling 見出しと subtype 選択肢
- calling ごとの特徴 bullet block

代表例:
- `：ジェノタイプを選択：`
- `突然変異した人間`, `真性人類`
- `Calling`, `:choose calling:`
- `使徒`, `アルコノート`, `老練兵`, `ガンスリンガー`, `マローダー`, `巡礼者`, `遊牧民`, `学者`, `ティンカー`, `ワーデン`, `水商人`, `ウォーターバイン農家`
- `ù +2 Ego ...` / `ù +2 Agility ...` / `ù +2 Strength ...` のような説明ブロック

証跡:
- `Player.log:380`
- `Player.log:390`
- `Player.log:488`
- `Player.log:521`

対応単位:
- `CharGenLocalizationPatch`
- まず calling 名と subtype 名、次に bullet block をまとめて対応

## 4. Character Creation - Mutation Picker

対象:
- mutation category labels
- mutation / defect names
- morphotype / esper/chimera 系の説明文
- point allocation text

代表例:
- `：変異を選択：`
- `モーフタイプ`, `肉体突然変異`, `肉体的欠陥`, `精神突然変異`, `精神的欠陥`
- `エスパー`, `キメラ`, `不安定ゲノム`, `アドレナリン制御`
- `フリル [V]`, `三重関節`, `再生`, `炎線 [V]`
- `アルビノ (D)`, `量子震え (D)`
- `Points Remaining: 12`
- `You only manifest mental mutations...`

証跡:
- `Player.log:701`
- `Player.log:708`
- `Player.log:711`

対応単位:
- `CharGenLocalizationPatch`
- 見出し / mutation names / defects / points UI をまとめて扱う

## 5. Character Creation - Cybernetics Picker

対象:
- cybernetics category label
- implant names
- implant descriptions / stat modifiers

代表例:
- `Cybernetics`
- `光学バイオスキャナ (Face)`
- `皮膚用断熱材 (Body)`
- `暗視システム (Face)`
- `+6 熱耐性`, `+2 DV`, `+2 STR`
- `<none>`

証跡:
- `Player.log:266`
- `Player.log:350`

対応単位:
- `CharGenLocalizationPatch`
- implant 名・説明・補助 stat 行をまとめて対応

## 6. Character Creation - Attribute Allocation / Summary

対象:
- attribute help text
- points remaining counters
- build summary block

代表例:
- `Your Strength score determines ...`
- `Your Agility score determines ...`
- `Points Remaining: 0`
- `Strength: 16 / Agility: 17 / ...`
- `老練兵 / 突然変異した人間 / 人型`

証跡:
- `Player.log:986`
- `Player.log:985`
- `Player.log:1051`

対応単位:
- `CharGenLocalizationPatch`
- attribute help / points / summary を 1 バケットで扱う

## 7. Message Log Pattern Gaps

対象:
- 旅程ログ
- 環境ログ
- 地名や液体名の混在する procedural message

代表例:
- `You embark for the caves of Qud.`
- `On the 2nd of Simmun Ut, you arrive at the village of ...`
- `You pass by a ウォーターヴァイン.`
- `You wade through a 水たまり of sap.`
- `You are stuck in a 水たまり of sap!`

証跡:
- `Player.log:3610`

対応単位:
- `MessagePatternTranslator`
- 文字列追加ではなく pattern 追加で潰すべき塊

## 8. In-Game Menu / Journal / Reputation Residual UITextSkin

対象:
- character sheet / skill tree / journal / reputation screen の sink-only UI
- まだ upstream route が細かく切れていない menu text

代表例:
- `SKILLS`, `ATTRIBUTES && POWERS`, `EQUIPMENT`, `TINKERING`, `JOURNAL`, `QUESTS`, `REPUTATION`, `MESSAGE LOG`
- `Skill Points (SP): 0`
- `Learned [1/7]`
- `Cudgel`, `Endurance`, `Persuasion`, `Wayfaring`
- `The villagers of Agashur don't care about you...`
- `Reputation: 0`

証跡:
- `Player.log:3633`
- `Player.log:3643`
- `Player.log:3598`

対応単位:
- 現状は `UITextSkinTranslationPatch` の残差
- 将来的には upstream patch を増やして分解したい
- 当面は「skills/journal/reputation UI」の塊として扱う

## 9. 明確な blind spot / 対象外

- `HistoricStringExpander` 系: `docs/procedural-text-status.md` のとおり現状は coverage 母集団に入れない
- 疑似グラフィックや単独ショートカットラベル: 観測ノイズとして除外済み

## 10. 次の作業順序

1. Main Menu の未訳 key を辞書追加
2. Char-gen flow labels と genotype / calling / subtype を追加
3. Mutation category / names / defects / points UI を追加
4. Cybernetics 名称と説明ブロックを追加
5. Message log 用の pattern を追加
6. 最後に skills / journal / reputation の residual UITextSkin を upstream 分解または辞書追加
