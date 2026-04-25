# Issue #405 — Residual untranslated entries: Books.jp.xml Title + Subtypes.jp.xml extrainfo

## Why

QudJP の翻訳カバレッジは現状 ~98.9% で残存未訳は約 31 件 (issue 列挙の sporadic 4 件は調査の結果 intentional pass-through と判定)。**書名と character creation 画面の extrainfo は player 直接表示** のため、Steam 公開水準の polish として完訳する。

Books.jp.xml の `Title` 属性は `Qud.UI.BookScreen` の `titleText.SetText(Book.Title)` で直接表示され、QudJP の `BookScreenTranslationPatch` を通る前に XML 値が既に raw English / 内部 ID になっている。base-game の `Books.xml` も同じ raw ID を持つため英語版プレイヤーも同じ表示になるが、issue maintainer は Japanese build の polish のため完訳希望。

Subtypes.jp.xml の `<extrainfo>` 要素は character creation 画面で subtype 説明として表示される。同 file 内の他 24 subtype は既に日本語化済で、Arconaut の 1 entry のみ残っていた。

## What

### データ修正 — 計 31 件 / 2 ファイル

| File | 種別 | 件数 |
| --- | --- | ---: |
| `Mods/QudJP/Localization/Books.jp.xml` | Title 属性日本語化 | 30 |
| `Mods/QudJP/Localization/Subtypes.jp.xml` | extrainfo 日本語化 | 1 |

### 1. Books.jp.xml — 20 件の wrapped 英語タイトル日本語化

`Title="{{W|<English>}}"` を `Title="{{W|<Japanese>}}"` に置換。`{{W|...}}` wrapper は維持。すべて Codex 諮問 + Wiki + 既存翻訳本文との照合で確定。

| Line | ID | 旧 (English) | 新 (Japanese) |
| ---: | --- | --- | --- |
| 5 | DagashasSpur | `Dagasha Remembers` | `ダガーシャは覚えている` |
| 17 | FearinBeyLah | `A Tale of Fear in Bey Lah` | `ベイ・ラーの恐怖譚` |
| 141 | LoveinBeyLah | `A Tale of Love in Bey Lah` | `ベイ・ラーの恋愛譚` |
| 294 | CyberIntro | `125810239481203-41023` | `125810239481203-41023` (数字列維持) |
| 557 | Skybear | `Song of the Sky-Bear` | `天空の熊の歌` |
| 578 | MimicandMadpole | `The Mimic and the Madpole` | `ミミックとマッドポール` |
| 598 | TeleporterOrbs | `The Girl in the Sky` | `空の娘` |
| 620 | Sonnet | `a crumpled sheet of paper` | `くしゃくしゃの紙片` |
| 642 | DarkCalculus | `On the Origins and Nature of the Dark Calculus` | `暗き微積の起源と本質` |
| 658 | CrimeandPunishment | `Crime and Punishment` | `罪と罰` |
| 664 | AphorismsAboutBirds | `Aphorisms about Birds` | `鳥についての箴言` |
| 702 | Animals | `On Humanoid Mimicry of Animals and Plants` | `ヒューマノイドによる動植物模倣について` |
| 716 | EntropytoHierarchy | `From Entropy to Hierarchy` | `エントロピーからヒエラルキーへ` |
| 735 | BloodstainedSheaf | `A sheaf of bloodstained, goatskin parchment` | `血染めの山羊皮紙の束` |
| 755 | Sheaf1 | `A sheaf of tattered parchment` | `ぼろぼろの羊皮紙の束` |
| 783 | TornGraphPaper | `a sheet of torn graph paper` | `破れた方眼紙` |
| 908 | Corpus | `Corpus` | `コルプス` |
| 970 | Across1 | `Across Moghra'yi, Vol. I: The Sunderlies` | `モグラヤイを越えて 第1巻：サンダリーズ` |
| 1371 | Across2 | `Across Moghra'yi, Vol. II: Athenreach` | `モグラヤイを越えて 第2巻：アセンリーチ` |
| 1402 | Across3 | `Across Moghra'yi, Vol. III: Oth, the Free City` | `モグラヤイを越えて 第3巻：自由都市オース` |

