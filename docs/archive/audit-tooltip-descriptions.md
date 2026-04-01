# Tooltip Description Quality Audit

## Scope

- Targeted only `Short` attributes in:
  - `Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml`
  - `Mods/QudJP/Localization/ObjectBlueprints/Furniture.jp.xml`
  - `Mods/QudJP/Localization/ObjectBlueprints/Foods.jp.xml`
  - `Mods/QudJP/Localization/ObjectBlueprints/HiddenObjects.jp.xml`
- Cross-checked against:
  - `docs/glossary.csv`
  - Local base-game XML under `.../StreamingAssets/Base/ObjectBlueprints/*.xml`

## Summary statistics

- Total checked: **1,516** `Short` attributes
  - `Items.jp.xml`: 824
  - `Furniture.jp.xml`: 283
  - `Foods.jp.xml`: 150
  - `HiddenObjects.jp.xml`: 259
- High-confidence issue counts below are **occurrence-based** and **overlap by category**.

| Category | High-confidence occurrences | Notes |
|---|---:|---|
| 誤訳 / 意味崩壊 | 24 | Mostly concentrated in `Furniture.jp.xml` and a few lore-heavy `HiddenObjects` / `Items` rows. |
| 機械翻訳痕跡 / 不自然な日本語 | 19 | Strongest cluster in early `Furniture.jp.xml` art/shrine descriptions. |
| プレースホルダー / レイアウト破損 | 18 | `Items.jp.xml` contains 17 likely broken `*スルタン*` token rows plus 1 broken `=name=`/`&#10;` row. |
| カラータグ / 色付きフォーマット問題 | 3 | `SunderGrenade1-3` expose raw `&C` plus large spacer runs. |
| 固有名詞 / 用語不統一 | 33 | `Eaters`/`イーター`/`喰らう者`, `Qas`/`カス`, `Watervine`, `Spray-a-Brain`, `Very Old Bird`, `Moghra'yi`. |
| `{{...}}` バランス破損 | 0 | No unmatched double-brace color tags detected in the audited `Short` rows. |

### File-level takeaways

- `Furniture.jp.xml` has the densest MT damage and meaning collapse.
- `Items.jp.xml` has the largest concentration of formatting / dynamic-token risks.
- `HiddenObjects.jp.xml` has fewer rows overall, but several marquee lore descriptions contain untranslated English or severe phrase-level distortion.
- `Foods.jp.xml` did **not** surface any high-confidence top-tier issues in this pass.

## Critical findings

