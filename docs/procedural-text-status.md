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

## 観測上の意味

- `HistoricStringExpander` 系の英語断片は、現在の untranslated-text observability baseline では
  **既知の blind spot** として扱う
- `missing key` / `no pattern` の集計対象に入らないため、coverage の母集団へ混ぜない
- まずは Rosetta 起動下で world generation が安定することを優先する

## 今後の再導入条件

- 表示専用ケースへ適用範囲を限定できること
- history generation 用の symbolic key を翻訳対象から確実に除外できること
- Rosetta 起動の実ゲーム確認で world generation が通ること
- untranslated observability と playability の証跡を分離して提示できること
