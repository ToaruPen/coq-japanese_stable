# Caves of Qud ゲームデータ構造分析 (StreamingAssets/Base)

## 0. スコープ

- 対象パス:
  - `/Users/sankenbisha/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/`
- 対象:
  - `Base/*.xml` (トップレベル在庫)
  - `Base/**/*.xml` (ObjectBlueprints 配下を含む実運用データ)
- 観測対象ゲーム版:
  - 2.0.4

---

## 1. XML ファイル在庫 (行数付き)

### 1.1 `Base/*.xml` 在庫 (トップレベル)

`wc -l Base/*.xml` の実測:

```text
    1733 ActivatedAbilities.xml
    1384 Bodies.xml
    1865 Books.xml
     717 ChiliadFactions.xml
     189 Colors.xml
     876 Commands.xml
      56 Compat.xml
   14004 Conversations.xml
     471 EmbarkModules.xml
    1940 Factions.xml
     422 Genders.xml
      42 Genotypes.xml
    2222 HiddenConversations.xml
      60 HiddenMutations.xml
     558 Manual.xml
      86 Mods.xml
     120 Mutations.xml
    5194 Naming.xml
       4 ObjectBlueprints.xml
     226 Options.xml
   19218 PopulationTables.xml
      49 PronounSets.xml
     542 Quests.xml
      85 Relics.xml
     289 Skills.xml
      82 SparkingBaetyls.xml
     332 Subtypes.xml
      16 WishCommands.xml
    2498 Worlds.xml
    1619 ZoneTemplates.xml
   56899 total
```

### 1.2 再帰集計 (実データ量)

- `Base/**/*.xml` 総数: **44 files**
- 総行数: **103,408 lines**
- 補足: `ObjectBlueprints.xml` は空に近く、実体は `Base/ObjectBlueprints/*.xml` に分割格納。

---

## 2. 翻訳対象テキストの分類

## 2.1 静的テキスト

定義: XML 上に固定値として格納され、実行時変数展開を伴わない文言。

- 例 (`Options.xml`): `DisplayText="Main volume"` (`Options.xml:3`)
- 例 (`Skills.xml`): `Description="You are skilled at acrobatics."` (`Skills.xml:4`)
- 例 (`Mutations.xml`): `DisplayName="{{G|Physical Mutations}}"` (`Mutations.xml:9`)
- 例 (`Books.xml`): `<book ID="Skybear" Title="{{W|Song of the Sky-Bear}}">` (`Books.xml:3`)

## 2.2 半動的テキスト

定義: 固定文に `=...=` テンプレート変数を埋め込む形式。

- 例 (`Conversations.xml`): `Live and drink, =subject.waterRitualLiquid=-=player.siblingTerm=.` (`Conversations.xml:21`)
- 例 (`Conversations.xml`): `Would you teach me to craft =recipe=?` (`Conversations.xml:36`)
- 例 (`Conversations.xml`): `=subject.T= =verb:grab= ... =mutation.name=` (`Conversations.xml:70-72`)
- 例 (`Quests.xml`): `In the month of =month= of =year=, =name= ...` (`Quests.xml:6`)

## 2.3 動的テキスト

定義: XML 側は素材/規則を保持し、実文字列は実行時に Grammar / Messaging / HistoricStringExpander 系で生成される形式。

- 例 (`Quests.xml`): `&lt;spice.history.gospels.Celebration.LateSultanate.!random&gt;` (`Quests.xml:8`)
- 例 (`Quests.xml`): `&lt;spice.elements.entity$elements[random].weddingConditions.!random.capitalize&gt;` (`Quests.xml:48`)
- 例 (`Naming.xml`): `<template Name="*Name**Suffix*" ... />` (`Naming.xml:1347`)

補足: 上記は「動的生成の入力データ」。最終文面はランタイムで決定される。

---

## 3. 色コード仕様 (3形式)

集計は `Base/**/*.xml` 対象。

### 3.1 形式1: `{{W|text}}` (インラインマークアップ)

- 出現数: **1,260**
- 例: `{{W|Fit}}` (`Options.xml:60`)
- 例: `DisplayName="{{G|Physical Mutations}}"` (`Mutations.xml:9`)
- 例: `Title="{{W|Song of the Sky-Bear}}"` (`Books.xml:3`)

### 3.2 形式2: `&G` (前景色)

XML上は `&amp;G` として保存される。

- 出現数 (`&amp;[A-Za-z]`): **4,080**
- 例: `ColorString="&amp;r"` (`ObjectBlueprints/Widgets.xml:460`)
- 例: `ColorString="&amp;w"` (`ObjectBlueprints/Items.xml:81`)
- 例: `&amp;mQ Girl&amp;y` (`Conversations.xml:1968`)

### 3.3 形式3: `^r` (背景色)

多くは前景色と結合して `&amp;Y^k` 形式で記述。