| File | Line | Object | Current Japanese | Issue | Category | Suggested Fix |
|---|---:|---|---|---|---|---|
| `Furniture.jp.xml` | 7 | `Sewing Machine` | 傷のある金属 C の内側で、針とボビンが本縫いに一対の糸を打ち込む準備をする。 | `metal C` を文字の `C` として処理しており、C字型の金属枠という意味が崩れている。 | 誤訳 / MT痕跡 | `傷だらけのC字形の金属枠の内側で…` 方向に再訳。 |
| `Furniture.jp.xml` | 56 | `Light Sculpture` | 青色レーザー光が参照ビームから飛び出し、軽旋盤で彫刻された波面を再現する。 | `light lathe` を `軽旋盤` と誤訳。光学的な文脈が消えている。 | 誤訳 | `光学旋盤` / `光の旋盤` を使って文全体を再訳。 |
| `Furniture.jp.xml` | 87 | `Unfinished Sculpture` | …物質的な重さで半分アイデアを前かがみにしてしまう。 | `half-idea` が `半分アイデア` になっており、日本語として破綻。 | 機械翻訳痕跡 | `半ば形になりかけた着想を素材の重みがうなだれさせている` 方向へ。 |
| `Furniture.jp.xml` | 131 | `Mushroom Case` | …それらはエラの一種だ。 | `They are an arcology of gills.` の比喩を失い、「エラの一種」という意味不明文に崩れている。 | 誤訳 | `そこは鰓のアーコロジーそのものだ` など比喩を保持。 |
| `Furniture.jp.xml` | 173 | `Hammered Dulcimer` | 台形にヌーガットの弦が張られている。 | `gnu gut` を `ヌーガット` と誤読。別語になっている。 | 誤訳 | `ヌーの腸で作った弦` / `ガット弦` に修正。 |
| `Furniture.jp.xml` | 217 | `Hookah` | クリスタルの水パイプからは喫煙用の茎が咲く。 | `stem` を植物の `茎` と誤訳。器具の管・吸い口のこと。 | 誤訳 | `喫煙管` / `吸い口の管` に直す。 |
| `Furniture.jp.xml` | 498 | `AgolgotShrine` | 中央の顎から発芽したエッチングされた嚢胞… | `central chine` を `中央の顎` と誤解。後続も MT 的に崩壊。 | 誤訳 / MT痕跡 | 背骨・背筋のイメージを保って文全体を再訳。 |
| `Furniture.jp.xml` | 507 | `BethsaidaShrine` | …半神の精力を高める様子を石彫刻家が描いている。 | `quickening` の処理が不適切で、荘厳な造形描写が俗っぽく崩れている。 | 誤訳 | `胎動 / 活性化 / 躍動` の方向で文全体を組み直す。 |
| `Furniture.jp.xml` | 516 | `RermadonShrine` | …詩人がマッコムのパイプから泡状の塩水を打ち出して… | 構文関係が壊れており、日本語として誰が何をしたのか追いにくい。 | 機械翻訳痕跡 | `マッコムの管から泡立つ塩水を打ち出し…` と関係を再構築。 |
| `Furniture.jp.xml` | 525 | `ShugruithShrine` | 伝説の 19 の目…獣の縞模様の下着を覆っている。 | `hundred-nine` を `19`、`undergirth` を `下着` と誤訳。意味が大きく損なわれる。 | 誤訳 | `109の目`、`腹下 / 下腹部` などへ修正。 |
| `Furniture.jp.xml` | 534 | `QasShrine` | …Qas による空間、時間、山、村の正しさ… | `Qas` が本文だけ英語のまま。DisplayName は `ギルシュ・カスの神殿` で内部不統一。構文も崩れている。 | 固有名詞不統一 / MT痕跡 | `カス` に統一し、`rightening` を `正し直し / 秩序立て` 方向で再訳。 |
| `Furniture.jp.xml` | 543 | `QonShrine` | …不確実性のインキュバス… | 逐語訳が強く、日本語として不自然で神話的な格調も落ちている。 | 機械翻訳痕跡 | 文全体を詩的な日本語へ再構成。 |
| `Furniture.jp.xml` | 1946 | `Life Gate W` | …ヤシの葉がピラミッドを星条旗の空に持ち上げ… | `star-spangled sky` を `星条旗の空` と誤訳。さらに `frieze` / `cud` 周辺も崩れている。 | 誤訳 | `星散る空` とし、残りも帯状装飾・反芻塊の文脈で全面再訳。 |
| `Furniture.jp.xml` | 2401 | `Clockthing_QGirl` | …タイムが観測者から不正行為を行っていた… | `time cheating the observer` を「不正行為」と処理しており意味が崩壊。 | 誤訳 | `時のずれが観測者を惑わせていた` 方向へ。 |
| `Furniture.jp.xml` | 4531 | `Woven Basket` | マーシュリードは縦糸と横糸で手交雑され… | `hand-woven` を `手交雑` としており完全な誤訳。 | 誤訳 | `手で編まれ` に直す。 |
| `HiddenObjects.jp.xml` | 7 | `Ehalcodon` | …その TX ガラスの肌の下で… | `tx-glass` が `TX ガラス` のまま残り、全体も MT 臭が強い。 | 誤訳 / 未訳英語 | `txガラス` / 既存用語へ統一し、文全体を再訳。 |
| `HiddenObjects.jp.xml` | 57 | `BaseEaterGreatMachineShrine` | …消火栓を充填して演算された意志を操り… | `hydrants` を `消火栓` と直訳。文脈上は機械的な弁・噴出口に近い。 | 誤訳 | `給水弁 / 噴出口 / 水栓` 方向で再訳。 |
| `HiddenObjects.jp.xml` | 937 | `StarshipBookshelf W` | 雨を浴びたベリル世界のオーク板を bulkhead に釘打ちした。 | common noun の `bulkhead` が未訳で残存。 | 未訳英語 | `隔壁` に統一。 |
| `HiddenObjects.jp.xml` | 944 | `StarshipBookshelf E` | 雨を浴びたベリル世界のオーク板を bulkhead に釘打ちした。 | W 側と同じ未訳 common noun。 | 未訳英語 | `隔壁` に統一。 |
| `HiddenObjects.jp.xml` | 1622 | `Mechanical Golem` | …中心質には =hamsa.an= の砕けた印章が… | `center mass` を `中心質` としており不自然。トークンは保持されているが周辺の日本語が硬直している。 | 不自然な日本語 / MT痕跡 | `胴の中央には…` など自然な語に置換しつつトークンは保持。 |
| `Items.jp.xml` | 7402 | `Hologram Bracelet` | …&#10;「=name= 本人だと思うか？ ああ、本物さ。」 | `Short` 内に `&#10;` が入り、展開されない `=name=` が露出する。 | プレースホルダー / レイアウト破損 | セリフ部を削るか Long へ移し、`Short` は1文にとどめる。 |
| `Items.jp.xml` | 5265 | `SunderGrenade1` | …            &C構造物に2d100+20ダメージ。           &C非構造物に2d10+4ダメージ。 | raw `&C` と大量空白が `Short` に露出。表示崩れリスクが高い。 | カラータグ / レイアウト破損 | `{{C|...}}` で包むか、色を外して空白を削る。 |
| `Items.jp.xml` | 3368 | `Fist of the Ape God` | …使い手こそ化石伴侶であり秘密の共有者なのだと告げる。 | 原文末尾の所有関係と比喩 (`=pronouns.possessive= fossil-mate`) が落ち、意味が圧縮されすぎている。 | 誤訳 / 意味欠落 | 所有関係を保ちつつ比喩を再構成。 |
| `Items.jp.xml` | 7872 | `Sandals of the River-Wives` | …頁岩の割れ目へ mystai を連れて行き… | `mystai` が未訳で残存し、`moved with` が `連れて行き` に変質している。 | 未訳英語 / 誤訳 | `mystai` を訳注候補化し、関係は `mystaiとともに移り` 方向へ。 |
| `Items.jp.xml` | 7949 | `Grease Boots` | …底にはEatersの墓所の油圧プレスから… | glossary の `喰らう者` と不一致。 raw English のまま残っている。 | 固有名詞不統一 | `喰らう者の墓所` に統一。 |
| `Items.jp.xml` | 9307 | `BaseTierHands1_AV` | …*スルタン*の時代には紳士淑女の嗜みとなった。 | `*sultan*` 系の動的トークン名を日本語化しており、同じ `*...*` 構文を保持している他行と不一致。 | プレースホルダー破損（要実機確認） | token 名を英語キーへ戻すか、動的 token を使わない文へ書き換える。 |
| `Items.jp.xml` | 9351 | `BaseTierBody2_DV` | …*スルタン*の頃にはそれが晴れ着と呼ばれ… | 同上。加えて `イーター` も glossary の `喰らう者` と不一致。 | プレースホルダー破損（要実機確認） / 固有名詞不統一 | `*sultan*` 系を見直し、`イーター` を `喰らう者` に統一。 |
| `Items.jp.xml` | 9384 | `BaseTierBack2` | この外套は、初期のEatersの墓所から盗み取った… | raw English `Eaters` が残存。 glossary と不一致。 | 固有名詞不統一 | `初期の喰らう者たちの墓から…` 方向へ。 |
| `Items.jp.xml` | 9430 | `BaseTierBody3_AV` | …*スルタン*の時代にはそのスラグが採掘され… | `*スルタン*` 問題の別系列。 same pattern が多数ある。 | プレースホルダー破損（要実機確認） | 系列全体を token 単位で再確認。 |
| `Items.jp.xml` | 10687 | `Minstrel's Token` | 古い白金のシェケルに Very Old Bird の図像が刻まれている。 | 固有名詞が英語のまま残存。 | 未訳英語 / 固有名詞不統一 | repo 内の正式表記を決め、glossary へ追加して統一。 |

