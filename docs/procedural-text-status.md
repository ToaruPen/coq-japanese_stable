# プロシージャルテキスト対応状況（Phase 1）

`HistoryKit.HistoricStringExpander.ExpandString()` の Harmony Postfix で、展開後の英語文字列を
`UITextSkinTranslationPatch.TranslatePreservingColors()` に通す初期対応を実装しています。

この段階は **最小・安全な初期対応** です。英語展開そのものはゲーム本体に任せ、最終結果だけ辞書照合します。

## 対応済み

- 静的に近い履歴文（展開結果が辞書キーと完全一致するもの）は翻訳される。
- `{{R|...}}` / `&G` / `^k` などの色コードは保持される。

例:

- 入力（展開後）: `In the beginning, Resheph created Qud`
- 辞書キー: `In the beginning, Resheph created Qud`
- 出力: `はじめに、レシェフがクッドを創造した`

## 部分対応

- テンプレート展開文でも、**最終文字列がたまたま辞書キーに一致**すれば翻訳される。
- 一部だけ一致するケース（部分文字列一致）は翻訳されない（現状は完全一致のみ）。

例:

- 展開結果: `Sultan was crowned`
- 辞書キー: `Sultan was crowned`
- 出力: `スルタンが戴冠した`

一方で以下は未翻訳（完全一致しないため）:

- 展開結果: `Sultan was crowned in 1024`
- 辞書キー: `Sultan was crowned`

## 未対応

- 完全プロシージャル文（ランダム生成された固有名・語順・可変スロットを含む文）。
- `Messaging.XDidY()` / `Messaging.XDidYToZ()` 系の SVO -> SOV 変換。
- テンプレート単位の翻訳（展開前トークンを使った変換）。
- 変数を意識したパターン翻訳（`=...=` 相当の意味を使う可変翻訳）。

例:

- `{{G|Sultan Ehra-iv was crowned in the 3rd year of ...}}`（毎回変化）
- `snapjaw hits you for 5 damage!`（メッセージテンプレート変形）

## Phase 2 TODO

- `XDidY` / `XDidYToZ` の日本語語順（SOV）変換を追加。
- 展開後文字列ではなくテンプレートレベルでの翻訳導入を検討。
- 変数・固有名スロットを扱えるパターン照合（variable-aware matching）を実装。
- ランダム生成文向けに、辞書完全一致以外の段階的フォールバック戦略を設計。