CyberIntro は数字列 lore タイトル (sci-fi 端末/分類番号風) なので英語のまま (`{{W|125810239481203-41023}}` → `{{W|125810239481203-41023}}` で実質変化なし)。**英語維持を明示的に文書化** するため spec に列挙するが、edit 操作は不要。

実 edit は **19 件** (CyberIntro を除く)。

### 2. Books.jp.xml — 10 件の raw 内部 ID を日本語化

`Title="<RawID>"` を `Title="{{W|<Japanese>}}"` に置換。**`{{W|...}}` wrapper を新たに追加** することで他の翻訳済 Title と表示揃え。base-game も raw ID を持つため英語版動作とは divergence するが、これは issue maintainer の意図する Japanese polish 改善。

| Line | ID = 旧 Title | 新 Title |
| ---: | --- | --- |
| 42 | `HighSermon` | `{{W|シェキーナの大説教}}` |
| 135 | `Klanq` | `{{W|クランク！}}` |
| 153 | `Preacher1` | `{{W|銀の父祖への賛美}}` |
| 180 | `Preacher2` | `{{W|腐蝕したベテルへの戒め}}` |
| 216 | `Preacher3` | `{{W|聖なる結合}}` |
| 249 | `Preacher4` | `{{W|啓発の儀}}` |
| 279 | `TemplarDomesticant` | `{{W|ニューファーザーへの呼び声}}` |
| 354 | `AlchemistMutterings` | `{{W|錬金術師のぶつぶつ}}` |
| 375 | `Quotes` | `{{W|引用集}}` |
| 1900 | `EndCredits` | `{{W|制作クレジット}}` |

### 3. Subtypes.jp.xml — 1 件の extrainfo 日本語化

`Mods/QudJP/Localization/Subtypes.jp.xml` L200 (Arconaut subtype):

```xml
<removeextrainfo>Starts with random junk and artifacts</removeextrainfo>
<extrainfo>Starts with random junk and artifacts</extrainfo>
```

`<removeextrainfo>` は base-game 英語と byte-equal 必須なので維持。`<extrainfo>` のみ日本語化:

| Line | 旧 | 新 |
| ---: | --- | --- |
| 200 | `<extrainfo>Starts with random junk and artifacts</extrainfo>` | `<extrainfo>ランダムなガラクタとアーティファクトを所持して開始</extrainfo>` |

「〜を所持して開始」は同 file 内の既存訳例 (L288 `ランダムなアーティファクトとスクラップを所持して開始`、L317 `交易品を所持して開始` 等) と一致。`junk` (ガラクタ) と `scrap` (スクラップ) を区別。

### Out of scope

#### Sporadic 4 件 — Codex 諮問 + decompiled route 評価で intentional pass-through 確定

- `world-effects-status.ja.json:247-249` `-{0} Quickness` (`XRL.World.Effects.ITimeDilated.Details`)
  - **判定**: pass-through。同 file 内の他 entry は `Quickness` を英語のまま固定 (例 L309 `+{0} Quickness\nまもなく鈍重になる。`)。stat 名固定の慣習。
- `ui-displayname-adjectives.ja.json:1728-1730` `VISAGE` (`GetDisplayName.Identity`)
  - **判定**: pass-through。Base XML の shader/identity マーカー、表示は別経路。decompiled source に `"VISAGE"` の string literal なし。
- `ui-journal.ja.json:208-220` `TerrainJoppa` / `TerrainKyakukya` / `TerrainPalladiumReef 3l` (`JournalLineData`)
  - **判定**: pass-through。`JournalLineData.mapTarget` の terrain blueprint 検索用 ID。表示は `entry.GetDisplayText()` 側で別経路。

これら 4 件は **本 PR で touch しない**。spec の検証ステップで現状維持を確認。

#### Subtypes.jp.xml の他 4 件

issue が列挙していた `L287, L314, L315, L332` は調査の結果すべて `<removeextrainfo>` 要素 (XML merge 命令で base 英語と byte-equal 必須) であり `<extrainfo>` ではない。issue 起票時の audit subagent が要素種別を取り違えて誤計上したと判断。修正対象外。

#### CyberIntro 数字列タイトル