## Pattern-level findings

### 1. `Furniture.jp.xml` に MT 由来の崩れが集中

- とくにファイル前半の調度品・神殿群で、以下の傾向がまとまって出ている。
  - 詩的比喩の逐語訳
  - 英語の語義選択ミス (`light` → `軽`, `stem` → `茎`, `gnu gut` → `ヌーガット`)
  - 日本語として未整理な名詞連鎖
  - 荘厳な文章なのに説明的・口語的に崩れる
- この領域は単語単位の修正より、**文単位の再訳**が必要。

### 2. `Items.jp.xml` の `*sultan*` 系は 17 行で一括確認が必要

- 監査範囲内で `*スルタン*` を含む `Short` は **17件**。
- 同じ `*...*` 構文は `*material*`, `*creature.an*` など英語キーのまま維持されており、`*スルタン*` だけ日本語化されているのは不自然。
- 実機で展開確認していないため断定は避けるが、**高確率の token 破損候補**として扱うべき。

### 3. raw 英語の残り方に一貫性がない

- ブランド名・固有名詞・common noun が混在して残っている。
  - `Qas` は本文だけ英語、DisplayName は `カス`
  - `Watervine` は他ファイルで `ウォーターヴァイン` / `ウォーターバイン農家` があるのに本行だけ英語
  - `bulkhead` は common noun なので訳すべき
  - `Spray-a-Brain` は DisplayName がローカライズ済みなのに説明文では raw English
  - `Very Old Bird` は説明文だけ raw English

