# Learnings

## 2026-03-10 Session Start
- GitHub repo created: ToaruPen/Caves-of-Qud_Japanese (public)
- Default branch: main
- Protocol: SSH
- Initial commit: f26f8d7

## 2026-03-11 Task 0 PoC
- macOS + dotnet SDK 10.0.100 では `net10.0` の `dotnet test` が安定して実行可能。
- `Assembly-CSharp.dll` 参照で `ConsoleLib.Console.Markup` のメソッド署名取得が可能。
- ゲーム同梱 `0Harmony.dll` は `Version=2.2.2.0` を確認。
- Unity依存型のサンプル実体化では `TypeInitializationException` を再現できず（観測値は `False`）。

## 2026-03-11 Task 3 Legacy XML監査
- レガシー `Localization/` 配下は XML 35ファイル（66,306行）・辞書JSON 35ファイル（32,836行）・再帰全76ファイルを確認。
- XML 35/35 が `xml.etree.ElementTree` でパース成功、モジバケ指標文字（繧/縺/驕/蜒）は0件。
- XMLのBOM付きは15ファイル、辞書JSONのBOM付きは2ファイル（`mutation-ranktext.ja.json`, `world-parts.ja.json`）。
- カバレッジ比較（現行Base 2.0.4）で `ObjectBlueprints` 77.01%（4020/5220）、`Conversations` 97.5%（195/200）、他カテゴリは100%一致を確認。
- `glossary.csv` は列 `English, Japanese, Short, Notes, Status` で83エントリ（ヘッダ除く）、移行利用可能。

## 2026-03-11 Task 2 Game Data Analysis
- StreamingAssets/Base の XML は 44 ファイル (総計約 103,408 行) を確認。
- 大分類: ObjectBlueprints 15 / Conversations 3 / UI-Options 4 / Skills-Mutations 4 / Other 18。
- 翻訳文字列の近似は Static 約9,427、Semi-dynamic 約874、Dynamic は実行時生成で固定件数化不可。
- 色コード頻度は `{[A-Z]|` 826、`&amp;[A-Za-z]` 2,356、`^[A-Za-z]` 419。`&amp;&amp;` は13件、`^^` はBase XMLで0件。
- 会話 Load 実装は `merge(0)/replace(1)/add(2)/remove(3)` を確認。`MergeIfExists` は Base XML と ilspy-raw で未観測。
- 出力ドキュメント: `docs/game-data-analysis.md` を新規作成。

## AGENTS.md scaffolding (2026-03-11)

- Root AGENTS.md: 133 lines (well under 200 limit)
- Assemblies AGENTS.md: 155 lines — Harmony prefix/postfix patterns, DummyTarget pattern, HarmonyLib version split
- scripts AGENTS.md: 155 lines — Ruff ALL rules, Google docstrings, verb_noun.py naming
- Localization AGENTS.md: 151 lines — color code formats ({{W|}}, &G, ^r), =variable.name= preservation, mojibake sequences to avoid
- CLAUDE.md symlink: `ln -s AGENTS.md CLAUDE.md` works fine on macOS; `test -f CLAUDE.md` resolves through symlink correctly
- `readlink CLAUDE.md` returns `AGENTS.md` (relative, not absolute) — correct behavior


## 2026-03-11 Task 4 ILSpy補完
- `XRL.Core.GameText` は存在せず、実体は `XRL.GameText`（`VariableReplace`/`Process`）。
- `XRL.Language.HistoricStringExpander` は存在せず、実体は `HistoryKit.HistoricStringExpander`。
- `XRL.World.Conversations.ConversationUI` は存在せず、実体は `XRL.UI.ConversationUI`。
- 会話テキストイベント実体は `XRL.World.Conversations.DisplayTextEvent` / `PrepareTextEvent` / `PrepareTextLateEvent`。
- `ConversationLoader` は ID 衝突時に `Load=Merge(0)` で `ConversationXMLBlueprint.Merge()`、それ以外は辞書上書き。
- `Load=MergeIfExists` は会話ではなく `ObjectBlueprintLoader.ReadObjectsNode()` 側に実装（存在時のみmerge、不存在は無言スキップ）。
- `DescriptionBuilder` の優先順は `Mark(-800) -> Adjective(-500) -> Base(10) -> Clause(600) -> Tag(1100)`。
- `Markup` のエスケープは `&&` と `^^` がリテラル扱い（色コード開始を抑止）。
- `IComponent<T>` に `XDidY` / `XDidYToZ` / `DidX` / `DidXToY` ラッパーがあり、内部で `Messaging.*` へ委譲。