`{{W|125810239481203-41023}}` は sci-fi 端末/分類番号風の lore タイトル。日本語添字 (例: `{{W|125810239481203-41023号}}` 等) の付与は意図的回避。原文どおり数字列で表示するのが lore 美学に沿う。修正対象外 (該当 entry は spec の照合対象として列挙のみ)。

#### Saad Amus / サアド の表記揺れ

調査中、`Saad Amus` の Japanese 表記が `サード` (Books.jp.xml 本文) と `サアド` (Conversations.jp.xml) で揺れていることを発見したが、**本 issue #405 のスコープ外** (#406 で扱った cross-Scoped skill 名と類似の cross-file term inconsistency)。別 issue として起票検討すべき follow-up。

## How

1. Books.jp.xml の 19 wrapped 英語タイトルを Edit ツールで日本語化 (CyberIntro は変更不要)。
2. Books.jp.xml の 10 raw ID を `{{W|<Japanese>}}` 形式に置換。
3. Subtypes.jp.xml の L200 extrainfo を日本語化。
4. XML parse 検証 (`xmllint --noout`)。
5. `validate_xml.py --strict` を再実行。
6. 既存テストスイート全通過確認。
7. テスト contract の追加なし — XML attribute 翻訳の機械検証は #409 (CI gate) のスコープ。

タスク分割：

- Task 1: Books.jp.xml の 19 wrapped 英語タイトル日本語化
- Task 2: Books.jp.xml の 10 raw ID 日本語化 + `{{W|...}}` wrapper 追加
- Task 3: Subtypes.jp.xml L200 の extrainfo 日本語化
- Task 4: 全リポジトリ verification

## Verification

```bash
xmllint --noout Mods/QudJP/Localization/Books.jp.xml
xmllint --noout Mods/QudJP/Localization/Subtypes.jp.xml
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

すべて green でなければならない。

## Risks

- **書名翻訳の文学的判断**: 30 件の Title はすべて Codex 諮問 + Wiki 照合 + 既存翻訳本文との整合確認を経て確定。固有名詞は本文で確立した日本語表記 (`モグラヤイ`, `ベイ・ラー`, `ヒンドレン`, `天空の熊`, `アセンリーチ`, `オース`) に従っており、player には本文と一貫した名前で見える。
- **Raw ID 10 件の base-game divergence**: base-game では `Title="HighSermon"` 等 raw ID を表示するため、Japanese build は base 動作と異なる。これは intentional polish。`BookScreenTranslationPatch` は context `BookScreen.TitleText` で介入できるが、現状 dictionary 経路ではなく XML 直書きで対応する方針 (issue maintainer の指示「Books.jp.xml の Title が全て日本語化」)。
- **CyberIntro 数字列維持の判断**: 推奨訳「数字列のまま」は lore 美学だが、UX 観点で player が混乱する可能性。Codex 諮問でも数字添字なし維持を推奨。本 PR は推奨どおり。
- **将来の base-game 更新時の merge conflict**: base-game の Books.xml が更新されて新規 Title が追加されると、raw ID が QudJP overlay にない場合は base 表示にフォールバック。本 PR で日本語化した既存 ID は影響を受けない。

## References

- Codex 諮問: 各 Book の本文テーマに合った日本語タイトル提案 + sporadic 4 件の route 判定
- Caves of Qud Wiki:
  - [Across Moghra'yi, Vol. I: The Sunderlies](https://wiki.cavesofqud.com/wiki/Across_Moghra'yi,_Vol._I:_The_Sunderlies)
  - [Bey Lah](https://wiki.cavesofqud.com/wiki/Bey_Lah)
  - [The Mimic and the Madpole](https://wiki.cavesofqud.com/wiki/The_Mimic_and_the_Madpole)
  - [Saad Amus, the Sky-Bear](https://wiki.cavesofqud.com/wiki/Saad_Amus,_the_Sky-Bear)
  - [Corpus Choliys](https://wiki.cavesofqud.com/wiki/Corpus_Choliys)
  - [Dagasha](https://wiki.cavesofqud.com/wiki/Dagasha)
- Issue #405 全文 (parent: #400)
- 既存翻訳本文 (Books.jp.xml の現状日本語化済 26 件) を canonical lore 表記の参照源として活用