### 4. `Short` では 1 行完結前提のレイアウトを崩す行がある

- `Items.jp.xml:7402` の `&#10;`
- `Items.jp.xml:5265-5284` の大量空白 + raw `&C`
- `{{...}}` のダブルブレース不整合自体は検出されなかったが、**行内フォーマットの崩れ**は別系統で存在する。

### 5. `HiddenObjects.jp.xml` は少数精鋭で重い問題がある

- 件数は多くないが、`Ehalcodon` や `BaseEaterGreatMachineShrine` のような lore-heavy な行で意味が崩れている。
- 星船まわりは prose quality 自体は高めだが、`bulkhead` のような common noun の取り残しがある。

### 6. `Foods.jp.xml` は今回の監査では大きな赤旗なし

- 原文比較と構文スキャンの範囲では、上位30件に入る高信頼の問題は見つからなかった。
- ただし「問題がゼロ」という意味ではなく、**今回の4ファイル監査では他3ファイルの問題密度が圧倒的に高かった**という位置づけ。

## Proper noun mismatches with `glossary.csv`

| Term / route | Glossary / existing repo form | In-scope mismatch | Evidence |
|---|---|---|---|
| `Eaters` | `喰らう者` | raw `Eaters` と `イーター` が混在 | `Items.jp.xml:7949, 9384` / `Furniture.jp.xml:145, 525` ほか計 **27件** |
| `Qas` | `カス` | 本文だけ `Qas` | `Furniture.jp.xml:534` |
| `Moghra'yi` | `モグラヤイ` | `モグラーイー` | `Furniture.jp.xml:3919` |
| `Watervine` | repo 既存表記 `ウォーターヴァイン` / `ウォーターバイン農家` | raw `Watervine` | `Furniture.jp.xml:3750` |
| `Spray-a-Brain` | DisplayName / wish text はローカライズ済み (`スプレイ-ア-ブレイン`, `スプレイ・ア・ブレイン缶`) | 説明文では raw English | `Items.jp.xml:1486, 1674` |
| `Very Old Bird` | 未登録 | raw English のまま | `Items.jp.xml:10687` |

## Recommended triage order

1. **表示崩れ系を先に直す**
   - `Items.jp.xml:7402`
   - `Items.jp.xml:5265-5284`
   - `Items.jp.xml` の `*スルタン*` 17件

2. **`Furniture.jp.xml` 前半の MT-heavy な行を文単位で再訳する**
   - `Sewing Machine`
   - `Light Sculpture`
   - `Unfinished Sculpture`
   - `Mushroom Case`
   - 神殿群 (`Agolgot` / `Bethsaida` / `Rermadon` / `Shugruith` / `Qas` / `Qon`)

3. **glossary 衝突を整理する**
   - `Eaters` / `イーター` → `喰らう者`
   - `Qas` → `カス`
   - `Moghra'yi` → `モグラヤイ`
   - `Watervine`, `Spray-a-Brain`, `Very Old Bird` の正式表記決定

4. **`HiddenObjects.jp.xml` の marquee lore rows を再訳する**
   - `Ehalcodon`
   - `BaseEaterGreatMachineShrine`
   - `Mechanical Golem`
