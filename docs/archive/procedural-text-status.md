# プロシージャルテキスト対応状況

`HistoryKit.HistoricStringExpander.ExpandString()` 向けの runtime patch は、現在 **意図的に無効化** されています。

無効化の実装根拠は `Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs` で、
`TargetMethods()` が warning を出したうえで `yield break` します。これは履歴文の表示だけでなく
`HistorySpice` / world generation 用の symbolic key まで翻訳してしまい、プレイアビリティを壊す
危険が確認されたためです。

## 現在の扱い

- ステータス: `intentionally disabled`
- 理由: world generation / playability safeguard
- runtime 挙動: patch 登録時に warning を出して対象メソッドを返さない
- テスト対象: `Postfix` 本体の文字列変換ロジックだけは L1/L2 で維持する
- 表示レイヤーでの部分対応:
  - `PopupShowTranslationPatch` → `PopupTranslationPatch` の pattern fallback で `spice.cooking.ate` (`You eat the meal.`) を翻訳
  - `JournalObservationAddTranslationPatch` / `JournalPatternTranslator` で `spice.gossip.twoFaction` 展開後の gossip 文を翻訳
  - `DescriptionShortDescriptionPatch` / `DescriptionLongDescriptionPatch` → `DescriptionTextTranslator` で `spice.villages.description` 展開後の村説明を翻訳
  - `historyspice-common.ja.json` を `{tN}` capture 翻訳の語彙ソースとして利用

## 観測上の意味

- `HistoricStringExpander` 系の英語断片は、現在の untranslated-text observability baseline では
  一部が display-layer patch で回収されるが、依然として **既知の blind spot** を含む
- `missing key` / `no pattern` の集計対象に入らないため、coverage の母集団へ混ぜない
- まずは Rosetta 起動下で world generation が安定することを優先する

## 今回の到達点

- `HistoricStringExpanderPatch` 自体は再有効化していない
- 代わりに、実際に表示される下流レイヤーへ限定して pattern 翻訳を追加した
- 対応済みファミリー:
  - `spice.cooking.ate` — `src/Patches/PopupTranslationPatch.cs` / テスト: `L2/PopupShowTranslationPatchTests.cs`
  - `spice.gossip.twoFaction`（journal observation 保存時）— `src/Patches/JournalPatternTranslator.cs` / テスト: `L1/JournalPatternTranslatorTests.cs`, `L2/JournalApiAddTranslationPatchTests.cs`
  - `spice.villages.description` — `src/Patches/DescriptionTextTranslator.cs` / テスト: `L2/DescriptionShortDescriptionPatchTests.cs`, `L2/DescriptionLongDescriptionPatchTests.cs`
- このため、world generation / `HistorySpice` symbolic key 汚染のリスクを増やさずに、
  既知の表示英語断片だけを局所的に回収できる

## 未対応 / 今後の候補

- `Popup.Show` 系の `spice.gossip.leadIns` など、未パターン化の HistorySpice 表示文 — 対応開始時は `src/Patches/PopupTranslationPatch.cs` と `L1/MessagePatternTranslatorTests.cs` を確認
- `spice.commonPhrases.*` のうち `historyspice-common.ja.json` がまだ持っていない語彙カテゴリ — `Localization/Dictionaries/historyspice-common.ja.json` に語彙を追加し `L1/JournalPatternTranslatorTests.cs` でカバレッジを確認
- ほかの `HistoricStringExpander` 呼び出しで、表示専用だと確認できた route の downstream 対応 — `src/Patches/HistoricStringExpanderPatch.cs` の `TargetMethods()` を起点に route を精査

## 今後の再導入条件

- 表示専用ケースへ適用範囲を限定できること
- history generation 用の symbolic key を翻訳対象から確実に除外できること
- Rosetta 起動の実ゲーム確認で world generation が通ること
- untranslated observability と playability の証跡を分離して提示できること