- 出現数 (`&amp;[A-Za-z]\^[A-Za-z]`): **428**
- 例: `Value="&amp;Y^k"` (`ObjectBlueprints/Items.xml:38`)
- 例: `ColorString="&amp;y^k"` (`ObjectBlueprints/Walls.xml:20`)
- 例: `ColorString="&amp;Y^y"` (`ObjectBlueprints/PhysicalPhenomena.xml:400`)

---

## 4. `Load="..."` セマンティクス

### 4.1 観測されたバリアントと件数

- `Load="Merge"`: **1**
- `Load="Replace"`: **10**
- `Load="Remove"`: **35**
- `Load="Fill"`: **7**

### 4.2 実例

- Merge: `<choice ID="Trade" ... Load="Merge">` (`Conversations.xml:171`)
- Replace: `<choice ID="GiveTrinket" Load="Replace" ...>` (`Conversations.xml:347`)
- Replace: `<text ID="FindTrinket" Load="Replace">` (`Conversations.xml:353`)
- Remove: `<choice ID="Trade" Load="Remove">` (`HiddenConversations.xml:12`)
- Fill: `<mixin Name="BaseAnimatedObject" Load="Fill" />` (`ObjectBlueprints/Creatures.xml:11568`)

### 4.3 意味 (運用観点)

- `Merge`: 既存IDノードに追記/上書き合成 (会話ID衝突時の基本動作)
- `Replace`: 同一ID要素の差し替え
- `Remove`: 同一ID要素の削除
- `Fill`: 観測上、ObjectBlueprints の mixin 継承時に利用 (既存定義を残しつつ不足補完する用途)

---

## 5. 主要スキーマ分析

## 5.1 ObjectBlueprints

- ルート: `<objects>`
- 実データ: `Base/ObjectBlueprints/*.xml`
- 主要構造:
  - `<object Name="..." Inherits="...">`
  - `<part Name="Render" DisplayName="..." ... />`
  - `<part Name="Description" Short="..." />`

主要キー実測:

- `object Name`: **5,223**
- `part Name`: **16,414**
- 翻訳キー候補: `DisplayName` / `Short`

例:

- `ObjectBlueprints/Items.xml:35-40`
- `ObjectBlueprints/Items.xml:198-201`

## 5.2 Conversations

- ルート: `<conversations>`
- 主要構造:
  - `<conversation ID="..." Inherits="...">`
  - `<node ID="...">` / `<start ID="...">`
  - `<text ...>...</text>`
  - `<choice ID="..." Target/GotoID="...">...</choice>`

主要キー実測 (`Conversations.xml` + `HiddenConversations.xml`):

- `conversation ID`: **212**
- `node/start ID`: **1,622**
- `choice ID`: **249**
- `text` ノード: **1,728**

## 5.3 Options

- ルート: `<options>`
- 主要構造: `<option ID="..." DisplayText="..." Category="..." Type="..." ... />`
- キー実測:
  - `option ID`: **187**
  - `DisplayText`: **187**

## 5.4 Skills

- ルート: `<skills>`
- 主要構造:
  - `<skill Name="..." Description="..." ...>`
  - `<power Name="..." Description="..." ...>`
- キー実測:
  - `skill Name`: **21**
  - `power Name`: **123**

## 5.5 Mutations

- ルート: `<mutations>`
- 主要構造:
  - `<category Name="..." DisplayName="...">`
  - `<mutation Name="..." ... />`
  - 一部 `<description>...</description>` / `<leveltext>`
- キー実測:
  - `mutation Name`: **82**
  - `category DisplayName`: **10**

## 5.6 Books

- ルート: `<books>`
- 主要構造:
  - `<book ID="..." Title="...">`
  - `<page>...</page>`
- キー実測:
  - `book ID`: **53**
  - `page`: **210**

---

## 6. 定量サマリ

## 6.1 ファイル規模

- トップレベル XML: **30 files / 56,899 lines**
- 再帰 XML: **44 files / 103,408 lines**

## 6.2 推定翻訳文字列数 (カテゴリ別)

以下は正規表現ベースの概算 (重複・文脈依存を含む):

- 静的テキスト候補 (約 **7,919**)
  - ObjectBlueprints `DisplayName+Short`: 5,668
  - Conversations/Hidden/Quests の静的 `text` ノード: 1,421
  - Options `DisplayText`: 187
  - Skills (`skill/power Name + Description`): 288
  - Mutations (`Name + DisplayName`): 92
  - Books (`Title + page`): 263

- 半動的テキスト候補
  - `=...=` を含む `text` ノード: **364**
  - `=...=` トークン総数 (Conversations/Hidden/Quests): **723**

- 動的テキスト候補
  - `spice.` 参照総数 (全XML): **28**
  - `Naming.xml` の `*Placeholder*` テンプレート: **110**

## 6.3 ローカライズ難易度の含意

- 静的: 件数最大。XML差し替え中心で進めやすい。
- 半動的: 変数破壊を避ける翻訳規約が必須。
- 動的: Grammar/Messaging/HistoricStringExpander 側の実行時生成を含み、XMLのみでは完結しない。
